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
        // Both fields are optional; a supplied MOM is still limited to the column size, and a supplied PDF is validated by the file store.
        RuleFor(x => x.CompletionMom).MaximumLength(4000);
    }
}
