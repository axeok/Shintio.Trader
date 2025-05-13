using Shintio.Trader.Enums;

namespace Shintio.Trader.Models;

public record SkisOptions(
	decimal Quantity,
	decimal Leverage,
	decimal StartDelta,
	decimal StopDelta,
	QuantityMultiplier QuantityMultiplier = QuantityMultiplier.HighQuad
);