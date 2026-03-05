using VictronInvestment.Models;

namespace VictronInvestment.Services;

public class SavingsCalculator
{
    /// <summary>
    /// Calculates the Rand value of kWh using the Tshwane inclining block tariff.
    /// Block 1: 0-100 kWh, Block 2: 101-400 kWh, Block 3: 401-650 kWh, Block 4: >650 kWh
    /// </summary>
    public decimal CalculateCost(decimal kWh, Tariff tariff)
    {
        if (kWh <= 0) return 0m;

        decimal cost = 0m;
        decimal remaining = kWh;

        // Block 1: first 100 kWh
        var block1 = Math.Min(remaining, 100m);
        cost += block1 * tariff.Block1Rate;
        remaining -= block1;
        if (remaining <= 0) return Math.Round(cost, 2);

        // Block 2: next 300 kWh (101-400)
        var block2 = Math.Min(remaining, 300m);
        cost += block2 * tariff.Block2Rate;
        remaining -= block2;
        if (remaining <= 0) return Math.Round(cost, 2);

        // Block 3: next 250 kWh (401-650)
        var block3 = Math.Min(remaining, 250m);
        cost += block3 * tariff.Block3Rate;
        remaining -= block3;
        if (remaining <= 0) return Math.Round(cost, 2);

        // Block 4: everything above 650 kWh
        cost += remaining * tariff.Block4Rate;
        return Math.Round(cost, 2);
    }
}
