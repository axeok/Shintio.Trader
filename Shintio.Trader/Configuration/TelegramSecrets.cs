namespace Shintio.Trader.Configuration;

public sealed class TelegramSecrets
{
    public string Phone { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public int ApiId { get; set; } = 0;
    public string ApiHash { get; set; } = "";
}