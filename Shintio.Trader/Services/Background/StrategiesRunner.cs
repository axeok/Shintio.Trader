using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Background;

public class StrategiesRunner : BackgroundService
{
	public static readonly decimal BaseCommissionPercent = 0.0005m;
	public static readonly string Pair = CurrencyPair.SOL_USDT;
	public static readonly int LogStep = (int)TimeSpan.FromHours(6).TotalSeconds;

	private readonly ILogger<StrategiesRunner> _logger;

	private readonly IStrategy[] _strategies;
	private readonly SandboxService _sandbox;

	public StrategiesRunner(ILogger<StrategiesRunner> logger, IEnumerable<IStrategy> strategies, SandboxService sandbox)
	{
		_logger = logger;

		_strategies = strategies.ToArray();
		_sandbox = sandbox;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var history = new List<KlineItem>((int)(SandboxService.EndTime - SandboxService.StartTime).TotalSeconds);
		var accounts = new Dictionary<IStrategy, TradeAccount>();

		foreach (var strategy in _strategies)
		{
			accounts[strategy] =
				new TradeAccount(strategy.InitialBalance, BaseCommissionPercent, strategy.ValidateBalance);
		}

		var i = 0;
		await foreach (var item in _sandbox.FetchKlineHistory(Pair))
		{
			history.Add(item);

			var currentPrice = item.OpenPrice;
			var needToLog = i % LogStep == 0;

			if (needToLog)
			{
				_logger.LogInformation($"{item.OpenTime}: ${currentPrice}");

				foreach (var strategy in _strategies)
				{
					var account = accounts[strategy];

					var currentBalance = account.CalculateTotalCurrentQuantity(currentPrice);
					var logString = strategy.GetLogString(account, currentPrice, history, i);

					_logger.LogInformation(
						$"[{strategy.GetType().Name}] ${MoneyHelper.FormatMoney(currentBalance)} | {logString}");
				}
			}

			Parallel.ForEach(_strategies, strategy =>
			{
				var account = accounts[strategy];

				strategy.Run(account, currentPrice, history, i);
			});

			i++;
		}
	}
}