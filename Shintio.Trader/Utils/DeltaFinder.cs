using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Services;
using Shintio.Trader.Services.Strategies;

namespace Shintio.Trader.Utils;

public class DeltaFinder
{
	private static readonly decimal StartDeltaMin = 0.01m;
	private static readonly decimal StartDeltaMax = 0.05m;
	private static readonly decimal StartDeltaStep = 0.005m;

	private static readonly decimal EndDeltaMin = 0.01m;
	private static readonly decimal EndDeltaMax = 0.05m;
	private static readonly decimal EndDeltaStep = 0.005m;

	public static readonly int DaySteps = (int)TimeSpan.FromHours(24).TotalSeconds;

	public static async Task<(decimal start, decimal end, List<decimal> results)> Find(
		SandboxService sandbox,
		decimal initialBalance,
		decimal commission,
		string pair,
		decimal quantity,
		decimal leverage,
		DateTime start,
		DateTime end,
		SkisStrategy.Trend trend,
		int trendSteps,
		decimal lastHigh,
		decimal lastLow,
		decimal balance,
		Order[] orders
	)
	{
		var strategies = new List<(TradeAccount account, SkisStrategy strategy, List<decimal> results)>();
		for (var endDelta = EndDeltaMin; endDelta <= EndDeltaMax; endDelta += EndDeltaStep)
		{
			for (var startDelta = StartDeltaMin; startDelta < endDelta / 2; startDelta += StartDeltaStep)
			{
				var account = new TradeAccount(initialBalance, commission, ValidateBalance);
				account.Balance = balance;
				account.Orders = orders.Select(o => o with { }).ToList();

				var strategy = new SkisStrategy(
					quantity,
					leverage,
					startDelta,
					endDelta,
					QuantityMultiplier.HighQuad,
					trend: trend,
					trendSteps: trendSteps,
					lastHigh: lastHigh,
					lastLow: lastLow
				);

				strategies.Add((account, strategy, new List<decimal>()));
			}
		}

		var step = 0;
		var currentPrice = 0m;
		await foreach (var item in sandbox.FetchKlineHistory(pair, start, end))
		{
			currentPrice = item.OpenPrice;

			foreach (var (account, strategy, results) in strategies)
			{
				if (step % strategy.RunStep == 0)
				{
					strategy.Run(account, currentPrice, [], step);
				}

				if (step % DaySteps == 0)
				{
					results.Add(account.CalculateTotalCurrentQuantity(currentPrice));
				}
			}

			step++;

			if (step % DaySteps == 0)
			{
				Console.WriteLine($"{step / DaySteps}/{(end - start).TotalDays}");
			}
		}
		
		if (step % DaySteps != 0)
		{
			foreach (var (account, strategy, results) in strategies)
			{
				results.Add(account.CalculateTotalCurrentQuantity(currentPrice));
			}
		}

		var best = strategies.MaxBy(t => t.results.Last());

		return (best.strategy._trendStartDelta, best.strategy._trendEndDelta, best.results);
	}

	private static bool ValidateBalance(TradeAccount tradeAccount, decimal balanceToRemove, decimal currentPrice)
	{
		return StrategyHelper.ValidateBalanceLiquidation(tradeAccount, balanceToRemove, currentPrice);
	}
}