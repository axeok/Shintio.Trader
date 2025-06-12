using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Enums;
using Shintio.Trader.Models;
using Shintio.Trader.Models.Managers;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Models.Strategies.Skis;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Background;

public class StrategiesBenchmark6 : BackgroundService
{
	private record BenchmarkResult(
		decimal EthPrice,
		decimal Balance,
		decimal TotalBalance,
		decimal Orders,
		IReadOnlyDictionary<string, decimal> Pairs,
		IReadOnlyDictionary<string, decimal> TotalPnls
	);
	
	public static readonly decimal BaseCommissionPercent = 0.0005m;

	private readonly ILogger<StrategiesBenchmark6> _logger;

	private readonly StrategiesRunner _runner;

	public StrategiesBenchmark6(ILogger<StrategiesBenchmark6> logger, StrategiesRunner runner)
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
		var start = new DateTime(2025, 6, 1, 0, 0, 0);
		var end = new DateTime(2025, 6, 30);

		var collectStep = TimeSpan.FromHours(1);

		var stopwatch = Stopwatch.StartNew();

		var managers = new List<SkisMultipairSandboxStrategyManager>()
		{
			new(
				1511.54m,
				BaseCommissionPercent,
				new Dictionary<string, SkisPairInfo>()
				{
					[CurrencyPair.DOGE_USDT] = new SkisPairInfo(
						new SkisData(Trend.Flat, 0, 0, decimal.MaxValue),
						new SkisOptions(5m, 10m, 0.0010m, 0.0260m),
						25, 200, 0.15m, 0.85m
					),
					[CurrencyPair._1000PEPE_USDT] = new SkisPairInfo(
						new SkisData(Trend.Flat, 0, 0, decimal.MaxValue),
						new SkisOptions(5m, 10m, 0.0460m, 0.0360m),
						20, 70, 0.1m, 0.7m
					),
					[CurrencyPair.WIF_USDT] = new SkisPairInfo(
						new SkisData(Trend.Flat, 0, 0, decimal.MaxValue),
						new SkisOptions(5m, 10m, 0.0960m, 0.0210m),
						20, 85, 0.45m, 0.7m
					),
					[CurrencyPair.ETH_USDT] = new SkisPairInfo(
						new SkisData(Trend.Flat, 0, 0, decimal.MaxValue),
						new SkisOptions(5m, 10m, 0.0160m, 0.0960m),
						40, 200, 0.3m, 0.9m
					),
					// [CurrencyPair.NEAR_USDT] = new SkisPairInfo(
					// 	new SkisData(Trend.Flat, 0, 0, decimal.MaxValue),
					// 	new SkisOptions(5m, 10m, 0.0660m, 0.0910m),
					// 	35, 145, 0.6m, 0.8m
					// ),
					[CurrencyPair.PNUT_USDT] = new SkisPairInfo(
						new SkisData(Trend.Flat, 0, 0, decimal.MaxValue),
						new SkisOptions(5m, 10m, 0.0360m, 0.0210m),
						10000, 10000, 0.1m, 0.9m
					),
				},
				1
			)
		};

		var result = _runner.MultipairRunParallel<BenchmarkResult, SkisMultipairSandboxStrategyManager>(
			start,
			end,
			TimeSpan.FromHours(1),
			(int)collectStep.TotalHours,
			managers,
			(manager, pairs, step) =>
			{
				// _logger.LogInformation($"[{Pair}] Collecting {(step / 24) + 1}/{totalCollects}...");
				// return (decimal)manager.Data.TrendSteps;
				// return manager.Account.Balance;
				return new BenchmarkResult(
					pairs["ETHUSDT"],
					manager.Account.Balance,
					manager.Account.CalculateTotalCurrentQuantity(pairs),
					manager.Account.CalculateOrdersCurrentQuantity(pairs),
					pairs.ToDictionary(
						p => p.Key,
						// p => (decimal)(manager.Account.GetLongs(p.Key).Count() - manager.Account.GetShorts(p.Key).Count()))
						p => manager.Account.CalculateOrdersCurrentPnL(p.Key, p.Value)),
					pairs.ToDictionary(
						p => p.Key,
						p => manager.PairsInfo[p.Key].TotalPnl)
				);
				// return manager.Account.CalculateTotalCurrentQuantity(pairs);
				// return manager.Account.LastCalculatedBalance;
				// return
				// [
				// 	manager.Account.CalculateTotalCurrentQuantity(currentPrice),
				// 	// manager.Account.Statistics.Shorts.TotalCount, manager.Account.Statistics.Longs.TotalCount
				// ];
				// return ;
			}
		);

		var benchmarkResults = result.Values.First();

		stopwatch.Stop();
		_logger.LogInformation($"Elapsed: {stopwatch.Elapsed}");

		await File.WriteAllTextAsync(
			"benchmark.json",
			JsonSerializer.Serialize(new
			{
				StartTime = start.ToString("yyyy-MM-dd"),
				EndTime = end.ToString("yyyy-MM-dd"),
				SaveStepSeconds = (int)collectStep.TotalSeconds,
				Pairs = benchmarkResults.First().Pairs.Keys,
				Values = benchmarkResults.Select(r => new[]
				{
					r.Balance,
					r.TotalBalance,
					r.Orders,
				}.Concat(r.Pairs.Values)),
				TotalPnls = benchmarkResults.Select(r => r.TotalPnls.Select(p => p.Value)),
				Prices = benchmarkResults.Select(r => r.EthPrice),
			},
			new JsonSerializerOptions()
			{
				WriteIndented = true,
			}),
			cancellationToken: stoppingToken
		);

		_logger.LogInformation("Benchmark completed");
	}
}