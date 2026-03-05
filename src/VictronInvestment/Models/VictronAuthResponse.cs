using System.Text.Json.Serialization;

namespace VictronInvestment.Models;

public class VictronAuthResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("idUser")]
    public int IdUser { get; set; }
}
