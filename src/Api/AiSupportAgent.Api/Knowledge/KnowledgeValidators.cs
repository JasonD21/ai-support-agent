using FluentValidation;

namespace AiSupportAgent.Api.Knowledge;

public class FaqValidator : AbstractValidator<FaqPayload>
{
    public FaqValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(4000);
    }
}
public class ServiceValidator : AbstractValidator<ServicePayload>
{
    public ServiceValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Price).MaximumLength(100);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}
public class PolicyValidator : AbstractValidator<PolicyPayload>
{
    public PolicyValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(20000);
    }
}
public class BusinessProfileValidator : AbstractValidator<BusinessProfilePayload>
{
    public BusinessProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}