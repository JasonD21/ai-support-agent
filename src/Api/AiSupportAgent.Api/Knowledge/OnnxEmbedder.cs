using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace AiSupportAgent.Api.Knowledge;

// Runs a BERT-family sentence model in-process. Outputs token embeddings, which we
// mean-pool (over the attention mask) and L2-normalize ourselves — the §3 correctness rule.
public sealed class OnnxEmbedder : IEmbedder, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly bool _needsTokenTypeIds;
    private const int MaxTokens = 256;

    public int Dimension => 384;
    public string ModelId { get; }

    public OnnxEmbedder(string modelPath, string vocabPath, string modelId)
    {
        var options = new Microsoft.ML.OnnxRuntime.SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 }; // modest CPU
        _session = new InferenceSession(modelPath, options);
        _tokenizer = BertTokenizer.Create(vocabPath);
        _needsTokenTypeIds = _session.InputMetadata.ContainsKey("token_type_ids");
        ModelId = modelId;
    }

    public float[] Embed(string text)
    {
        var ids = _tokenizer.EncodeToIds(text ?? string.Empty);   // adds [CLS]/[SEP], lowercases (uncased vocab)
        var seq = Math.Min(ids.Count, MaxTokens);

        var inputIds = new DenseTensor<long>(new[] { 1, seq });
        var attentionMask = new DenseTensor<long>(new[] { 1, seq });
        var tokenTypeIds = new DenseTensor<long>(new[] { 1, seq });
        for (var i = 0; i < seq; i++)
        {
            inputIds[0, i] = ids[i];
            attentionMask[0, i] = 1;
            tokenTypeIds[0, i] = 0;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
        };
        if (_needsTokenTypeIds)
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds));

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        var pooled = new float[Dimension];
        if (output.Dimensions.Length == 3)            // [1, seq, 384] token embeddings → mean-pool
        {
            for (var t = 0; t < seq; t++)
                for (var d = 0; d < Dimension; d++)
                    pooled[d] += output[0, t, d];
            for (var d = 0; d < Dimension; d++)
                pooled[d] /= seq;
        }
        else                                          // [1, 384] already pooled by the export
        {
            for (var d = 0; d < Dimension; d++)
                pooled[d] = output[0, d];
        }

        double norm = 0;
        for (var d = 0; d < Dimension; d++) norm += pooled[d] * (double)pooled[d];
        norm = Math.Sqrt(norm);
        if (norm > 0)
            for (var d = 0; d < Dimension; d++) pooled[d] = (float)(pooled[d] / norm);

        return pooled;
    }

    public void Dispose() => _session.Dispose();
}