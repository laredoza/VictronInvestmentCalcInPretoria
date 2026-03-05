using System.Net.Http.Json;
using System.Text.Json;
using VictronInvestment.Configuration;
using VictronInvestment.Models;

namespace VictronInvestment.Services;

public class VictronApiService
{
    private readonly HttpClient _httpClient;
    private readonly VictronSettings _settings;
    private string? _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public VictronApiService(HttpClient httpClient, VictronSettings settings)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(settings.ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _settings = settings;
    }

    public async Task AuthenticateAsync()
    {
        var loginBody = new
        {
            username = _settings.Username,
            password = _settings.Password
        };

        var response = await _httpClient.PostAsJsonAsync("v2/auth/login", loginBody);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Authentication failed ({response.StatusCode}): {content}");

        var authResponse = JsonSerializer.Deserialize<VictronAuthResponse>(content, JsonOptions);

        if (authResponse == null || string.IsNullOrEmpty(authResponse.Token))
            throw new Exception($"Authentication failed: no token received. Response: {content}");

        _authToken = authResponse.Token;
    }

    public async Task<List<MonthlyEnergy>> FetchYearlyEnergyAsync(int year)
    {
        if (_authToken == null)
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first.");

        var installationId = _settings.InstallationId;

        // Calculate epoch timestamps for the year range.
        // Victron timestamps are shifted: Dec 31 of prev year = January data.
        // So we start from Dec 1 of the previous year to capture January,
        // and end at Dec 31 of the year (or now) to capture December.
        var startDate = new DateTimeOffset(year - 1, 12, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = year < DateTime.Now.Year
            ? new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero)
            : new DateTimeOffset(DateTime.UtcNow);

        var startEpoch = startDate.ToUnixTimeSeconds();
        var endEpoch = endDate.ToUnixTimeSeconds();

        var url = $"v2/installations/{installationId}/stats?type=kwh&interval=months&start={startEpoch}&end={endEpoch}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Authorization", $"Bearer {_authToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch energy data for {year} ({response.StatusCode}): {content}");

        var statsResponse = JsonSerializer.Deserialize<VictronStatsResponse>(content, JsonOptions);

        if (statsResponse is not { Success: true })
            throw new Exception($"Stats API error for {year}: {content}");

        return ConvertToMonthlyEnergy(year, statsResponse);
    }

    private static List<MonthlyEnergy> ConvertToMonthlyEnergy(int year, VictronStatsResponse stats)
    {
        // Victron energy flow codes:
        // Pb = PV to battery, Pg = PV to grid, Pc = PV to consumers
        // Gb = Grid to battery, Gc = Grid to consumers
        // Bc = Battery to consumers, Bg = Battery to grid

        // Victron timestamps use the last day of each month (e.g. 2024-01-31 = January).
        // The API returns the previous year's December as the first point (e.g. 2023-12-31
        // appears in the 2024 request). We treat this as December of (year-1), and since
        // we process years sequentially, we include it as December of the requested year - 1.
        // We build a (year, month) keyed dictionary to handle this correctly.
        var monthlyData = new Dictionary<(int Year, int Month), Dictionary<string, decimal>>();

        foreach (var (code, records) in stats.Records)
        {
            foreach (var record in records)
            {
                if (record.Count < 2) continue;

                var timestamp = record[0];
                var value = (decimal)record[1];

                // Convert epoch to date (Victron returns milliseconds).
                // Victron timestamps are the last day of the previous month
                // (e.g. 2025-12-31 = January 2026 data). Adding 1 day maps to
                // the 1st of the actual month the data represents.
                var epochMs = (long)timestamp;
                var date = DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime.AddDays(1);

                var key = (date.Year, date.Month);

                if (!monthlyData.ContainsKey(key))
                    monthlyData[key] = new Dictionary<string, decimal>();

                if (!monthlyData[key].ContainsKey(code))
                    monthlyData[key][code] = 0m;

                monthlyData[key][code] += value;
            }
        }

        var now = DateTime.Now;
        return monthlyData
            .OrderBy(kv => kv.Key.Year).ThenBy(kv => kv.Key.Month)
            .Select(kv =>
            {
                var (dataYear, month) = kv.Key;
                var codes = kv.Value;

                decimal GetCode(string c) => codes.TryGetValue(c, out var v) ? v : 0m;

                var pv = GetCode("Pb") + GetCode("Pg") + GetCode("Pc");
                var load = GetCode("Pc") + GetCode("Gc") + GetCode("Bc");

                return new MonthlyEnergy
                {
                    Year = dataYear,
                    Month = month,
                    Pv = pv,
                    Load = load,
                    Export = GetCode("Pg") + GetCode("Bg"),
                    Import = GetCode("Gc") + GetCode("Gb"),
                    Charge = GetCode("Pb") + GetCode("Gb"),
                    Discharge = GetCode("Bc") + GetCode("Bg"),
                    FetchedAt = now
                };
            })
            .Where(e => e.Pv > 0 || e.Load > 0)
            .ToList();
    }
}
