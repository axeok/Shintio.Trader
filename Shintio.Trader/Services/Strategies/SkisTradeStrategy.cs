using Shintio.Trader.Enums;
using Shintio.Trader.Models;

namespace Shintio.Trader.Services.Strategies;

public static class SkisTradeStrategy
{
	public static (SkisData data, IReadOnlyCollection<SkisOrder> orders, bool closeLongs, bool closeShorts) Run(
		decimal currentPrice,
		decimal balance,
		SkisData data,
		SkisOptions options
	)
	{
		var closeLongs = false;
		var closeShorts = false;

		var trend = data.Trend;
		var trendSteps = data.TrendSteps;
		var lastHigh = Math.Max(data.LastHigh, currentPrice);
		var lastLow = Math.Min(data.LastLow, currentPrice);

		var deltaHigh = (currentPrice - lastHigh) / lastHigh * -1;
		var deltaLow = (currentPrice - lastLow) / lastLow;

		switch (trend)
		{
			case Trend.Up:
				if (deltaHigh >= options.StopDelta)
				{
					lastLow = currentPrice;
					trend = Trend.Flat;
					closeLongs = true;
					trendSteps = 0;
				}

				break;
			case Trend.Down:
				if (deltaLow >= options.StopDelta)
				{
					lastHigh = currentPrice;
					trend = Trend.Flat;
					closeShorts = true;
					trendSteps = 0;
				}

				break;
		}

		if (trend == Trend.Flat)
		{
			if (deltaHigh >= options.StartDelta)
			{
				trend = Trend.Down;
			}

			if (deltaLow >= options.StartDelta)
			{
				trend = Trend.Up;
			}
		}

		trendSteps++;

		var quantity = options.Quantity;
		quantity = options.QuantityMultiplier switch
		{
			QuantityMultiplier.None => quantity,
			QuantityMultiplier.Low => quantity - (trendSteps / 10m),
			QuantityMultiplier.LowQuad => quantity - ((trendSteps / 10m) * (trendSteps / 10m)),
			QuantityMultiplier.High => quantity + (trendSteps / 10m),
			QuantityMultiplier.HighQuad => quantity + ((trendSteps / 10m) * (trendSteps / 10m)),
		};

		var leverage = Math.Clamp(Math.Floor(options.Leverage + (balance / 100)), 10, 75);

		var orders = new List<SkisOrder>();

		switch (trend)
		{
			case Trend.Up:
				if (quantity >= 1)
				{
					orders.Add(new SkisOrder(false, quantity, leverage));
				}

				break;
			case Trend.Down:
				if (quantity >= 1)
				{
					orders.Add(new SkisOrder(true, quantity, leverage));
				}

				break;
			case Trend.Flat:
				break;
		}

		return (
			new SkisData(trend, trendSteps, lastHigh, lastLow),
			orders,
			closeLongs,
			closeShorts
		);
	}
}