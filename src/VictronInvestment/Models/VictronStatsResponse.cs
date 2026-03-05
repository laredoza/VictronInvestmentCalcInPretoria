using System.Text.Json.Serialization;

namespace VictronInvestment.Models;

public class VictronStatsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("records")]
    public Dictionary<string, List<List<double>>> Records { get; set; } = new();

    [JsonPropertyName("totals")]
    public Dictionary<string, double> Totals { get; set; } = new();
}
