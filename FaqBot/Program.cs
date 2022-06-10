using FaqBot.HNSW;
using FaqBot.SentenceEncoding;
using HNSW.Net;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => { builder.AddSimpleConsole(options => { options.TimestampFormat = "[hh:mm:ss] "; }); });
var logger = loggerFactory.CreateLogger<Program>();

var vectors = new List<float[]>(2);

StaticCompositeResolver.Instance.Register(MessagePackSerializer.DefaultOptions.Resolver);
StaticCompositeResolver.Instance.Register(new LazyKeyItemFormatter<int, float[]>(i => vectors[i]));
MessagePackSerializer.DefaultOptions.WithResolver(StaticCompositeResolver.Instance);

var modelPath = Path.Join(Environment.CurrentDirectory, "models/onnx/use_l_v5.onnx");
using var encoder = new UniversalSentenceEncoder(loggerFactory.CreateLogger<UniversalSentenceEncoder>(), modelPath);

var vectorBuffer = Vector<float>.Build.Dense(encoder.OutputDimension);

Vector<float> PrintVectorEmbedding(string input, Vector<float> vector)
{
    logger.LogInformation($"\"{input}\":\n[{vector.ToVectorString()}]");
    return vector;
}

Vector<float> PrintEmbedding(string input)
{
    return PrintVectorEmbedding(input, encoder.ComputeEmbeddingVector(input, vectorBuffer));
}

Vector<float> PrintEmbeddingNewVector(string input)
{
    return PrintVectorEmbedding(input, encoder.ComputeEmbeddingVector(input));
}

PrintEmbedding("dog");
PrintEmbedding("Puppies are nice.");
PrintEmbedding("I enjoy taking long walks along the beach with my dog.");

var vector1 = PrintEmbeddingNewVector("The WiFi Settings window outputs symbols and nothing else");
var vector2 = PrintEmbeddingNewVector("My tracker is not appearing on the Server");

logger.LogInformation($"Distance: {Distance.Cosine(vector1.AsArray(), vector2.AsArray())}");

var questions = new string[]
{
    "Please specify upload_port while updating firmware",
    "Trying to upload firmware fails",
    "The server won’t start",
    "The SlimeVR server won’t start",
    "The WiFi Settings window outputs ERROR",
    "The WiFi Settings window outputs symbols and nothing else",
    "My tracker keeps flashing",
    "My tracker never connects to Wi-Fi",
    "My tracker is not appearing on the Server",
    "My tracker doesn't show up",
    "My aux tracker isn’t working",
    "My extension isn’t working",
    "Sensor was reset error",
    "The trackers are connected to my wifi but don’t turn up on the server",
    "The trackers are connected to the server but aren’t turning up on Steam",
    "The trackers aren't showing up in SteamVR",
    "Tracker doesn't showin up on SteamVR but it does on the server",
    "My trackers are bound to the wrong controllers in SteamVR",
    "My trackers are drifting a lot",
    "My feet sink into the floor",
    "My feet slide a lot",
    "Trackers are moving in the wrong direction when I move",
    "Trackers are rotating on SteamVR in the wrong direction when I move",
    "Does setup take a long time and/or do you need to do it every time you play?",
    "When are they shipping?",
    "When does it ship?",
    "When are the trackers coming?",
    "can I use 3 trackers for full body tracking",
};

foreach (var question in questions)
{
    vectors.Add(encoder.ComputeEmbedding(question));
}

var parameters = new SmallWorld<ILazyItem<float[]>, float>.Parameters();
var distance = new WrappedDistance<ILazyItem<float[]>, float[], float>(i => i.Value, CosineDistance.SIMD);

var graph = new SmallWorld<ILazyItem<float[]>, float>(distance.WrappedDistanceFunc, DefaultRandomGenerator.Instance, parameters);

IEnumerable<LazyKeyItem<int, float[]>> ConvertToLazyKeyItems(List<float[]> input)
{
    for (var i = 0; i < input.Count; i++)
    {
        yield return new(i, key => input[key]);
    }
}

graph.AddItems(ConvertToLazyKeyItems(vectors).ToArray());

var results = graph.KNNSearch(new LazyItemValue<float[]>(encoder.ComputeEmbedding("Trackers")), 15);
var sortedResults = results.OrderBy(i => i.Distance);

logger.LogInformation(string.Join(Environment.NewLine, sortedResults.Select(i => $"\"{questions[((LazyKeyItem<int, float[]>)i.Item).Key]}\": {i.Distance}")));
