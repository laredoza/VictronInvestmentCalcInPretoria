# Victron Solar Investment Calculator

A .NET console application that calculates how much money you've saved by generating solar power, using real data from the Victron VRM API and City of Tshwane electricity tariffs.

## Features

- Fetches monthly solar generation data from the Victron VRM API
- Applies City of Tshwane inclining block tariffs (2021/22 - 2025/26)
- Caches API data in SQLite to avoid redundant API calls
- Displays a yearly breakdown of PV generation and savings in Rands
- Calculates investment payoff timeline based on actual and projected savings
- Warns when tariff data is missing for fetched energy periods

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Victron VRM account (https://vrm.victronenergy.com)

## Setup

1. Clone the repository

2. Copy the example config and fill in your credentials:
   ```bash
   cp src/VictronInvestment/appsettings.example.json src/VictronInvestment/appsettings.json
   ```

3. Edit `src/VictronInvestment/appsettings.json`:
   ```json
   {
     "Victron": {
       "Username": "your-email@example.com",
       "Password": "your-vrm-password",
       "InstallationId": 0,
       "ApiBaseUrl": "https://vrmapi.victronenergy.com/"
     },
     "StartYear": 2022,
     "TotalInvestmentAmount": 150000,
     "CacheStaleHours": 24,
     "Database": {
       "ConnectionString": "Data Source=victron_investment.db"
     }
   }
   ```

   | Setting | Description |
   |---------|-------------|
   | `Username` | Your Victron VRM email |
   | `Password` | Your Victron VRM password |
   | `InstallationId` | Your installation ID (visible in the VRM portal URL) |
   | `ApiBaseUrl` | Victron VRM API base URL (default: `https://vrmapi.victronenergy.com/`) |
   | `StartYear` | The first year to fetch energy data for |
   | `TotalInvestmentAmount` | Total cost of your solar installation in Rands. Set to `0` to skip payoff calculation. |
   | `CacheStaleHours` | Hours before current-year cached data is refreshed (default: `24`) |

## Usage

```bash
cd src/VictronInvestment
dotnet run
```

On first run the app will:
1. Create the SQLite database and seed tariff data
2. Authenticate with the Victron VRM API
3. Fetch monthly energy data for each year (from `StartYear` onwards)
4. Display the savings report
5. Warn if any months/years are missing tariff data

Subsequent runs use cached data for completed years. Current year data refreshes after `CacheStaleHours` (default: 24 hours).

### Refreshing all data

Delete the database file to force a full re-fetch:
```bash
rm src/VictronInvestment/victron_investment.db
dotnet run
```

## Tariffs

City of Tshwane residential electricity tariffs (excl. VAT) are stored in SQLite and seeded on first run:

| Financial Year | 0-100 kWh | 101-400 kWh | 401-650 kWh | >650 kWh |
|---|---|---|---|---|
| 2021/22 | R1.9519 | R2.2844 | R2.4881 | R2.6823 |
| 2022/23 | R2.0970 | R2.4541 | R2.6738 | R2.8824 |
| 2023/24 | R2.4137 | R2.8247 | R3.0775 | R3.3176 |
| 2024/25 | R2.7033 | R3.1637 | R3.4468 | R3.7158 |
| 2025/26 | R2.9790 | R3.4864 | R3.7983 | R4.0948 |

Financial years run July to June. If the app warns about missing tariff data for a year, add the new rates to the seed data in `Data/DatabaseInitializer.cs` and delete the database to re-seed.

## Project Structure

```
src/VictronInvestment/
  Program.cs                    # Entry point and report display
  Configuration/
    VictronSettings.cs          # Config POCO
  Models/
    VictronAuthResponse.cs      # Auth API response
    VictronStatsResponse.cs     # Stats API response
    MonthlyEnergy.cs            # Cached energy record
    Tariff.cs                   # Tariff rates
  Services/
    VictronApiService.cs        # VRM API auth + data fetching
    SavingsCalculator.cs        # Block tariff calculation
  Data/
    DatabaseInitializer.cs      # Schema creation + tariff seeding
    Repositories.cs             # SQLite data access
```
