using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using static TorchSharp.torch.nn.functional;

namespace Shintio.Trader.Prediction.Common;

public class PredictionModel : Module<Tensor, Tensor>
{
	private readonly Linear _inputLayer;   // 2520
	private readonly Linear _hiddenLayer1; // 1024
	private readonly Linear _hiddenLayer2; // 512
	private readonly Linear _hiddenLayer3; // 256
	private readonly Linear _hiddenLayer4; // 128
	private readonly Linear _hiddenLayer5; // 64
	private readonly Linear _hiddenLayer6; // 32
	private readonly Linear _outputLayer;  // 16

	private readonly Dropout _dropout1;
	private readonly Dropout _dropout2;
	private readonly Dropout _dropout3;
	private readonly Dropout _dropout4;
	private readonly Dropout _dropout5;
	private readonly Dropout _dropout6;
	private readonly Dropout _dropout7;

	public PredictionModel(int inputSize, int hiddenSize, int outputSize, float dropout)
		: base(nameof(PredictionModel))
	{
		var hiddenSize2 = hiddenSize / 2;  // 512
		var hiddenSize3 = hiddenSize2 / 2; // 256
		var hiddenSize4 = hiddenSize3 / 2; // 128
		var hiddenSize5 = hiddenSize4 / 2; // 64
		var hiddenSize6 = hiddenSize5 / 2; // 32
		var hiddenSize7 = hiddenSize6 / 2; // 16

		_inputLayer = Linear(inputSize, hiddenSize);
		
		_hiddenLayer1 = Linear(hiddenSize, hiddenSize2);
		_hiddenLayer2 = Linear(hiddenSize2, hiddenSize3);
		_hiddenLayer3 = Linear(hiddenSize3, hiddenSize4);
		_hiddenLayer4 = Linear(hiddenSize4, hiddenSize5);
		_hiddenLayer5 = Linear(hiddenSize5, hiddenSize6);
		_hiddenLayer6 = Linear(hiddenSize6, hiddenSize7);
		
		_outputLayer = Linear(hiddenSize7, outputSize);

		_dropout1 = Dropout(dropout);
		_dropout2 = Dropout(dropout);
		_dropout3 = Dropout(dropout);
		_dropout4 = Dropout(dropout);
		_dropout5 = Dropout(dropout);
		_dropout6 = Dropout(dropout);
		_dropout7 = Dropout(dropout);

		RegisterComponents();

		this.to(PredictionConstants.Device);
	}

	public override Tensor forward(Tensor input)
	{
		var result = _inputLayer.forward(input);
		result = relu(result);
		result = _dropout1.forward(result);

		result = _hiddenLayer1.forward(result);
		result = relu(result);
		result = _dropout2.forward(result);

		result = _hiddenLayer2.forward(result);
		result = relu(result);
		result = _dropout3.forward(result);

		result = _hiddenLayer3.forward(result);
		result = relu(result);
		result = _dropout4.forward(result);

		result = _hiddenLayer4.forward(result);
		result = relu(result);
		result = _dropout5.forward(result);

		result = _hiddenLayer5.forward(result);
		result = relu(result);
		result = _dropout6.forward(result);

		result = _hiddenLayer6.forward(result);
		result = relu(result);
		result = _dropout7.forward(result);

		result = _outputLayer.forward(result);

		return result;
	}
}