using System.Text.Json;

namespace AiSupportAgent.Api.Knowledge;

public record UpsertKnowledgeRequest(
    KnowledgeItemType ItemType,
    JsonElement Payload
);
public record KnowledgeItemDto(
    Guid Id,
    KnowledgeItemType ItemType,
    JsonElement Payload,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record FieldDescriptor(
    string Name,
    string Label,
    string Kind,
    bool Required
);
public record KnowledgeTypeDescriptor(
    string Type,
    string Label,
    bool Singleton,
    FieldDescriptor[] Fields
);