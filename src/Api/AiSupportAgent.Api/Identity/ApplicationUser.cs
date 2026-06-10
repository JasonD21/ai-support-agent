using Microsoft.AspNetCore.Identity;

namespace AiSupportAgent.Api.Identity;

public class ApplicationUser : IdentityUser   // Id is a string GUID (Identity default)
{
    public string? DisplayName { get; set; }
}