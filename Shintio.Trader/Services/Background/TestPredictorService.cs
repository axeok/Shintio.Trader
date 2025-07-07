using System.Diagnostics;
using Binance.Net.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Enums;
using Shintio.Trader.Prediction.Utils;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;
using TorchSharp;
using KlinePredictor = Shintio.Trader.Prediction.Predictors.KlinePredictor;
using static System.FormattableString;

namespace Shintio.Trader.Services.Background;

public class TestPredictorService : BackgroundService
{
	private readonly ILogger<TestPredictorService> _logger;
	private readonly KlinePredictor _predictor;
	private readonly BinanceService _binanceService;
	private readonly SandboxService _sandbox;

	public TestPredictorService(
		ILogger<TestPredictorService> logger,
		KlinePredictor predictor,
		BinanceService binanceService,
		SandboxService sandbox
	)
	{
		_logger = logger;
		_predictor = predictor;
		_binanceService = binanceService;
		_sandbox = sandbox;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var start = new DateTime(2025, 6, 1);
		var end = new DateTime(2025, 6, 30);

		_logger.LogInformation(
			"Загрузка исторических данных за период {Start:d} - {End:d}...",
			start, end);

		var history = _sandbox.GetRange(
			CurrencyPair.ETH_USDT,
			start,
			end
		);

		var trainingData = new List<KlineItem>(history);

		_logger.LogInformation($"Загружено {trainingData.Count} свечей");

		var modelLoaded = await _predictor.LoadModel();

		// await _predictor.Train(trainingData, 20);

		_logger.LogInformation("Проверка предсказаний...");

		for (var i = 0; i < 30; i++)
		{
			await Predict(trainingData);
		}
	}

	private async Task Predict(List<KlineItem> history)
	{
		var skip = Random.Shared.Next(history.Count - 200);

		var lastCandles = history.Skip(skip).Take(60).ToList();
		var lastPrice = lastCandles.Last().ClosePrice;
		
		var targetPrice = history.Skip(skip + 60 + 59).First().ClosePrice;
		
		var predictedPrice = await _predictor.Predict(lastCandles);

		var loss = MoneyHelper.GetPercent(targetPrice, predictedPrice);

		var isUp = lastPrice < targetPrice;
		var correct = isUp ? lastPrice < predictedPrice : lastPrice > predictedPrice;
		
		_logger.LogInformation(Invariant($"[{correct}] {targetPrice} -> {predictedPrice} ({Math.Abs(loss * 100):f2}%)"));
	}
}