namespace Shintio.Trader.Models;

public record SkisOrder(bool IsShort, decimal Quantity, decimal Leverage);