// using System.Text.Json;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
// using Shintio.Trader.Enums;
// using Shintio.Trader.Interfaces;
// using Shintio.Trader.Models;
// using Shintio.Trader.Services.Strategies;
// using Shintio.Trader.Tables;
//
// namespace Shintio.Trader.Services.Background;
//
// public class StrategiesBenchmark2 : BackgroundService
// {
// 	public static readonly decimal BaseCommissionPercent = 0.0005m;
//
// 	public static readonly string[] Pairs =
// 	[
// 		// CurrencyPair.SOL_USDT,
// 		// CurrencyPair.ETH_USDT,
// 		// CurrencyPair.BTC_USDT,
// 		// CurrencyPair.XRP_USDT,
// 		// CurrencyPair.DOGE_USDT,
// 		// CurrencyPair.LISTA_USDT,
// 		CurrencyPair.OM_USDT,
// 		// CurrencyPair.BNB_USDT,
// 		// CurrencyPair.ADA_USDT,
// 		// CurrencyPair.AVAX_USDT,
// 		// CurrencyPair.TRX_USDT,
// 		// CurrencyPair.LTC_USDT,
// 		// CurrencyPair.LINK_USDT,
// 		// CurrencyPair.NEAR_USDT,
// 		// CurrencyPair.BCH_USDT,
// 		// CurrencyPair.FIL_USDT,
// 		// CurrencyPair.PEPE_USDT,
// 	];
//
// 	public static readonly int SaveStep = (int)TimeSpan.FromMinutes(60).TotalSeconds;
//
// 	private static readonly int ChunkStep = SaveStep;
// 	private static readonly int MaxChunks = 720;
//
// 	private readonly ILogger<StrategiesBenchmark> _logger;
//
// 	private readonly Dictionary<string, Dictionary<string, IStrategy>> _strategies;
// 	private readonly SandboxService _sandbox;
//
// 	public StrategiesBenchmark2(ILogger<StrategiesBenchmark> logger, SandboxService sandbox)
// 	{
// 		_logger = logger;
//
// 		_strategies = new Dictionary<string, Dictionary<string, IStrategy>>();
//
// 		foreach (var pair in Pairs)
// 		{
// 			_strategies[pair] = new Dictionary<string, IStrategy>();
//
// 			// foreach (var quantity in new[] { 1 })
// 			foreach (var quantity in new[] { 60 })
// 			{
// 				// foreach (var leverage in new[] { 10 })
// 				foreach (var leverage in new[] { 25 })
// 				{
// 					foreach (var maxDelta in new[] { 0.03m })
// 						// foreach (var maxDelta in new[] { 0.015m, 0.01m, 0.02m, 0.0295m, 0.03m, 0.04m })
// 					{
// 						var minDelta = 0.0311m;
// 						// foreach (var minDelta in new[] { 0.05m })
// 						// foreach (var maxDelta in new[] { 0.015m, 0.01m, 0.02m, 0.03m })
// 						{
// 							foreach (var initialBalance in new[] { 10_000 })
// 								// foreach (var initialBalance in new[] { 500, 2_000, 10_000 })
// 							{
// 								// foreach (var skipSteps in new[] { 365 })
// 								{
// 									var name =
// 										$"{pair} Q{quantity:F0}L{leverage:F0}D{maxDelta * 100:F2}D{initialBalance:F0}";
//
// 									_strategies[pair][name] =
// 										new SkisStrategy(
// 											quantity,
// 											leverage,
// 											maxDelta,
// 											minDelta,
// 											initialBalance,
// 											runStep: TimeSpan.FromSeconds(SaveStep)
// 										);
// 								}
// 							}
// 						}
// 					}
// 				}
// 			}
// 		}
//
// 		_sandbox = sandbox;
// 	}
//
// 	private record StrategyData(TradeAccount Account, Queue<KlineItem> History, int MaxHistoryCount)
// 	{
// 		public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;
// 	};
//
// 	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
// 	{
// 		var results = new Dictionary<IStrategy, List<decimal>>();
// 		var balances = new Dictionary<IStrategy, List<decimal>>();
// 		var shorts = new Dictionary<IStrategy, List<decimal>>();
// 		var longs = new Dictionary<IStrategy, List<decimal>>();
// 		var shortsCount = new Dictionary<IStrategy, List<decimal>>();
// 		var longsCount = new Dictionary<IStrategy, List<decimal>>();
//
// 		var strategiesData = new Dictionary<IStrategy, StrategyData>();
//
// 		foreach (var (pair, strategies) in _strategies)
// 		{
// 			foreach (var strategy in strategies.Values)
// 			{
// 				results[strategy] = new List<decimal> { };
// 				balances[strategy] = new List<decimal> { };
// 				shorts[strategy] = new List<decimal> { };
// 				longs[strategy] = new List<decimal> { };
// 				shortsCount[strategy] = new List<decimal> { };
// 				longsCount[strategy] = new List<decimal> { };
// 				strategiesData[strategy] = new StrategyData(
// 					new TradeAccount(strategy.InitialBalance, BaseCommissionPercent, strategy.ValidateBalance),
// 					new Queue<KlineItem>(strategy.MaxHistoryCount),
// 					strategy.MaxHistoryCount
// 				);
// 			}
// 		}
//
// 		var prices = new Dictionary<string, List<decimal[]>>();
// 		foreach (var pair in Pairs)
// 		{
// 			prices[pair] = new List<decimal[]>();
//
// 			var totalChunks = ((int)(SandboxService.EndTime - SandboxService.StartTime).TotalSeconds) / ChunkStep;
// 			var chunkIndex = 0;
// 			await foreach (var chunk in FetchKlineHistoryChunks(pair))
// 			{
// 				var startTime = DateTime.UtcNow;
// 				_logger.LogInformation($"[{pair}] Running {chunkIndex + 1}/{totalChunks} chunk...");
// 				if (chunkIndex + 1 > 720)
// 				{
// 					return;
// 				}
//
// 				if (true)
// 				{
// 					Parallel.ForEach(_strategies[pair].Values, strategy =>
// 							// foreach (var strategy in _strategies[pair].Values)
// 						{
// 							var data = strategiesData[strategy];
// 							var account = data.Account;
// 							var history = data.History;
// 							var maxHistoryCount = data.MaxHistoryCount;
//
// 							var stepIndex = chunkIndex * ChunkStep;
// 							foreach (var item in chunk)
// 							{
// 								history.Enqueue(item);
// 								if (history.Count > maxHistoryCount)
// 								{
// 									history.Dequeue();
// 								}
//
// 								var currentPrice = item.OpenPrice;
//
// 								if (strategy.AutoProcessMarket)
// 								{
// 									account.ProcessMarket(currentPrice);
// 								}
//
// 								if (stepIndex % strategy.RunStep == 0)
// 								{
// 									strategy.Run(account, currentPrice, history, stepIndex);
// 								}
//
// 								stepIndex++;
// 							}
//
// 							var price = chunk.Last().OpenPrice;
//
// 							results[strategy].Add(account.CalculateTotalCurrentQuantity(price));
// 							balances[strategy].Add(account.Balance);
// 							shorts[strategy].Add(account.Shorts.Sum(o => o.CalculateCurrentQuantity(price)));
// 							longs[strategy].Add(account.Longs.Sum(o => o.CalculateCurrentQuantity(price)));
// 							shortsCount[strategy].Add(account.Shorts.Count());
// 							longsCount[strategy].Add(account.Longs.Count());
//
// 							data.Elapsed = DateTime.UtcNow - startTime;
// 						}
// 					);
// 				}
//
// 				var open = chunk.First().OpenPrice;
// 				var high = chunk.Max(i => i.HighPrice);
// 				var low = chunk.Min(i => i.LowPrice);
// 				var close = chunk.Last().ClosePrice;
//
// 				prices[pair].Add([open, high, low, close]);
// 				chunkIndex++;
//
// 				if (chunkIndex % MaxChunks == 0)
// 				{
// 					await SaveFile(chunkIndex, results, balances, shorts, longs, shortsCount, longsCount, prices,
// 						stoppingToken);
// 				}
// 			}
// 		}
//
// 		_logger.LogInformation("Benchmark completed");
// 	}
//
// 	private async IAsyncEnumerable<IReadOnlyCollection<KlineItem>> FetchKlineHistoryChunks(string pair)
// 	{
// 		var cache = new List<KlineItem>(ChunkStep);
//
// 		await foreach (var item in _sandbox.FetchKlineHistory(pair))
// 		{
// 			cache.Add(item);
//
// 			if (cache.Count >= ChunkStep)
// 			{
// 				yield return cache;
//
// 				cache = new List<KlineItem>();
// 			}
// 		}
//
// 		if (cache.Count > 0)
// 		{
// 			yield return cache;
// 		}
// 	}
//
// 	private async Task SaveFile(
// 		int chunkIndex,
// 		Dictionary<IStrategy, List<decimal>> results,
// 		Dictionary<IStrategy, List<decimal>> balances,
// 		Dictionary<IStrategy, List<decimal>> shorts,
// 		Dictionary<IStrategy, List<decimal>> longs,
// 		Dictionary<IStrategy, List<decimal>> shortsCount,
// 		Dictionary<IStrategy, List<decimal>> longsCount,
// 		Dictionary<string, List<decimal[]>> prices,
// 		CancellationToken stoppingToken
// 	)
// 	{
// 		await File.WriteAllTextAsync(
// 			"benchmark.json",
// 			JsonSerializer.Serialize(new
// 			{
// 				StartTime = SandboxService.StartTime.ToString("yyyy-MM-dd"),
// 				EndTime =
// 					(SandboxService.StartTime + (TimeSpan.FromSeconds(ChunkStep) * chunkIndex)).ToString("yyyy-MM-dd"),
// 				SaveStepSeconds = SaveStep,
// 				Strategies = _strategies.SelectMany(pair => pair.Value.Select(p => new
// 				{
// 					Name = p.Key,
// 					Pair = pair.Key,
// 					Values = results[p.Value].Select((v, i) => new
// 					{
// 						Value = v,
// 						Time = ((DateTimeOffset)(SandboxService.StartTime +
// 						                         (TimeSpan.FromSeconds(ChunkStep) * i)))
// 							.ToUnixTimeMilliseconds(),
// 					}),
// 					Balances = balances[p.Value].Select((v, i) => new
// 					{
// 						Value = v,
// 						Time = ((DateTimeOffset)(SandboxService.StartTime +
// 						                         (TimeSpan.FromSeconds(ChunkStep) * i)))
// 							.ToUnixTimeMilliseconds(),
// 					}),
// 					Shorts = shorts[p.Value].Select((v, i) => new
// 					{
// 						Value = v,
// 						Time = ((DateTimeOffset)(SandboxService.StartTime +
// 						                         (TimeSpan.FromSeconds(ChunkStep) * i)))
// 							.ToUnixTimeMilliseconds(),
// 					}),
// 					Longs = longs[p.Value].Select((v, i) => new
// 					{
// 						Value = v,
// 						Time = ((DateTimeOffset)(SandboxService.StartTime +
// 						                         (TimeSpan.FromSeconds(ChunkStep) * i)))
// 							.ToUnixTimeMilliseconds(),
// 					}),
// 					ShortsCount = shortsCount[p.Value].Select((v, i) => new
// 					{
// 						Value = v,
// 						Time = ((DateTimeOffset)(SandboxService.StartTime +
// 						                         (TimeSpan.FromSeconds(ChunkStep) * i)))
// 							.ToUnixTimeMilliseconds(),
// 					}),
// 					LongsCount = longsCount[p.Value].Select((v, i) => new
// 					{
// 						Value = v,
// 						Time = ((DateTimeOffset)(SandboxService.StartTime +
// 						                         (TimeSpan.FromSeconds(ChunkStep) * i)))
// 							.ToUnixTimeMilliseconds(),
// 					}),
// 				})),
// 				Prices = prices.Select(pair => new
// 				{
// 					Pair = pair.Key,
// 					Values = pair.Value.Select((v, i) => new
// 					{
// 						Value = v,
// 						Time = ((DateTimeOffset)(SandboxService.StartTime + (TimeSpan.FromSeconds(ChunkStep) * i)))
// 							.ToUnixTimeMilliseconds(),
// 					}),
// 				}),
// 			}),
// 			cancellationToken: stoppingToken
// 		);
// 	}
// }