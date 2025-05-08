using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Services.Strategies;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Services.Background;

public class StrategiesBenchmark : BackgroundService
{
	public static readonly decimal BaseCommissionPercent = 0.0005m;

	public static readonly string[] Pairs =
	[
		// CurrencyPair.SOL_USDT,
		CurrencyPair.ETH_USDT,
		// CurrencyPair.BTC_USDT,
		// CurrencyPair.XRP_USDT,
		// CurrencyPair.DOGE_USDT,
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
	];
	public static readonly int SaveStep = (int)TimeSpan.FromHours(24).TotalSeconds;

	private static readonly int ChunkStep = SaveStep;

	private readonly ILogger<StrategiesBenchmark> _logger;

	private readonly Dictionary<string, Dictionary<string, IStrategy>> _strategies;
	private readonly SandboxService _sandbox;

	public StrategiesBenchmark(ILogger<StrategiesBenchmark> logger, SandboxService sandbox)
	{
		_logger = logger;

		_strategies = new Dictionary<string, Dictionary<string, IStrategy>>();

		foreach (var pair in Pairs)
		{
			_strategies[pair] = new Dictionary<string, IStrategy>();
			
			// foreach (var quantity in new[] { 1 })
				foreach (var quantity in new[] { 1 })
			{
				// foreach (var leverage in new[] { 10 })
					foreach (var leverage in new[] { 10 })
				{
					// foreach (var maxDelta in new[] { 0.0295m })
						foreach (var maxDelta in new[] { 0.015m, 0.01m, 0.02m, 0.0295m, 0.03m, 0.04m })
					{
						// foreach (var minDelta in new[] { 0.05m })
							// foreach (var maxDelta in new[] { 0.015m, 0.01m, 0.02m, 0.03m })
						{
							foreach (var initialBalance in new[] { 10_000 })
								// foreach (var initialBalance in new[] { 500, 2_000, 10_000 })
							{
								// foreach (var skipSteps in new[] { 365 })
								{
									var name =
										$"{pair} Q{quantity:F0}L{leverage:F0}D{maxDelta * 100:F2}D{initialBalance:F0}";

									_strategies[pair][name] =
										new SkisStrategy(
											quantity,
											leverage,
											maxDelta,
											initialBalance
										);
								}
							}
						}
					}
				}
			}
		}
		
		_sandbox = sandbox;
	}

	private record StrategyData(TradeAccount Account, Queue<KlineItem> History, int MaxHistoryCount)
	{
		public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;
	};

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var results = new Dictionary<IStrategy, List<decimal>>(); 
		var shorts = new Dictionary<IStrategy, List<int>>(); 
		var longs = new Dictionary<IStrategy, List<int>>(); 
		
		var strategiesData = new Dictionary<IStrategy, StrategyData>();

		foreach (var (pair, strategies) in _strategies)
		{
			foreach (var strategy in strategies.Values)
			{
				results[strategy] = new List<decimal> { };
				shorts[strategy] = new List<int> { };
				longs[strategy] = new List<int> { };
				strategiesData[strategy] = new StrategyData(
					new TradeAccount(strategy.InitialBalance, BaseCommissionPercent, strategy.ValidateBalance),
					new Queue<KlineItem>(strategy.MaxHistoryCount),
					strategy.MaxHistoryCount
				);
			}
		}

		var prices = new Dictionary<string, List<decimal[]>>();
		foreach (var pair in Pairs)
		{
			prices[pair] = new List<decimal[]>();
			
			var totalChunks = ((int)(SandboxService.EndTime - SandboxService.StartTime).TotalSeconds) / ChunkStep;
			var chunkIndex = 0;
			await foreach (var chunk in FetchKlineHistoryChunks(pair))
			{
				var startTime = DateTime.UtcNow;
				_logger.LogInformation($"[{pair}] Running {chunkIndex + 1}/{totalChunks} chunk...");

				if (true)
				{
					Parallel.ForEach(_strategies[pair].Values, strategy =>
							// foreach (var strategy in _strategies[pair].Values)
						{
							var data = strategiesData[strategy];
							var account = data.Account;
							var history = data.History;
							var maxHistoryCount = data.MaxHistoryCount;

							var stepIndex = chunkIndex * ChunkStep;
							foreach (var item in chunk)
							{
								history.Enqueue(item);
								if (history.Count > maxHistoryCount)
								{
									history.Dequeue();
								}

								var currentPrice = item.OpenPrice;

								if (strategy.AutoProcessMarket)
								{
									account.ProcessMarket(currentPrice);
								}

								if (stepIndex % strategy.RunStep == 0)
								{
									strategy.Run(account, currentPrice, history, stepIndex);
								}

								stepIndex++;
							}

							var currentBalance = account.CalculateTotalCurrentQuantity(chunk.Last().OpenPrice);

							results[strategy].Add(currentBalance);
							shorts[strategy].Add(account.Shorts.Count());
							longs[strategy].Add(account.Longs.Count());

							data.Elapsed = DateTime.UtcNow - startTime;
						}
					);
				}

				var open = chunk.First().OpenPrice;
				var high = chunk.Max(i => i.HighPrice);
				var low = chunk.Min(i => i.LowPrice);
				var close = chunk.Last().ClosePrice;

				prices[pair].Add([open, high, low, close]);
				chunkIndex++;

				if (chunkIndex % 10 == 0)
				{
					await File.WriteAllTextAsync(
						"benchmark.json",
						JsonSerializer.Serialize(new
						{
							StartTime = SandboxService.StartTime.ToString("yyyy-MM-dd"),
							EndTime =
								(SandboxService.StartTime + (TimeSpan.FromSeconds(ChunkStep) * chunkIndex)).ToString("yyyy-MM-dd"),
							SaveStepSeconds = SaveStep,
							Strategies = _strategies.SelectMany(pair => pair.Value.Select(p => new
							{
								Name = p.Key,
								Pair = pair.Key,
								Values = results[p.Value],
								Shorts = shorts[p.Value],
								Longs = shorts[p.Value],
							})),
							Prices = prices,
						}),
						cancellationToken: stoppingToken
					);
				}
			}
		}

		await File.WriteAllTextAsync(
			"benchmark.json",
			JsonSerializer.Serialize(new
			{
				StartTime = SandboxService.StartTime.ToString("yyyy-MM-dd"),
				EndTime = SandboxService.EndTime.ToString("yyyy-MM-dd"),
				SaveStepSeconds = SaveStep,
				Strategies = _strategies.SelectMany(pair => pair.Value.Select(p => new
				{
					Name = p.Key,
					Pair = pair.Key,
					Values = results[p.Value],
					Shorts = shorts[p.Value],
					Longs = shorts[p.Value],
				})),
				Prices = prices,
			}),
			cancellationToken: stoppingToken
		);

		_logger.LogInformation("Benchmark completed");
	}
	
	private async IAsyncEnumerable<IReadOnlyCollection<KlineItem>> FetchKlineHistoryChunks(string pair)
	{
		var cache = new List<KlineItem>(ChunkStep);

		await foreach (var item in _sandbox.FetchKlineHistory(pair))
		{
			cache.Add(item);

			if (cache.Count >= ChunkStep)
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