using System.Diagnostics;
using System.Text.Json;
using Binance.Net.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Enums;
using Shintio.Trader.Models;
using Shintio.Trader.Models.Managers;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Models.Strategies.Skis;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Background;

public class KlineDownloader : BackgroundService
{
	private readonly ILogger<KlineDownloader> _logger;

	private readonly BinanceService _binanceService;

	public KlineDownloader(ILogger<KlineDownloader> logger, BinanceService binanceService)
	{
		_logger = logger;

		_binanceService = binanceService;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var start = new DateTime(2025, 6, 1);
		var end = new DateTime(2025, 6, 2);

		var collectStep = TimeSpan.FromHours(1);

		var stopwatch = Stopwatch.StartNew();

		var history = await _binanceService.FetchKlineHistory(
			CurrencyPair.ETH_USDT,
			KlineInterval.OneSecond,
			start,
			end
		).ToArrayAsync();

		File.WriteAllText("data.json", JsonSerializer.Serialize(history, new JsonSerializerOptions
		{
			WriteIndented = true,
		}));

		_logger.LogInformation("Download completed");
	}
}