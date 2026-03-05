namespace VictronInvestment.Models;

public class MonthlyEnergy
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Pv { get; set; }
    public decimal Load { get; set; }
    public decimal Export { get; set; }
    public decimal Import { get; set; }
    public decimal Discharge { get; set; }
    public decimal Charge { get; set; }
    public DateTime FetchedAt { get; set; }
}
