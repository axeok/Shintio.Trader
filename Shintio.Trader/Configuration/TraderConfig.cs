namespace Shintio.Trader.Configuration;

public sealed class TraderConfig
{
    public long LogsChannelId { get; set; } = 0;
    public int? LogsThreadId { get; set; } = null;
}