using Dapper;
using Microsoft.Data.Sqlite;

namespace VictronInvestment.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Tariffs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FinancialYear TEXT NOT NULL UNIQUE,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                Block1Rate REAL NOT NULL,
                Block2Rate REAL NOT NULL,
                Block3Rate REAL NOT NULL,
                Block4Rate REAL NOT NULL
            )
            """);

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS MonthlyEnergy (
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                Pv REAL NOT NULL DEFAULT 0,
                Load REAL NOT NULL DEFAULT 0,
                Export REAL NOT NULL DEFAULT 0,
                Import REAL NOT NULL DEFAULT 0,
                Discharge REAL NOT NULL DEFAULT 0,
                Charge REAL NOT NULL DEFAULT 0,
                FetchedAt TEXT NOT NULL,
                PRIMARY KEY (Year, Month)
            )
            """);

        await SeedTariffsAsync(connection);
    }

    private static async Task SeedTariffsAsync(SqliteConnection connection)
    {
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Tariffs");
        if (count > 0) return;

        var tariffs = new[]
        {
            new { FinancialYear = "2021/22", Start = "2021-07-01", End = "2022-06-30", B1 = 1.9519, B2 = 2.2844, B3 = 2.4881, B4 = 2.6823 },
            new { FinancialYear = "2022/23", Start = "2022-07-01", End = "2023-06-30", B1 = 2.0970, B2 = 2.4541, B3 = 2.6738, B4 = 2.8824 },
            new { FinancialYear = "2023/24", Start = "2023-07-01", End = "2024-06-30", B1 = 2.4137, B2 = 2.8247, B3 = 3.0775, B4 = 3.3176 },
            new { FinancialYear = "2024/25", Start = "2024-07-01", End = "2025-06-30", B1 = 2.7033, B2 = 3.1637, B3 = 3.4468, B4 = 3.7158 },
            new { FinancialYear = "2025/26", Start = "2025-07-01", End = "2026-06-30", B1 = 2.9790, B2 = 3.4864, B3 = 3.7983, B4 = 4.0948 },
        };

        foreach (var t in tariffs)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO Tariffs (FinancialYear, StartDate, EndDate, Block1Rate, Block2Rate, Block3Rate, Block4Rate)
                VALUES (@FinancialYear, @Start, @End, @B1, @B2, @B3, @B4)
                """, t);
        }
    }
}
