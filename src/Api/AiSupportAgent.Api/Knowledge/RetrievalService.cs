using System.Text.Json;
using AiSupportAgent.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AiSupportAgent.Api.Knowledge;

public record RetrievedChunk(
    Guid ChunkId,
    string Content,
    double Similarity,
    string Title
);

public class RetrievalService(AppDbContext db, IEmbedder embedder)
{
    private const int TopK = 5;

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, CancellationToken ct)
    {
        var q = new Vector(embedder.Embed(query));

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Iterative scan keeps a small tenant from being starved by the post-scan tenant filter.
        // Set LOCAL so it's scoped to this transaction (safe behind Neon's connection pooler).
        await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.iterative_scan = 'relaxed_order';", ct);

        var rows = await db.Chunks
            .Select(c => new { c.Id, c.Content, c.KnowledgeItemId, Distance = c.Embedding.CosineDistance(q) })
            .OrderBy(x => x.Distance)
            .Take(TopK)
            .ToListAsync(ct);

        if (rows.Count == 0) return [];

        var itemIds = rows.Select(r => r.KnowledgeItemId).Distinct().ToList();
        var items = await db.KnowledgeItems
            .Where(k => itemIds.Contains(k.Id))
            .Select(k => new { k.Id, k.ItemType, k.PayloadJson })
            .ToDictionaryAsync(k => k.Id, ct);

        await tx.CommitAsync(ct);

        return [.. rows.Select(r =>
        {
            var item = items.GetValueOrDefault(r.KnowledgeItemId);
            var title = item is null ? "" : TitleFor(item.ItemType, item.PayloadJson);
            return new RetrievedChunk(r.Id, r.Content, 1.0 - r.Distance, title);
        })];
    }

    private static string TitleFor(KnowledgeItemType type, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string? Get(string p) => root.TryGetProperty(p, out var v) ? v.GetString() : null;
        return type switch
        {
            KnowledgeItemType.Faq => Get("question") ?? "FAQ",
            KnowledgeItemType.Service => Get("name") ?? "Service",
            KnowledgeItemType.Policy => Get("title") ?? "Policy",
            KnowledgeItemType.BusinessProfile => "Business profile",
            _ => "Knowledge"
        };
    }
}