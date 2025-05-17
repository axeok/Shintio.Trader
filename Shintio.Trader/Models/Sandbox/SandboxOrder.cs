using Shintio.Trader.Utils;

namespace Shintio.Trader.Models.Sandbox;

public record SandboxOrder(
	bool IsShort,
	decimal Price,
	decimal Quantity,
	decimal Leverage,
	decimal? TakeProfitPrice,
	decimal? StopLossPrice
)
{
	public Guid Id { get; init; } = Guid.NewGuid();
	
	public decimal TotalQuantity => Quantity * Leverage;

	public decimal CalculateProfitPercent(decimal currentPrice)
	{
		return MoneyHelper.GetPercent(currentPrice, Price, Leverage) * (IsShort ? -1 : 1);
	}

	public decimal CalculateProfitQuantity(decimal currentPrice)
	{
		return Quantity * CalculateProfitPercent(currentPrice);
	}

	public decimal CalculateCurrentQuantity(decimal currentPrice)
	{
		return Quantity + CalculateProfitQuantity(currentPrice);
	}

	public bool NeedToClosePercent(decimal currentPrice, decimal? takeProfitPercent, decimal? stopLossPercent)
	{
		var percent = CalculateProfitPercent(currentPrice);

		return (takeProfitPercent != null && percent >= takeProfitPercent) ||
		       (stopLossPercent != null && percent <= -stopLossPercent);
	}

	public bool NeedToClosePrice(decimal currentPrice)
	{
		return IsShort
			? (TakeProfitPrice != null && currentPrice <= TakeProfitPrice) ||
			  (StopLossPrice != null && currentPrice >= StopLossPrice)
			: TakeProfitPrice != null && currentPrice >= TakeProfitPrice ||
			  (StopLossPrice != null && currentPrice <= StopLossPrice);
	}

	public bool NeedToOpenPrice(decimal currentPrice)
	{
		return IsShort
			? currentPrice >= Price
			: currentPrice <= Price;
	}
};