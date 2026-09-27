namespace CitadelIQ.Domain.Entities;

/// <summary>
/// The vector representation of a single <see cref="DocumentChunk"/>, produced by the configured
/// OpenAI embedding model.
/// </summary>
public class DocumentEmbedding
{
    public Guid ChunkId { get; private set; }
    public float[] Vector { get; private set; }
    public string ModelName { get; private set; }

    private DocumentEmbedding(Guid chunkId, float[] vector, string modelName)
    {
        ChunkId = chunkId;
        Vector = vector;
        ModelName = modelName;
    }

    public static DocumentEmbedding Create(Guid chunkId, float[] vector, string modelName) =>
        new(chunkId, vector, modelName);
}
