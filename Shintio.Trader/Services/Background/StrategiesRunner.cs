// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
// using Shintio.Trader.Enums;
// using Shintio.Trader.Interfaces;
// using Shintio.Trader.Models;
// using Shintio.Trader.Services.Strategies;
// using Shintio.Trader.Tables;
// using Shintio.Trader.Utils;
//
// namespace Shintio.Trader.Services.Background;
//
// public class StrategiesRunner : BackgroundService
// {
// 	public static readonly decimal BaseCommissionPercent = 0.0005m;
// 	public static readonly string Pair = CurrencyPair.SOL_USDT;
// 	public static readonly int LogStep = (int)TimeSpan.FromHours(1).TotalSeconds;
// 	
// 	private static readonly int ChunkStep = LogStep;
//
// 	private readonly ILogger<StrategiesRunner> _logger;
//
// 	private readonly IStrategy[] _strategies;
// 	private readonly SandboxService _sandbox;
//
// 	public StrategiesRunner(ILogger<StrategiesRunner> logger, IEnumerable<IStrategy> strategies, SandboxService sandbox)
// 	{
// 		_logger = logger;
//
// 		_strategies = [new GlebStrategy()];
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
// 		var strategiesData = new Dictionary<IStrategy, StrategyData>();
//
// 		foreach (var strategy in _strategies)
// 		{
// 			strategiesData[strategy] = new StrategyData(
// 				new TradeAccount(strategy.InitialBalance, BaseCommissionPercent, strategy.ValidateBalance),
// 				new Queue<KlineItem>(strategy.MaxHistoryCount),
// 				strategy.MaxHistoryCount
// 			);
// 		}
//
// 		var chunkIndex = 0;
// 		await foreach (var chunk in FetchKlineHistoryChunks(Pair))
// 		{
// 			var firstItem = chunk.First();
// 			var startTime = DateTime.UtcNow;
//
// 			// _logger.LogInformation($"{firstItem.OpenTime}: ${firstItem.OpenPrice}");
//
// 			foreach (var strategy in _strategies)
// 			{
// 				LogStrategy(strategy, strategiesData[strategy], firstItem.OpenPrice, chunkIndex * ChunkStep);
// 			}
// 			
// 			Parallel.ForEach(_strategies, strategy =>
// 			{
// 				var data = strategiesData[strategy];
// 				var account = data.Account;
// 				var history = data.History;
// 				var maxHistoryCount = data.MaxHistoryCount;
//
// 				var stepIndex = chunkIndex * ChunkStep;
// 				foreach (var item in chunk)
// 				{
// 					history.Enqueue(item);
// 					if (history.Count > maxHistoryCount)
// 					{
// 						history.Dequeue();
// 					}
//
// 					var currentPrice = item.OpenPrice;
//
// 					if (strategy.AutoProcessMarket)
// 					{
// 						account.ProcessMarket(currentPrice);
// 					}
//
// 					if (stepIndex % strategy.RunStep == 0)
// 					{
// 						strategy.Run(account, currentPrice, history, stepIndex);
// 					}
//
// 					stepIndex++;
// 				}
// 				
// 				data.Elapsed = DateTime.UtcNow - startTime;
// 			});
//
// 			chunkIndex++;
// 		}
// 	}
//
// 	private void LogStrategy(IStrategy strategy, StrategyData data, decimal currentPrice, int step)
// 	{
// 		var currentBalance = data.Account.CalculateTotalCurrentQuantity(currentPrice);
// 		var logString = strategy.GetLogString(data.Account, currentPrice, data.History, step);
//
// 		_logger.LogInformation(
// 			$"[{strategy.GetType().Name}] {(int)data.Elapsed.TotalMilliseconds}ms | ${MoneyHelper.FormatMoney(currentBalance)} | {logString}");
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
// }