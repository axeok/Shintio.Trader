using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Prediction.Common;
using Shintio.Trader.Prediction.Utils;
using Shintio.Trader.Tables;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn.functional;
using static TorchSharp.torch.utils.data;
using static System.FormattableString;

namespace Shintio.Trader.Prediction.Predictors;

public class KlinePredictor
{
	private const int BatchSize = 64;

	private const float LearningRate = 0.00001f;
	private const float Dropout = 0f;

	private PredictionModel _model;
	private const string ModelPath = "price_predictor.pt";
	private readonly ILogger<KlinePredictor> _logger;

	private const int InputCandles = 60 * 6;
	private const int OutputCandlesOffset = 59;
	private const int FeaturesPerCandle = 7;
	private const int InputSize = InputCandles * FeaturesPerCandle;
	private const int HiddenSize = 1024;
	private const int OutputSize = 1;

	private bool _isModelLoaded;

	public KlinePredictor(ILogger<KlinePredictor> logger)
	{
		_logger = logger;
		_model = new PredictionModel(InputSize, HiddenSize, OutputSize, Dropout);
		_model.to(PredictionConstants.Device);
		_isModelLoaded = false;
	}
	
	public async Task Train(List<KlineItem> trainingData, int epochs = 100)
	{
		_logger.LogInformation(
			$"{(_isModelLoaded ? "Продолжение" : "Начало")} обучения на {trainingData.Count} свечах ({PredictionConstants.Device.type})...");

		if (!_isModelLoaded)
		{
			_model = new PredictionModel(InputSize, HiddenSize, OutputSize, Dropout);
			_model.to(PredictionConstants.Device);
		}

		var dataset = new KlineDataset(trainingData, InputCandles, OutputCandlesOffset, PrepareInputTensor);
		var dataloader = new DataLoader(dataset,
			batchSize: BatchSize,
			shuffle: true,
			num_worker: Environment.ProcessorCount - 1
		);

		var optimizer = optim.Adam(_model.parameters(), lr: LearningRate);
		// var scheduler = optim.lr_scheduler.ReduceLROnPlateau(
		// 	optimizer,
		// 	mode: "min",
		// 	factor: 0.5f,
		// 	patience: 5,
		// 	verbose: true
		// );

		var stopwatch = Stopwatch.StartNew();
		for (var epoch = 0; epoch < epochs; epoch++)
		{
			var totalLoss = 0.0f;

			var batchIndex = 0;
			foreach (var batch in dataloader)
			{
				var inputs = batch["input"].to(PredictionConstants.Device);
				var targets = batch["target"].to(PredictionConstants.Device);

				optimizer.zero_grad();

				var predictions = _model.forward(inputs);
				predictions = predictions.reshape(targets.shape).to(PredictionConstants.Device);

				var loss = mse_loss(predictions.to(PredictionConstants.Device), targets.to(PredictionConstants.Device));

				var absoluteError = (predictions - targets).abs().mean().item<float>() * KlineNormalizer.MaxPrice;
				var percentageError = ((predictions - targets).abs() / targets * 100).mean().item<float>();

				loss.backward();
				optimizer.step();

				if (batchIndex % 100 == 0)
				{
					_logger.LogInformation(
						$"[{epoch * dataloader.Count + batchIndex}/{epochs * dataloader.Count}] " +
						Invariant($"Средняя ошибка: {absoluteError:F2} USDT ({percentageError:F2}%)"));
				}

				totalLoss += loss.item<float>();
				batchIndex++;

				var averageLoss = totalLoss / batchIndex;

				// scheduler.step(averageLoss);

				// if (averageLoss < bestLoss)
				// {
				// 	bestLoss = averageLoss;
				// 	await SaveModel();
				// }
			}

			var timePerEpoch = stopwatch.ElapsedMilliseconds / (epoch + 1);
			var estimatedTimeLeft = timePerEpoch * (epochs - epoch - 1) / 1000;

			_logger.LogInformation(
				"Эпоха: {Epoch}/{TotalEpochs}, " +
				"Время на эпоху: {TimePerEpoch}мс, " +
				"Осталось примерно: {TimeLeft:F1}с",
				epoch + 1, epochs, timePerEpoch, estimatedTimeLeft);

			if (epoch % 50 == 0)
			{
				await SaveModel(epoch.ToString());
			}
		}

		stopwatch.Stop();
		_logger.LogInformation(
			"Обучение завершено за {TotalTime:F1} секунд",
			stopwatch.ElapsedMilliseconds / 1000.0);

		await SaveModel();
	}


	public async Task<bool> LoadModel()
	{
		if (!File.Exists(ModelPath))
		{
			_logger.LogInformation("Существующая модель не найдена");
			_isModelLoaded = false;
			return false;
		}

		using var fs = File.OpenRead(ModelPath);
		try
		{
			_model.load(fs);
			_model.to(PredictionConstants.Device);
			_isModelLoaded = true;
			_logger.LogInformation("Существующая модель успешно загружена");
		}
		catch (Exception e)
		{
			_logger.LogInformation($"Ошибка загрузки модели: {e.Message}");
			return false;
		}
		
		return true;
	}

	public async Task SaveModel(string postfix = "")
	{
		using var fs = File.Create(ModelPath + postfix);
		_model.save(fs);
	}

	public async Task<decimal> Predict(IReadOnlyCollection<KlineItem> sequence)
	{
		var input = PrepareInputTensor(sequence);
		input = input.unsqueeze(0);
		var prediction = _model.forward(input);
    
		return (decimal)(prediction.item<float>() * KlineNormalizer.MaxPrice);
	}

	private Tensor PrepareInputTensor(IReadOnlyCollection<KlineItem> sequence)
	{
		var data = new float[InputSize];
		var idx = 0;

		foreach (var kline in sequence)
		{
			var normalized = KlineNormalizer.Normalize(kline);
			foreach (var value in normalized)
			{
				data[idx++] = value;
			}
		}

		return tensor(data, device: PredictionConstants.Device);
	}
}