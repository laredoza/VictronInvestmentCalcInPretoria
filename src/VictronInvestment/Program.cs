using System.Globalization;
using Microsoft.Extensions.Configuration;
using VictronInvestment.Configuration;
using VictronInvestment.Data;
using VictronInvestment.Models;
using VictronInvestment.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var victronSettings = new VictronSettings();
config.GetSection("Victron").Bind(victronSettings);
var totalInvestment = config.GetValue<decimal>("TotalInvestmentAmount", 0);
var connectionString = config["Database:ConnectionString"]
    ?? "Data Source=victron_investment.db";
var startYear = config.GetValue<int>("StartYear", 2022);
var cacheStaleHours = config.GetValue<int>("CacheStaleHours", 24);

// Initialize database
var dbInit = new DatabaseInitializer(connectionString);
await dbInit.InitializeAsync();

var tariffRepo = new TariffRepository(connectionString);
var energyRepo = new EnergyRepository(connectionString, cacheStaleHours);
var calculator = new SavingsCalculator();

// Authenticate with Victron VRM
Console.WriteLine("Authenticating with Victron VRM...");
var httpClient = new HttpClient();
var apiService = new VictronApiService(httpClient, victronSettings);

try
{
    await apiService.AuthenticateAsync();
    Console.WriteLine("Authenticated successfully.\n");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Authentication failed: {ex.Message}");
    Console.ResetColor();
    return;
}

// Fetch data for each year
var currentYear = DateTime.Now.Year;
var allMonthlyData = new List<(MonthlyEnergy Energy, Tariff Tariff, decimal SavingsRand)>();

for (var year = startYear; year <= currentYear; year++)
{
    List<MonthlyEnergy> energyData;

    if (await energyRepo.IsCacheStaleAsync(year))
    {
        Console.WriteLine($"Fetching {year} data from Victron VRM API...");
        try
        {
            energyData = await apiService.FetchYearlyEnergyAsync(year);
            foreach (var e in energyData)
                await energyRepo.UpsertMonthlyEnergyAsync(e);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Warning: Could not fetch {year}: {ex.Message}");
            Console.ResetColor();
            energyData = await energyRepo.GetCachedEnergyAsync(year);
            if (energyData.Count == 0) continue;
            Console.WriteLine($"  Using cached data for {year}.");
        }
    }
    else
    {
        Console.WriteLine($"Using cached data for {year}.");
        energyData = await energyRepo.GetCachedEnergyAsync(year);
    }

    var missingTariffMonths = new List<string>();

    foreach (var energy in energyData)
    {
        var date = new DateTime(energy.Year, energy.Month, 1);
        var tariff = await tariffRepo.GetTariffForDateAsync(date);
        if (tariff == null)
        {
            missingTariffMonths.Add(date.ToString("MMM yyyy"));
            continue;
        }

        var savingsRand = calculator.CalculateCost(energy.Pv, tariff);
        allMonthlyData.Add((energy, tariff, savingsRand));
    }

    if (missingTariffMonths.Count > 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (missingTariffMonths.Count == energyData.Count)
            Console.WriteLine($"  Warning: No tariff data for {year}. Add tariffs in DatabaseInitializer.cs to include this year.");
        else
            Console.WriteLine($"  Warning: No tariff found for {string.Join(", ", missingTariffMonths)}, skipping.");
        Console.ResetColor();
    }
}

// Display report
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║              Victron Solar Investment Report                     ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine();

decimal grandTotal = 0m;
decimal grandTotalKwh = 0m;

var byYear = allMonthlyData.GroupBy(x => x.Energy.Year).OrderBy(g => g.Key);

foreach (var yearGroup in byYear)
{
    var year = yearGroup.Key;
    Console.WriteLine($"── {year} ──────────────────────────────────────────────────────");
    Console.WriteLine();

    decimal yearTotal = 0m;
    decimal yearKwh = 0m;

    foreach (var (energy, tariff, savingsRand) in yearGroup.OrderBy(x => x.Energy.Month))
    {
        var monthName = new DateTime(energy.Year, energy.Month, 1).ToString("MMM", CultureInfo.InvariantCulture);
        Console.WriteLine(
            $"  {monthName,-5} PV: {energy.Pv,8:F1} kWh    Saved: R {savingsRand,10:N2}   (FY {tariff.FinancialYear})");

        yearTotal += savingsRand;
        yearKwh += energy.Pv;
    }

    Console.WriteLine($"                                    ───────────────");
    Console.WriteLine(
        $"  {year} Total:  {yearKwh,8:F1} kWh            R {yearTotal,10:N2}");
    Console.WriteLine();

    grandTotal += yearTotal;
    grandTotalKwh += yearKwh;
}

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine(
    $"║  Grand Total:  {grandTotalKwh,10:F1} kWh           R {grandTotal,10:N2}             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");

// Investment payoff calculation
if (totalInvestment > 0 && allMonthlyData.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"  Investment:  R {totalInvestment,10:N2}");
    Console.WriteLine($"  Recovered:   R {grandTotal,10:N2}");
    Console.WriteLine($"  Remaining:   R {Math.Max(0, totalInvestment - grandTotal),10:N2}");
    Console.WriteLine();

    if (grandTotal >= totalInvestment)
    {
        // Find the month where we crossed the threshold
        decimal running = 0m;
        int? payoffYear = null;
        string? payoffMonth = null;
        foreach (var (energy, tariff, savingsRand) in allMonthlyData.OrderBy(x => x.Energy.Year).ThenBy(x => x.Energy.Month))
        {
            running += savingsRand;
            if (running >= totalInvestment)
            {
                payoffYear = energy.Year;
                payoffMonth = new DateTime(energy.Year, energy.Month, 1).ToString("MMM", CultureInfo.InvariantCulture);
                break;
            }
        }

        var firstMonth = allMonthlyData.Min(x => new DateTime(x.Energy.Year, x.Energy.Month, 1));
        var payoffDate = new DateTime(payoffYear!.Value, allMonthlyData.First(x => x.Energy.Year == payoffYear).Energy.Month, 1);
        var yearsToPayoff = (payoffDate - firstMonth).TotalDays / 365.25;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ** Investment PAID OFF in {payoffMonth} {payoffYear} ({yearsToPayoff:F1} years) **");
        Console.ResetColor();
    }
    else
    {
        var remaining = totalInvestment - grandTotal;

        // Calculate average monthly savings from last 12 months of data
        var recentMonths = allMonthlyData
            .OrderByDescending(x => x.Energy.Year)
            .ThenByDescending(x => x.Energy.Month)
            .Take(12)
            .ToList();

        var avgMonthlySavings = recentMonths.Average(x => x.SavingsRand);
        var monthsRemaining = remaining / avgMonthlySavings;
        var payoffDate = DateTime.Now.AddMonths((int)Math.Ceiling(monthsRemaining));
        var firstMonth = allMonthlyData.Min(x => new DateTime(x.Energy.Year, x.Energy.Month, 1));
        var totalYears = (payoffDate - firstMonth).TotalDays / 365.25;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Avg monthly savings (last 12 months): R {avgMonthlySavings:N2}");
        Console.WriteLine($"  Estimated payoff: {payoffDate:MMM yyyy} ({totalYears:F1} years from install)");
        Console.ResetColor();
    }
}
