namespace AiSupportAgent.Api.Knowledge;

public interface IEmbedder
{
    int Dimension { get; }
    string ModelId { get; }
    float[] Embed(string text);
}