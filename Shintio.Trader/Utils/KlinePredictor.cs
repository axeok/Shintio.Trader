using System.Collections.Concurrent;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Utils;

public class KlinePredictor
{
    public class SingleModelInput
    {
        [VectorType(1200)]
        public float[] Features { get; set; }
        public float Label { get; set; }
    }

    public class SingleModelPrediction
    {
        [ColumnName("Score")]
        public float PredictedValue { get; set; }
    }

    public void Train(KlineItem[] items)
    {
        var mlContext = new MLContext(seed: 0);
        
        // 2. Подготовка обучающих данных
        var trainingData = new ConcurrentBag<(float[] Features, float[] Label)>();
        
        var step = 0;
        // var count = 1_000;
        var count = items.Length - 360;

        var evaluateOffset = Random.Shared.Next(0, count);

        var evaluateData = items.Skip(evaluateOffset)
            .Take(300)
            .ToArray();
        var checkData = items.Skip(evaluateOffset + 300)
            .Take(60)
            .ToArray();

        // for (int i = 0; i < count; i++)
        // {
        //     var inputFeatures = items.Skip(i).Take(300)
        //         .SelectMany(k => new[]
        //         {
        //             (float)k.OpenPrice,
        //             (float)k.ClosePrice,
        //             (float)k.LowPrice,
        //             (float)k.HighPrice
        //         })
        //         .ToArray(); // 1200 признаков
        //
        //     var outputFeatures = items.Skip(i + 300).Take(60)
        //         .SelectMany(k => new[]
        //         {
        //             (float)k.OpenPrice,
        //             (float)k.ClosePrice,
        //             (float)k.LowPrice,
        //             (float)k.HighPrice
        //         })
        //         .ToArray(); // 240 признаков
        //
        //     trainingData.Add((inputFeatures, outputFeatures));
        //
        //     step++;
        //     if (step % 10000 == 0)
        //     {
        //         Console.WriteLine($"{step}/{count}");
        //     }
        // }
        //
        // Console.WriteLine("Начинаем обучение моделей...");
        //
        // var models = new ConcurrentDictionary<int, ITransformer>();
        // var predictors = new ConcurrentDictionary<int, PredictionEngine<SingleModelInput, SingleModelPrediction>>();
        //
        // var modelCount = 240;
        // var completedModels = 0;
        //
        // // Параллельное обучение моделей
        // Parallel.For(0, modelCount, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        // {
        //     var singleLabelData = mlContext.Data.LoadFromEnumerable(
        //         trainingData.Select(row => new SingleModelInput 
        //         { 
        //             Features = row.Features,
        //             Label = row.Label[i]
        //         }));
        //
        //     var pipeline = mlContext.Transforms.NormalizeMinMax("Features")
        //         .Append(mlContext.Regression.Trainers.LightGbm(
        //             labelColumnName: "Label",
        //             featureColumnName: "Features",
        //             numberOfLeaves: 100,
        //             minimumExampleCountPerLeaf: 10,
        //             learningRate: 0.1));
        //
        //     var model = pipeline.Fit(singleLabelData);
        //     models.TryAdd(i, model);
        //     
        //     var predictor = mlContext.Model.CreatePredictionEngine<SingleModelInput, SingleModelPrediction>(model);
        //     predictors.TryAdd(i, predictor);
        //     
        //     var current = Interlocked.Increment(ref completedModels);
        //     if (current % 10 == 0)
        //     {
        //         Console.WriteLine($"Обучено {current}/{modelCount} моделей");
        //     }
        // });
        //
        // // Сохраняем модели
        // Directory.CreateDirectory("Models");
        // foreach (var pair in models)
        // {
        //     mlContext.Model.Save(pair.Value, null, $"Models/kline_model_{pair.Key}.zip");
        // }

        var models = LoadModels(mlContext);
        var predictors = new ConcurrentDictionary<int, PredictionEngine<SingleModelInput, SingleModelPrediction>>();
        for (var i = 0; i < 240; i++)
        {
            var predictor = mlContext.Model.CreatePredictionEngine<SingleModelInput, SingleModelPrediction>(models[i]);
            predictors.TryAdd(i, predictor);
        }

        // Тестирование
        var testSeries = evaluateData;
        var testFeatures = testSeries.Take(300)
            .SelectMany(k => new[] 
            {
                (float)k.OpenPrice,
                (float)k.ClosePrice,
                (float)k.LowPrice,
                (float)k.HighPrice
            })
            .ToArray();

        var predictions = new float[240];
        for (int i = 0; i < 240; i++)
        {
            if (predictors.TryGetValue(i, out var predictor))
            {
                var prediction = predictor.Predict(new SingleModelInput { Features = testFeatures });
                predictions[i] = prediction.PredictedValue;
            }
        }

        var printCount = 60;

        // Выводим результаты
        // Console.WriteLine($"Предсказание первых {printCount} свечей:");
        Console.WriteLine("Предсказание: ");
        for (var i = 59; i < printCount; i++)
        {
            var offset = i * 4;
            Console.WriteLine($"Свеча {i + 1}: " +
                              $"Open={predictions[offset]:F2}, " +
                              $"Close={predictions[offset + 1]:F2}, " +
                              $"Low={predictions[offset + 2]:F2}, " +
                              $"High={predictions[offset + 3]:F2}");
        }

        // Выводим результаты
        // Console.WriteLine($"Реальные данные первых {printCount} свечей:");
        Console.WriteLine("Реальные данные: ");
        for (var i = 59; i < printCount; i++)
        {
            Console.WriteLine($"Свеча {i + 1}: " +
                              $"Open={checkData[i].OpenPrice:F2}, " +
                              $"Close={checkData[i].ClosePrice:F2}, " +
                              $"Low={checkData[i].LowPrice:F2}, " +
                              $"High={checkData[i].HighPrice:F2}");
        }

        // Выводим результаты
        // Console.WriteLine($"Реальные данные первых {printCount} свечей:");
        Console.WriteLine("Конец входа: ");
        for (var i = 59; i < printCount; i++)
        {
            var last = evaluateData.Last();
            
            Console.WriteLine($"Свеча {i + 1}: " +
                              $"Open={last.OpenPrice:F2}, " +
                              $"Close={last.ClosePrice:F2}, " +
                              $"Low={last.LowPrice:F2}, " +
                              $"High={last.HighPrice:F2}");
        }
    }

    public List<ITransformer> LoadModels(MLContext mlContext)
    {
        var models = new List<ITransformer>();
        for (int i = 0; i < 240; i++)
        {
            var modelPath = $"Models/kline_model_{i}.zip";
            if (File.Exists(modelPath))
            {
                models.Add(mlContext.Model.Load(modelPath, out var _));
            }
        }
        return models;
    }
}