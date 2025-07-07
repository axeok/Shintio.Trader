using static TorchSharp.torch;

namespace Shintio.Trader.Prediction;

public static class PredictionConstants
{
	public static readonly Device Device = cuda.is_available() ? CUDA : CPU;
}