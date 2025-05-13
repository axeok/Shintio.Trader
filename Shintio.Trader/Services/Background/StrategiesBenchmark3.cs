using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Services.Strategies;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Background;

public class StrategiesBenchmark3 : BackgroundService
{
	// public static readonly decimal BaseCommissionPercent = 0;
	public static readonly decimal BaseCommissionPercent = 0.001m;

	public static readonly string Pair = CurrencyPair.DOGE_USDT;
	// public static readonly DateTime StartTime = new(2024, 11, 1);
	public static readonly DateTime StartTime = new(2024, 05, 1);
	public static readonly DateTime EndTime = new(2025, 05, 1);

	private static readonly decimal StartDeltaMin = 0.01m;
	private static readonly decimal StartDeltaMax = 0.05m;
	private static readonly decimal StartDeltaStep = 0.001m;

	private static readonly decimal EndDeltaMin = 0.01m;
	private static readonly decimal EndDeltaMax = 0.05m;
	private static readonly decimal EndDeltaStep = 0.001m;

	// private static readonly decimal InitialBalance = 10_000;

	public static readonly int DaySteps = (int)TimeSpan.FromHours(24).TotalSeconds;
	public static readonly int DaysPerSegment = 3;
	public static readonly int SegmentSteps = (int)TimeSpan.FromDays(DaysPerSegment).TotalSeconds;

	private readonly ILogger<StrategiesBenchmark3> _logger;

	// private readonly TradeAccount _account;
	private readonly SandboxService _sandbox;

	public StrategiesBenchmark3(ILogger<StrategiesBenchmark3> logger, SandboxService sandbox)
	{
		_logger = logger;

		// _account = new TradeAccount(InitialBalance, BaseCommissionPercent, ValidateBalance);

		_sandbox = sandbox;
	}

	private bool ValidateBalance(TradeAccount account, decimal balanceToRemove, decimal currentPrice)
	{
		return StrategyHelper.ValidateBalanceValue(account, balanceToRemove);
		// return StrategyHelper.ValidateBalanceLiquidation(account, balanceToRemove, currentPrice);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		foreach (var initialBalance in new[] { 500, 1_000, 2_000, 10_000 })
		{
			var results = new List<decimal>();
			var starts = new List<decimal>();
			var ends = new List<decimal>();
			var prices = new List<decimal[]>();

			var totalDays = (int)(EndTime - StartTime).TotalDays;
			var segment = 0;
			await foreach (var items in FetchKlineHistoryChunks(Pair))
			{
				var monthResults =
					new List<(decimal Start, decimal End, List<decimal> Balances)>(
						(int)(StartDeltaStep * EndDeltaStep));


				for (var startDelta = StartDeltaMin; startDelta <= StartDeltaMax; startDelta += StartDeltaStep)
				{
					for (var endDelta = EndDeltaMin; endDelta <= EndDeltaMax; endDelta += EndDeltaStep)
					{
						_logger.LogInformation(
							$"{initialBalance} - {segment}/{totalDays / DaysPerSegment} - {startDelta}/{StartDeltaMax} - {endDelta}/{EndDeltaMax}");

						var step = 0;
						var strategy = new SkisStrategy(
							10,
							10,
							startDelta,
							endDelta,
							QuantityMultiplier.HighQuad
						);
						var account = new TradeAccount(initialBalance, BaseCommissionPercent, ValidateBalance);
						var balances = new List<decimal>();

						foreach (var item in items)
						{
							var currentPrice = item.OpenPrice;

							if (step % strategy.RunStep == 0)
							{
								strategy.Run(account, currentPrice, [], step);
							}

							step++;

							if (step % DaySteps == 0)
							{
								// Console.WriteLine(account.Balance);
								// Console.WriteLine(account.CalculateOrdersCurrentQuantity(currentPrice));
								var currentBalance = account.CalculateTotalCurrentQuantity(currentPrice);
								// var currentBalance = account.ReservedBalance;

								balances.Add(currentBalance);
							}
						}

						monthResults.Add((startDelta, endDelta, balances));
					}
				}

				var best = monthResults.MaxBy(t => t.Balances.Last());

				results.AddRange(best.Balances);
				starts.AddRange(Enumerable.Range(0, best.Balances.Count).Select(_ => best.Start));
				ends.AddRange(Enumerable.Range(0, best.Balances.Count).Select(_ => best.End));

				prices.AddRange(items.Chunk(DaySteps).Select(chunk =>
				{
					var open = chunk.First().OpenPrice;
					var high = chunk.Max(i => i.HighPrice);
					var low = chunk.Min(i => i.LowPrice);
					var close = chunk.Last().ClosePrice;

					return new[] { open, high, low, close };
				}));

				segment++;
			}

			await File.WriteAllTextAsync(
				$"benchmarks/{initialBalance}.json",
				JsonSerializer.Serialize(new
				{
					StartTime = StartTime.ToString("yyyy-MM-dd"),
					EndTime = EndTime.ToString("yyyy-MM-dd"),
					SaveStepSeconds = DaySteps,
					Pair = Pair,
					Values = results,
					Starts = starts,
					Ends = ends,
					Prices = prices,
				}),
				cancellationToken: stoppingToken
			);
		}

		_logger.LogInformation("Benchmark completed");
	}
	
	private async IAsyncEnumerable<IReadOnlyCollection<KlineItem>> FetchKlineHistoryChunks(string pair)
	{
		var cache = new List<KlineItem>(SegmentSteps);

		await foreach (var item in _sandbox.FetchKlineHistory(pair, StartTime, EndTime))
		{
			cache.Add(item);

			if (cache.Count >= SegmentSteps)
			{
				yield return cache;
				
				cache = new List<KlineItem>();
			}
		}

		if (cache.Count > 0)
		{
			yield return cache;
		}
	}
}