using Shintio.Trader.Tables;

namespace Shintio.Trader.Prediction.Utils;

public static class KlineNormalizer
{
	public static float MaxPrice = 3744.28f;
	public static float MaxVolume = 325265.823f;
	public static float MaxTradeCount = 158977;

	public static float[] Normalize(KlineItem kline)
	{
		return
		[
			(float)kline.OpenPrice / MaxPrice,
			(float)kline.ClosePrice / MaxPrice,
			(float)kline.HighPrice / MaxPrice,
			(float)kline.LowPrice / MaxPrice,
			(float)kline.Volume / MaxVolume,
			kline.TradeCount / MaxTradeCount,
			(float)kline.TakerBuyBaseVolume / MaxVolume,
		];
	}
}

// public class KlineNormalizer
// {
// 	private readonly float[] _means;
// 	private readonly float[] _stds;
// 	private const int FeaturesCount = 7;
//
// 	public KlineNormalizer(List<KlineItem> data)
// 	{
// 		_means = new float[FeaturesCount];
// 		_stds = new float[FeaturesCount];
//
// 		var features = new List<float[]>();
// 		for (var i = 0; i < FeaturesCount; i++)
// 		{
// 			features.Add(new float[data.Count]);
// 		}
//
// 		for (var i = 0; i < data.Count; i++)
// 		{
// 			var kline = data[i];
// 			features[0][i] = (float)kline.OpenPrice;
// 			features[1][i] = (float)kline.ClosePrice;
// 			features[2][i] = (float)kline.HighPrice;
// 			features[3][i] = (float)kline.LowPrice;
// 			features[4][i] = (float)kline.Volume;
// 			features[5][i] = kline.TradeCount;
// 			features[6][i] = (float)kline.TakerBuyBaseVolume;
// 		}
//
// 		for (var i = 0; i < FeaturesCount; i++)
// 		{
// 			_means[i] = features[i].Average();
// 			_stds[i] = (float)Math.Sqrt(features[i].Select(x => Math.Pow(x - _means[i], 2)).Average());
// 		}
// 	}
//
// 	public float[] Normalize(KlineItem kline)
// 	{
// 		return
// 		[
// 			((float)kline.OpenPrice - _means[0]) / _stds[0],
// 			((float)kline.ClosePrice - _means[1]) / _stds[1],
// 			((float)kline.HighPrice - _means[2]) / _stds[2],
// 			((float)kline.LowPrice - _means[3]) / _stds[3],
// 			((float)kline.Volume - _means[4]) / _stds[4],
// 			(kline.TradeCount - _means[5]) / _stds[5],
// 			((float)kline.TakerBuyBaseVolume - _means[6]) / _stds[6]
// 		];
// 	}
//
// 	public decimal Denormalize(float value, int featureIndex)
// 	{
// 		return (decimal)(value * _stds[featureIndex] + _means[featureIndex]);
// 	}
// }