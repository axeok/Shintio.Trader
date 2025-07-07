using Shintio.Trader.Tables;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.utils.data;

namespace Shintio.Trader.Prediction.Utils;

public class KlineDataset : Dataset
{
	public delegate Tensor PrepareInputDelegate(IReadOnlyCollection<KlineItem> sequence);

	private readonly List<KlineItem> _data;
	private readonly int _sequenceLength;
	private readonly int _outputOffset;

	private readonly PrepareInputDelegate _prepareInput;

	public KlineDataset(List<KlineItem> data, int sequenceLength, int outputOffset, PrepareInputDelegate prepareInput)
	{
		_data = data;
		_sequenceLength = sequenceLength;
		_outputOffset = outputOffset;
		_prepareInput = prepareInput;
	}

	public override long Count => _data.Count - (_sequenceLength + _outputOffset);

	public override Dictionary<string, Tensor> GetTensor(long index)
	{
		var inputSequence = _data.Skip((int)index)
			.Take(_sequenceLength)
			.ToList();

		var targetPrice = _data[(int)index + _sequenceLength + _outputOffset].ClosePrice;

		var inputTensor = _prepareInput(inputSequence);
		var targetTensor = PrepareTargetTensor(targetPrice);

		if (inputTensor.device != PredictionConstants.Device)
		{
			inputTensor = inputTensor.to(PredictionConstants.Device);
		}

		if (targetTensor.device != PredictionConstants.Device)
		{
			targetTensor = targetTensor.to(PredictionConstants.Device);
		}

		return new Dictionary<string, Tensor>
		{
			["input"] = inputTensor,
			["target"] = targetTensor.reshape(1),
		};
	}

	private Tensor PrepareTargetTensor(decimal targetPrice)
	{
		return tensor((float)targetPrice, device: PredictionConstants.Device);
	}
}