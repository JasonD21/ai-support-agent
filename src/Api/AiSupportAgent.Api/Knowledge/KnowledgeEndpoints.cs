using System.Text.Json;
using System.Text.Json.Serialization;
using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Persistence;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace AiSupportAgent.Api.Knowledge;

public static class KnowledgeEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge").RequireAuthorization().WithTags("Knowledge");
        group.MapGet("/types", GetTypes);
        group.MapGet("", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        return app;
    }

    private static IResult GetTypes() => Results.Ok(new[]
    {
        new KnowledgeTypeDescriptor("Faq", "FAQ", false,
        [
            new("question", "Question", "text", true),
            new("answer", "Answer", "textarea", true),
        ]),
        new KnowledgeTypeDescriptor("Service", "Service / Product", false,
        [
            new("name", "Name", "text", true),
            new("description", "Description", "textarea", true),
            new("price", "Price", "text", false),
            new("category", "Category", "text", false),
        ]),
        new KnowledgeTypeDescriptor("Policy", "Policy", false,
        [
            new("title", "Title", "text", true),
            new("body", "Body", "textarea", true),
        ]),
        new KnowledgeTypeDescriptor("BusinessProfile", "Business profile", true,
        [
            new("name", "Business name", "text", true),
            new("about", "About", "textarea", false),
            new("hours", "Hours", "text", false),
            new("location", "Location / service area", "text", false),
            new("contactEmail", "Contact email", "text", false),
            new("contactPhone", "Contact phone", "text", false),
        ]),
    });

    private static async Task<IResult> List(AppDbContext db, KnowledgeItemType? type, int offset = 0, int limit = 50)
    {
        var q = db.KnowledgeItems.AsQueryable();
        if (type is { } t) q = q.Where(k => k.ItemType == t);
        var items = await q.OrderByDescending(k => k.CreatedAt).Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 100)).ToListAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> Get(Guid id, AppDbContext db)
    {
        var item = await db.KnowledgeItems.FirstOrDefaultAsync(k => k.Id == id);
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    private static async Task<IResult> Create(UpsertKnowledgeRequest req, AppDbContext db, KnowledgeService svc, ITenantContext tenantCtx)
    {
        if (tenantCtx.TenantId is not { } tenantId) return Results.Unauthorized();

        if (req.ItemType == KnowledgeItemType.BusinessProfile &&
            await db.KnowledgeItems.AnyAsync(k => k.ItemType == KnowledgeItemType.BusinessProfile))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            { ["businessProfile"] = ["A business profile already exists — edit it instead."] });

        var (payloadJson, errors) = ValidatePayload(req.ItemType, req.Payload);
        if (errors is not null) return Results.ValidationProblem(errors);

        var item = new KnowledgeItem { TenantId = tenantId, ItemType = req.ItemType, PayloadJson = payloadJson! };
        db.KnowledgeItems.Add(item);
        await svc.ReembedAsync(item);
        await db.SaveChangesAsync();
        return Results.Created($"/api/knowledge/{item.Id}", ToDto(item));
    }

    private static async Task<IResult> Update(Guid id, UpsertKnowledgeRequest req, AppDbContext db, KnowledgeService svc)
    {
        var item = await db.KnowledgeItems.FirstOrDefaultAsync(k => k.Id == id);
        if (item is null) return Results.NotFound();
        if (item.ItemType != req.ItemType)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["itemType"] = ["Type can't be changed."] });

        var (payloadJson, errors) = ValidatePayload(req.ItemType, req.Payload);
        if (errors is not null) return Results.ValidationProblem(errors);

        item.PayloadJson = payloadJson!;
        item.UpdatedAt = DateTime.UtcNow;
        await svc.ReembedAsync(item);
        await db.SaveChangesAsync();
        return Results.Ok(ToDto(item));
    }

    private static async Task<IResult> Delete(Guid id, AppDbContext db)
    {
        var item = await db.KnowledgeItems.FirstOrDefaultAsync(k => k.Id == id);
        if (item is null) return Results.NotFound();
        db.KnowledgeItems.Remove(item);     // cascades chunks
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // Deserialize the polymorphic payload into the right record, validate, re-serialize to canonical JSON.
    private static (string? json, IDictionary<string, string[]>? errors) ValidatePayload(KnowledgeItemType type, JsonElement payload)
    {
        try
        {
            switch (type)
            {
                case KnowledgeItemType.Faq:
                    var f = payload.Deserialize<FaqPayload>(Json)!;
                    return Check(new FaqValidator().Validate(f)) ?? (JsonSerializer.Serialize(f, Json), null);
                case KnowledgeItemType.Service:
                    var s = payload.Deserialize<ServicePayload>(Json)!;
                    return Check(new ServiceValidator().Validate(s)) ?? (JsonSerializer.Serialize(s, Json), null);
                case KnowledgeItemType.Policy:
                    var p = payload.Deserialize<PolicyPayload>(Json)!;
                    return Check(new PolicyValidator().Validate(p)) ?? (JsonSerializer.Serialize(p, Json), null);
                case KnowledgeItemType.BusinessProfile:
                    var b = payload.Deserialize<BusinessProfilePayload>(Json)!;
                    return Check(new BusinessProfileValidator().Validate(b)) ?? (JsonSerializer.Serialize(b, Json), null);
                default:
                    return (null, new Dictionary<string, string[]> { ["itemType"] = ["Unknown type."] });
            }
        }
        catch (JsonException)
        {
            return (null, new Dictionary<string, string[]> { ["payload"] = ["Payload does not match the item type."] });
        }
    }

    private static (string?, IDictionary<string, string[]>?)? Check(ValidationResult r) =>
        r.IsValid ? null : (null, r.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            as IDictionary<string, string[]>);

    private static KnowledgeItemDto ToDto(KnowledgeItem k) =>
        new(k.Id, k.ItemType, JsonDocument.Parse(k.PayloadJson).RootElement.Clone(), k.CreatedAt, k.UpdatedAt);
}