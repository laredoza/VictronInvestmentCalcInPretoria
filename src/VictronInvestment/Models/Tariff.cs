namespace VictronInvestment.Models;

public class Tariff
{
    public int Id { get; set; }
    public string FinancialYear { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Block1Rate { get; set; }
    public decimal Block2Rate { get; set; }
    public decimal Block3Rate { get; set; }
    public decimal Block4Rate { get; set; }
}
