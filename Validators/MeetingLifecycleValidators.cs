using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class SimplePauseRequestDtoValidator : AbstractValidator<SimplePauseRequestDto>
{
    public SimplePauseRequestDtoValidator()
    {
        RuleFor(x => x.Remark).MaximumLength(2000);
    }
}

public class SimpleResumeRequestDtoValidator : AbstractValidator<SimpleResumeRequestDto>
{
    public SimpleResumeRequestDtoValidator()
    {
        RuleFor(x => x.Remark).MaximumLength(2000);
    }
}

public class MeetingCompleteRequestDtoValidator : AbstractValidator<MeetingCompleteRequestDto>
{
    public MeetingCompleteRequestDtoValidator()
    {
        RuleFor(x => x.CompletionMom).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.CompletionPdf).NotNull();
    }
}
