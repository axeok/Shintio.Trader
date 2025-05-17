namespace Shintio.Trader.Models;

public record StrategyResult<TData>(
	TData Data,
	IReadOnlyCollection<StrategyOrder> OrdersToOpen,
	bool CloseLongs,
	bool CloseShorts
)
{
}