using Dapper;
using Microsoft.Data.Sqlite;
using VictronInvestment.Models;

namespace VictronInvestment.Data;

public class TariffRepository
{
    private readonly string _connectionString;

    public TariffRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Tariff?> GetTariffForDateAsync(DateTime date)
    {
        using var connection = new SqliteConnection(_connectionString);
        var dateStr = date.ToString("yyyy-MM-dd");
        return await connection.QueryFirstOrDefaultAsync<Tariff>(
            "SELECT * FROM Tariffs WHERE @Date >= StartDate AND @Date <= EndDate",
            new { Date = dateStr });
    }
}

public class EnergyRepository
{
    private readonly string _connectionString;
    private readonly int _cacheStaleHours;

    public EnergyRepository(string connectionString, int cacheStaleHours = 24)
    {
        _connectionString = connectionString;
        _cacheStaleHours = cacheStaleHours;
    }

    public async Task<List<MonthlyEnergy>> GetCachedEnergyAsync(int year)
    {
        using var connection = new SqliteConnection(_connectionString);
        var results = await connection.QueryAsync<MonthlyEnergy>(
            "SELECT * FROM MonthlyEnergy WHERE Year = @Year ORDER BY Month",
            new { Year = year });
        return results.ToList();
    }

    public async Task<bool> IsCacheStaleAsync(int year)
    {
        using var connection = new SqliteConnection(_connectionString);

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM MonthlyEnergy WHERE Year = @Year",
            new { Year = year });

        // If we have fewer than 2 months cached, treat as stale (incomplete fetch)
        if (count < 2) return true;

        var fetchedAt = await connection.ExecuteScalarAsync<string?>(
            "SELECT MAX(FetchedAt) FROM MonthlyEnergy WHERE Year = @Year",
            new { Year = year });

        if (fetchedAt == null) return true;

        if (year < DateTime.Now.Year)
            return false; // completed years never go stale

        var lastFetch = DateTime.Parse(fetchedAt);
        return (DateTime.Now - lastFetch).TotalHours > _cacheStaleHours;
    }

    public async Task UpsertMonthlyEnergyAsync(MonthlyEnergy energy)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            """
            INSERT INTO MonthlyEnergy (Year, Month, Pv, Load, Export, Import, Discharge, Charge, FetchedAt)
            VALUES (@Year, @Month, @Pv, @Load, @Export, @Import, @Discharge, @Charge, @FetchedAt)
            ON CONFLICT(Year, Month) DO UPDATE SET
                Pv = @Pv, Load = @Load, Export = @Export, Import = @Import,
                Discharge = @Discharge, Charge = @Charge, FetchedAt = @FetchedAt
            """, energy);
    }
}
