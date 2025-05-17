using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Models.Strategies.Skis;
using Shintio.Trader.Services.Strategies;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Background;

public class StrategiesBenchmark4 : BackgroundService
{
	// public static readonly decimal BaseCommissionPercent = 0;
	public static readonly decimal BaseCommissionPercent = 0.0005m;

	public static readonly string Pair = CurrencyPair.ETH_USDT;

	// public static readonly DateTime StartTime = new(2024, 11, 1);
	// public static readonly DateTime StartTime = new(2024, 05, 1);
	// public static readonly DateTime EndTime = new(2025, 05, 1);

	// private static readonly decimal InitialBalance = 10_000;

	public static readonly int DaySteps = (int)TimeSpan.FromHours(24).TotalSeconds;
	public static readonly int DaysPerSegment = 3;
	public static readonly int SegmentSteps = (int)TimeSpan.FromDays(DaysPerSegment).TotalSeconds;

	private readonly ILogger<StrategiesBenchmark4> _logger;

	private readonly StrategiesRunner _runner;

	public StrategiesBenchmark4(ILogger<StrategiesBenchmark4> logger, StrategiesRunner runner)
	{
		_logger = logger;

		_runner = runner;
	}

	private bool ValidateBalance(TradeAccount account, decimal balanceToRemove, decimal currentPrice)
	{
		return StrategyHelper.ValidateBalanceLiquidation(account, balanceToRemove, currentPrice);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var start = new DateTime(2025, 1, 1);
		var end = new DateTime(2025, 5, 16);

		var managers = new List<SkisSandboxStrategyManager>();

		for (var dStart = 0.001m; dStart <= 0.2m; dStart += 0.001m)
		{
			for (var dEnd = 0.001m; dEnd <= 0.2m; dEnd += 0.001m)
			{
				var strategy = new SkisSandboxStrategyManager(
					500,
					BaseCommissionPercent,
					new SkisData(Trend.Flat, 0, 0, decimal.MaxValue),
					new SkisOptions(5m, 10m, dStart, dEnd),
					1
				);
				managers.Add(strategy);
			}
		}

		var totalSteps = (int)((end - start).TotalMinutes / TimeSpan.FromHours(1).TotalMinutes);
		var totalCollects = (int)totalSteps / 24;

		var stopwatch = Stopwatch.StartNew();
		var result = _runner.RunParallel<decimal[], SkisSandboxStrategyManager>(
			Pair,
			start,
			end,
			TimeSpan.FromHours(1),
			24,
			managers,
			(manager, currentPrice, step) =>
			{
				// _logger.LogInformation($"[{Pair}] Collecting {(step / 24) + 1}/{totalCollects}...");
				return [manager.Account.CalculateTotalCurrentQuantity(currentPrice), manager.Account.Statistics.Shorts.TotalCount, manager.Account.Statistics.Longs.TotalCount];
				// return ;
			}
		);
		stopwatch.Stop();
		_logger.LogInformation(stopwatch.Elapsed.ToString());
		var best = result.MaxBy(p => p.Value.Average(d => d[0]));
		_logger.LogInformation(best.Key.Options.ToString());
		_logger.LogInformation(best.Value.Last().ToString());

		await File.WriteAllTextAsync(
			$"benchmark.json",
			JsonSerializer.Serialize(new
			{
				StartTime = start.ToString("yyyy-MM-dd"),
				EndTime = end.ToString("yyyy-MM-dd"),
				SaveStepSeconds = DaySteps,
				Pair = Pair,
				Values = best.Value.Select(d => d[0]),
				Shorts = best.Value.Select(d => d[1]),
				Longs = best.Value.Select(d => d[2]),
				// Starts = starts,
				// Ends = ends,
				// DeltaBalances = deltaBalances,
				// Prices = prices,
				// WinratesCount = winratesCount,
				// WinratesSum = winratesSum,
			}),
			cancellationToken: stoppingToken
		);

		_logger.LogInformation("Benchmark completed");

		// return;
		// foreach (var initialBalance in new[] { 500m })
		// 	// foreach (var initialBalance in new[] { 500, 1_000, 2_000, 10_000 })
		// {
		// 	var account = new TradeAccount(initialBalance, BaseCommissionPercent, ValidateBalance);
		// 	var days = TimeSpan.FromDays(2);
		// 	var start = new DateTime(2025, 5, 2);
		// 	// var end = start + TimeSpan.FromDays(30 * 4);
		// 	var end = new DateTime(2025, 5, 16);
		// 	
		// 	var quantity = 5m;
		// 	var leverage = 10m;
		// 	
		// 	var trend = SkisStrategy.Trend.Flat;
		// 	var trendSteps = 0;
		// 	var lastHigh = 0m;
		// 	var lastLow = decimal.MaxValue;
		//
		// 	var oldTrend = SkisStrategy.Trend.Flat;
		// 	var oldTrendSteps = 0;
		// 	var oldLastHigh = 0m;
		// 	var oldLastLow = decimal.MaxValue;
		// 	var oldBalance = initialBalance;
		// 	var oldOrders = Array.Empty<Order>();
		// 	
		// 	var results = new List<decimal>();
		// 	var starts = new List<decimal>();
		// 	var ends = new List<decimal>();
		// 	var winratesCount = new List<decimal>();
		// 	var winratesSum = new List<decimal>();
		// 	var prices = new List<decimal[]>();
		// 	var deltaBalances = new List<decimal>();
		//
		// 	var step = 0;
		// 	var segment = 0;
		// 	var segments = (int)Math.Ceiling((end - start) / days);
		// 	var currentStart = start;
		// 	for (; currentStart < end; currentStart += days)
		// 	{
		// 		_logger.LogInformation($"{initialBalance} - {segment}/{segments}");
		//
		// 		var currentEnd = currentStart + days;
		//
		// 		var (startDelta, endDelta, deltaResults) = await DeltaFinder.Find(
		// 			_sandbox,
		// 			initialBalance,
		// 			BaseCommissionPercent,
		// 			Pair,
		// 			quantity,
		// 			leverage,
		// 			// currentStart,
		// 			// currentEnd,
		// 			currentStart - days,
		// 			currentStart,
		// 			oldTrend,
		// 			oldTrendSteps,
		// 			oldLastHigh,
		// 			oldLastLow,
		// 			oldBalance,
		// 			oldOrders
		// 		);
		//
		// 		oldTrend = trend;
		// 		oldTrendSteps = trendSteps;
		// 		oldLastHigh = lastHigh;
		// 		oldLastLow = lastLow;
		// 		oldBalance = account.Balance;
		// 		oldOrders = account.Orders.Select(o => o with { }).ToArray();
		// 		
		// 		if (start != currentStart)
		// 		{
		// 			deltaBalances.AddRange(deltaResults);
		// 		}
		// 		
		// 		// var startDelta = 0.038m;
		// 		// var endDelta = 0.030m;
		//
		// 		var items = await FetchKlineHistory(Pair, currentStart, currentEnd);
		//
		// 		var strategy = new SkisStrategy(
		// 			quantity,
		// 			leverage,
		// 			startDelta,
		// 			endDelta,
		// 			trend: trend,
		// 			trendSteps: trendSteps,
		// 			lastHigh: lastHigh,
		// 			lastLow: lastLow
		// 		);
		//
		// 		foreach (var item in items)
		// 		{
		// 			if (step % strategy.RunStep == 0)
		// 			{
		// 				strategy.Run(account, item.OpenPrice, [], step);
		//
		// 				trend = strategy._trend;
		// 				trendSteps = strategy._trendSteps;
		// 				lastHigh = strategy._lastHigh;
		// 				lastLow = strategy._lastLow;
		// 			}
		//
		// 			step++;
		//
		// 			if (step % DaySteps == 0)
		// 			{
		// 				results.Add(account.CalculateTotalCurrentQuantity(item.OpenPrice));
		// 				starts.Add(startDelta);
		// 				ends.Add(endDelta);
		// 				winratesCount.Add(0);
		// 				winratesSum.Add(0);
		// 				// winratesCount.Add(account.Statistics.WinrateCount);
		// 				// winratesSum.Add(account.Statistics.WinrateSum);
		// 			}
		// 		}
		//
		// 		prices.AddRange(items.Chunk(DaySteps).Select(chunk =>
		// 		{
		// 			var open = chunk.First().OpenPrice;
		// 			var high = chunk.Max(i => i.HighPrice);
		// 			var low = chunk.Min(i => i.LowPrice);
		// 			var close = chunk.Last().ClosePrice;
		//
		// 			return new[] { open, high, low, close };
		// 		}));
		//
		// 		segment++;
		// 	}
		//
		// 	var (_, _, lastDeltaResults) = await DeltaFinder.Find(
		// 		_sandbox,
		// 		initialBalance,
		// 		BaseCommissionPercent,
		// 		Pair,
		// 		quantity,
		// 		leverage,
		// 		// currentStart,
		// 		// currentEnd
		// 		currentStart - days,
		// 		currentStart,
		// 		oldTrend,
		// 		oldTrendSteps,
		// 		oldLastHigh,
		// 		oldLastLow,
		// 		oldBalance,
		// 		oldOrders
		// 	);
		// 	
		// 	if (start != currentStart)
		// 	{
		// 		deltaBalances.AddRange(lastDeltaResults);
		// 	}
		//
		// 	await File.WriteAllTextAsync(
		// 		$"benchmark.json",
		// 		JsonSerializer.Serialize(new
		// 		{
		// 			StartTime = start.ToString("yyyy-MM-dd"),
		// 			EndTime = (end + days).ToString("yyyy-MM-dd"),
		// 			SaveStepSeconds = DaySteps,
		// 			Pair = Pair,
		// 			Values = results,
		// 			Starts = starts,
		// 			Ends = ends,
		// 			DeltaBalances = deltaBalances,
		// 			Prices = prices,
		// 			WinratesCount = winratesCount,
		// 			WinratesSum = winratesSum,
		// 		}),
		// 		cancellationToken: stoppingToken
		// 	);
		// }
		//
		// _logger.LogInformation("Benchmark completed");
	}

	// private async Task<IReadOnlyCollection<KlineItem>> FetchKlineHistory(
	// 	string pair,
	// 	DateTime start,
	// 	DateTime end
	// )
	// {
	// 	var cache = new List<KlineItem>((int)(end - start).TotalSeconds);
	//
	// 	await foreach (var item in _sandbox.FetchKlineHistory(pair, start, end))
	// 	{
	// 		cache.Add(item);
	// 	}
	//
	// 	return cache;
	// }
}