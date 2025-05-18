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

public class StrategiesBenchmark5 : BackgroundService
{
	// public static readonly decimal BaseCommissionPercent = 0;
	public static readonly decimal BaseCommissionPercent = 0.0005m;

	public static readonly string[] Pairs =
	[
		// CurrencyPair.ETH_USDT,
		// CurrencyPair.SOL_USDT,
		// CurrencyPair.BTC_USDT,
		// CurrencyPair.XRP_USDT,
		CurrencyPair.DOGE_USDT,
		// CurrencyPair.NEAR_USDT,
		// CurrencyPair.LISTA_USDT,
		// CurrencyPair.OM_USDT,
		// CurrencyPair.BNB_USDT,
		// CurrencyPair.ADA_USDT,
		// CurrencyPair.AVAX_USDT,
		// CurrencyPair.TRX_USDT,
		// CurrencyPair.LTC_USDT,
		// CurrencyPair.LINK_USDT,
		// CurrencyPair.NEAR_USDT,
		// CurrencyPair.BCH_USDT,
		// CurrencyPair.FIL_USDT,
		// CurrencyPair.PEPE_USDT,
		// CurrencyPair.WIF_USDT,
	];

	// public static readonly DateTime StartTime = new(2024, 11, 1);
	// public static readonly DateTime StartTime = new(2024, 05, 1);
	// public static readonly DateTime EndTime = new(2025, 05, 1);

	// private static readonly decimal InitialBalance = 10_000;

	public static readonly int DaySteps = (int)TimeSpan.FromHours(24).TotalSeconds;
	public static readonly int DaysPerSegment = 3;
	public static readonly int SegmentSteps = (int)TimeSpan.FromDays(DaysPerSegment).TotalSeconds;

	private readonly ILogger<StrategiesBenchmark5> _logger;

	private readonly StrategiesRunner _runner;

	public StrategiesBenchmark5(ILogger<StrategiesBenchmark5> logger, StrategiesRunner runner)
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
		var end = new DateTime(2025, 5, 17);

		var bests = new Dictionary<string, IReadOnlyCollection<decimal>>();

		foreach (var pair in Pairs)
		{
			// for (var profitMultiplier = 0.01m; profitMultiplier <= 1m; profitMultiplier += 0.05m)
			{
				var stopwatch = Stopwatch.StartNew();

				// for (var percent = 0.01m; percent <= 0.1m; percent += 0.02m)
				{
					var managers = new List<SkisSandboxStrategyManager>();
					for (var dStart = 0.001m; dStart <= 0.1m; dStart += 0.005m)
					{
						for (var dEnd = 0.001m; dEnd <= 0.1m; dEnd += 0.005m)
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

					var result = _runner.RunParallel<decimal, SkisSandboxStrategyManager>(
						pair,
						start,
						end,
						TimeSpan.FromHours(1),
						24,
						managers,
						(manager, currentPrice, step) =>
						{
							// _logger.LogInformation($"[{Pair}] Collecting {(step / 24) + 1}/{totalCollects}...");
							return manager.Account.Balance;
							// return manager.Account.LastCalculatedBalance;
							// return
							// [
							// 	manager.Account.CalculateTotalCurrentQuantity(currentPrice),
							// 	// manager.Account.Statistics.Shorts.TotalCount, manager.Account.Statistics.Longs.TotalCount
							// ];
							// return ;
						}
					);

					foreach (var (manager, values) in result)
					{
						if (values.Last() <= values.First())
						{
							continue;
						}

						bests[$"{pair} : {manager.Options}"] = values;
					}

					// var best = result.MaxBy(p => p.Value.Last());
					//
					// // var best = result.MaxBy(p => p.Value.Average(d => d[0]));
					// _logger.LogInformation(best.Key.Options.ToString());
					// _logger.LogInformation(best.Value.Last().ToString());
					//
					// bests[$"{pair} : {best.Key.Options}"] = best.Value;

					// var bestIndex = FindMostStableGrowthIndex(result.Values.Select(d => d.ToArray()).ToList(), 10);
					// if (bestIndex >= 0)
					// {
					// 	var best = result.ElementAt(bestIndex);
					//
					// 	// var best = result.MaxBy(p => p.Value.Average(d => d[0]));
					// 	_logger.LogInformation(best.Key.Options.ToString());
					// 	_logger.LogInformation(best.Value.Last().ToString());
					//
					// 	bests[$"{pair} : {best.Key.Options}"] = best.Value;
					// }
				}

				stopwatch.Stop();
				_logger.LogInformation($"[{pair}] Elapsed: {stopwatch.Elapsed}");
			}
		}

		await File.WriteAllTextAsync(
			$"benchmark.json",
			JsonSerializer.Serialize(new
			{
				StartTime = start.ToString("yyyy-MM-dd"),
				EndTime = end.ToString("yyyy-MM-dd"),
				SaveStepSeconds = DaySteps,
				Pairs = Pairs,
				Values = bests,
				// Shorts = bests.Value.Select(d => d[1]),
				// Longs = bests.Value.Select(d => d[2]),
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
	}

	public static int FindMostStableGrowthIndex(List<decimal[]> seriesList, double tolerancePercent = 0.5)
	{
		int bestIndex = -1;
		double bestScore = double.NegativeInfinity;

		for (int i = 0; i < seriesList.Count; i++)
		{
			var series = seriesList[i];
			// if (!IsMostlyMonotonicIncreasing(series, tolerancePercent)) continue;
			if (!IsMostlyMonotonicIncreasingByMax(series, tolerancePercent)) continue;

			double[] x = Enumerable.Range(0, series.Length).Select(v => (double)v).ToArray();
			double[] y = series.Select(v => (double)v).ToArray();

			(double slope, double r2) = LinearRegression(x, y);

			if (slope > 0)
			{
				double score = r2 * slope;
				if (score > bestScore)
				{
					bestScore = score;
					bestIndex = i;
				}
			}
		}

		return bestIndex;
	}

	private static bool IsMostlyMonotonicIncreasing(decimal[] series, double tolerancePercent)
	{
		double tolerance = 1 - (tolerancePercent / 100.0);
		for (int i = 1; i < series.Length; i++)
		{
			if ((double)series[i] < (double)series[i - 1] * tolerance)
				return false;
		}

		return true;
	}

	private static (double slope, double r2) LinearRegression(double[] x, double[] y)
	{
		int n = x.Length;
		double avgX = x.Average();
		double avgY = y.Average();

		double numerator = 0, denominator = 0, totalVariation = 0, explainedVariation = 0;

		for (int i = 0; i < n; i++)
		{
			numerator += (x[i] - avgX) * (y[i] - avgY);
			denominator += (x[i] - avgX) * (x[i] - avgX);
		}

		double slope = numerator / denominator;
		double intercept = avgY - slope * avgX;

		for (int i = 0; i < n; i++)
		{
			double predictedY = slope * x[i] + intercept;
			explainedVariation += (predictedY - avgY) * (predictedY - avgY);
			totalVariation += (y[i] - avgY) * (y[i] - avgY);
		}

		double r2 = totalVariation == 0 ? 1 : explainedVariation / totalVariation;
		return (slope, r2);
	}

	private static bool IsMostlyMonotonicIncreasingByMax(decimal[] series, double tolerancePercent)
	{
		double tolerance = 1 - (tolerancePercent / 100.0);
		decimal maxSoFar = series[0];

		for (int i = 1; i < series.Length; i++)
		{
			if ((double)series[i] < (double)maxSoFar * tolerance)
				return false;

			if (series[i] > maxSoFar)
				maxSoFar = series[i];
		}

		return true;
	}
}