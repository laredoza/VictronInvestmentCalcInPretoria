namespace VictronInvestment.Configuration;

public class VictronSettings
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int InstallationId { get; set; }
    public string ApiBaseUrl { get; set; } = "https://vrmapi.victronenergy.com/";
}
