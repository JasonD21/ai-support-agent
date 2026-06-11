using AiSupportAgent.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace AiSupportAgent.Api.Knowledge;

public class KnowledgeService(AppDbContext db, IEmbedder embedder)
{
    // Rebuilds all chunks for an item: drop existing, chunk, embed each, store. Caller saves.
    public async Task ReembedAsync(KnowledgeItem item)
    {
        var existing = await db.Chunks.Where(c => c.KnowledgeItemId == item.Id).ToListAsync();
        db.Chunks.RemoveRange(existing);

        var ordinal = 0;
        foreach (var text in Chunker.BuildChunks(item.ItemType, item.PayloadJson))
        {
            db.Chunks.Add(new Chunk
            {
                KnowledgeItemId = item.Id,
                TenantId = item.TenantId,
                Ordinal = ordinal++,
                Content = text,
                Embedding = new Vector(embedder.Embed(text)),
                EmbeddingModel = embedder.ModelId
            });
        }
    }
}