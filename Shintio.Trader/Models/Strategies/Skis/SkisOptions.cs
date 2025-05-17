using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;

namespace Shintio.Trader.Models.Strategies.Skis;

public record SkisOptions(
	decimal Quantity,
	decimal Leverage,
	decimal StartDelta,
	decimal StopDelta,
	QuantityMultiplier QuantityMultiplier = QuantityMultiplier.HighQuad
) : IStrategyOptions;