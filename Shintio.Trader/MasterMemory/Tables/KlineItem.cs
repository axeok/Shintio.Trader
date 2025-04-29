using MasterMemory;
using MessagePack;

namespace Shintio.Trader.Tables;

[MemoryTable("kline_item"), MessagePackObject(true)]
public record KlineItem
{
    [PrimaryKey] public required DateTime OpenTime { get; init; }
    [PrimaryKey] public required DateTime CloseTime { get; init; }

    public required decimal OpenPrice { get; init; }
    public required decimal ClosePrice { get; init; }
    public required decimal LowPrice { get; init; }
    public required decimal HighPrice { get; init; }
    public required decimal Volume { get; init; }
    public required decimal BuyVolume { get; init; }
    public required decimal SellVolume { get; init; }
}