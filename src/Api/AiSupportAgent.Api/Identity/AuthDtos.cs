namespace AiSupportAgent.Api.Identity;

public record RegisterRequest(
    string Email,
    string Password,
    string BusinessName,
    string? DisplayName
);
public record LoginRequest(
    string Email,
    string Password
);
public record UserDto(
    string Email,
    string? DisplayName
);
public record TenantDto(
    Guid Id,
    string Name,
    string SiteKey
);
public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserDto User,
    TenantDto Tenant
);
public record MeResponse(
    string Email,
    string? DisplayName,
    Guid TenantId,
    string TenantName,
    string SiteKey
);
