namespace Shintio.Trader.Models;

public record StrategyOrder(bool IsShort, decimal Quantity, decimal Leverage);