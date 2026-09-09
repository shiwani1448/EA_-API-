using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class SimplePauseRequestDtoValidator : AbstractValidator<SimplePauseRequestDto>
{
    public SimplePauseRequestDtoValidator()
    {
        RuleFor(x => x.Remark).NotEmpty().MaximumLength(2000);
    }
}

public class SimpleResumeRequestDtoValidator : AbstractValidator<SimpleResumeRequestDto>
{
    public SimpleResumeRequestDtoValidator()
    {
        RuleFor(x => x.Remark).MaximumLength(2000);
    }
}

public class MeetingStartRequestDtoValidator : AbstractValidator<MeetingStartRequestDto>
{
    public MeetingStartRequestDtoValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class MeetingPauseRequestDtoValidator : AbstractValidator<MeetingPauseRequestDto>
{
    public MeetingPauseRequestDtoValidator()
    {
        RuleFor(x => x.Remark).NotEmpty().MaximumLength(2000);
    }
}

public class MeetingResumeRequestDtoValidator : AbstractValidator<MeetingResumeRequestDto>
{
    public MeetingResumeRequestDtoValidator()
    {
        RuleFor(x => x.Remark).MaximumLength(2000);
    }
}

public class MeetingCompleteRequestDtoValidator : AbstractValidator<MeetingCompleteRequestDto>
{
    public MeetingCompleteRequestDtoValidator()
    {
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(2000);
    }
}
