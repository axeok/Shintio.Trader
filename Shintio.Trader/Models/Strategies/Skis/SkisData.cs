using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;

namespace Shintio.Trader.Models.Strategies.Skis;

public record SkisData(Trend Trend, int TrendSteps, decimal LastHigh, decimal LastLow) : IStrategyData;