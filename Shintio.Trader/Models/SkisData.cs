using Shintio.Trader.Enums;

namespace Shintio.Trader.Models;

public record SkisData(Trend Trend, int TrendSteps, decimal LastHigh, decimal LastLow);