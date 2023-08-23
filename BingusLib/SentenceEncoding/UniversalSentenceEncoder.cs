using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace BingusLib.SentenceEncoding
{
    public class UniversalSentenceEncoder : SentenceEncoder, IDisposable
    {
        private readonly ILogger<UniversalSentenceEncoder> Logger;

        public readonly string ModelPath;

        private readonly SessionOptions _sessionOptions = new();
        private readonly IntPtr _libraryHandle;
        private readonly InferenceSession _session;

        private readonly DenseTensor<string> _inputTensor = new(1);
        private readonly NamedOnnxValue[] _inputs;

        public UniversalSentenceEncoder(ILogger<UniversalSentenceEncoder> logger, string modelPath) : base(512)
        {
            Logger = logger;

            ModelPath = Path.GetFullPath(modelPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _sessionOptions.RegisterCustomOpLibraryV2("libs/ortextensions.dll", out _libraryHandle);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _sessionOptions.RegisterCustomOpLibraryV2("libs/libortextensions.so", out _libraryHandle);
            }
            else
            {
                Logger.LogError("Running on an unsupported OS, could not load ONNX custom ops!");
            }

            try
            {
                _sessionOptions.AppendExecutionProvider_CPU();
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "CPU is not available");
            }

            try
            {
                _sessionOptions.AppendExecutionProvider_CUDA();
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "CUDA is not available");
            }

            _session = new(ModelPath, _sessionOptions);

            try
            {
                EmbeddingDimension = _session.OutputMetadata.Single().Value.Dimensions[1];
                logger.LogInformation("Output dimension detected as {EmbeddingDimension}", EmbeddingDimension);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "Output dimension could not be detected, defaulting to {EmbeddingDimension}", EmbeddingDimension);
            }

            _inputs = new[] { NamedOnnxValue.CreateFromTensor("inputs", _inputTensor) };
        }

        protected override float[] InternalComputeEmbedding(string input, float[] vectorBuffer)
        {
            _inputTensor.SetValue(0, input);

            using var outputs = _session.Run(_inputs);
            var outputTensor = (DenseTensor<float>)outputs.Single().Value;

            for (var i = 0; i < EmbeddingDimension; i++)
            {
                vectorBuffer[i] = outputTensor.GetValue(i);
            }

            return vectorBuffer;
        }

        public void Dispose()
        {
            _session.Dispose();
            _sessionOptions.Dispose();
            NativeLibrary.Free(_libraryHandle);
            GC.SuppressFinalize(this);
        }
    }
}
