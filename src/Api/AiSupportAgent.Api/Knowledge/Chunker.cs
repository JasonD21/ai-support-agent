using System.Text.Json;

namespace AiSupportAgent.Api.Knowledge;

// Type-aware chunking: mostly one chunk per item, atomic chunks for the business profile,
// length-split only for long policy bodies.
public static class Chunker
{
    private const int MaxPolicyChars = 1200;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> BuildChunks(KnowledgeItemType type, string payloadJson) => type switch
    {
        KnowledgeItemType.Faq => Faq(Read<FaqPayload>(payloadJson)),
        KnowledgeItemType.Service => Service(Read<ServicePayload>(payloadJson)),
        KnowledgeItemType.Policy => Policy(Read<PolicyPayload>(payloadJson)),
        KnowledgeItemType.BusinessProfile => Profile(Read<BusinessProfilePayload>(payloadJson)),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static List<string> Faq(FaqPayload f) => [$"FAQ\nQ: {f.Question}\nA: {f.Answer}"];

    private static List<string> Service(ServicePayload s)
    {
        var price = string.IsNullOrWhiteSpace(s.Price) ? "" : $" Price: {s.Price}.";
        var cat = string.IsNullOrWhiteSpace(s.Category) ? "" : $" Category: {s.Category}.";
        return [$"Service: {s.Name}. {s.Description}.{price}{cat}"];
    }

    private static List<string> Policy(PolicyPayload p)
    {
        if (p.Body.Length <= MaxPolicyChars)
            return [$"Policy [{p.Title}]: {p.Body}"];

        var chunks = new List<string>();
        for (var i = 0; i < p.Body.Length; i += MaxPolicyChars)
            chunks.Add($"Policy [{p.Title}]: {p.Body.Substring(i, Math.Min(MaxPolicyChars, p.Body.Length - i))}");
        return chunks;
    }

    private static List<string> Profile(BusinessProfilePayload b)
    {
        var chunks = new List<string>();
        if (!string.IsNullOrWhiteSpace(b.About)) chunks.Add($"About {b.Name}: {b.About}");
        if (!string.IsNullOrWhiteSpace(b.Hours)) chunks.Add($"{b.Name} hours: {b.Hours}");
        if (!string.IsNullOrWhiteSpace(b.Location)) chunks.Add($"{b.Name} location/service area: {b.Location}");
        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(b.ContactEmail)) contact.Add($"email {b.ContactEmail}");
        if (!string.IsNullOrWhiteSpace(b.ContactPhone)) contact.Add($"phone {b.ContactPhone}");
        if (contact.Count > 0) chunks.Add($"{b.Name} contact: {string.Join(", ", contact)}");
        if (chunks.Count == 0) chunks.Add($"Business: {b.Name}");
        return chunks;
    }

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;
}