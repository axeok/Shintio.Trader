using Shintio.Trader.Models;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Models.Strategies.Skis;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Interfaces;

public interface IStrategy<TData, TOptions, TResult>
	where TData : IStrategyData
	where TOptions : IStrategyOptions
	where TResult : StrategyResult<TData>
{
	public TResult Run(decimal currentPrice, decimal balance, TData data, TOptions options);
}