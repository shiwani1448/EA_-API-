using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class MeetingDoerDtoValidator : AbstractValidator<MeetingDoerDto>
{
    public MeetingDoerDtoValidator()
    {
        // Frontend-owned form requiredness — DoerId/DoerName are optional; only DB
        // column length is guarded here.
        RuleFor(x => x.DoerId).MaximumLength(100);
        RuleFor(x => x.DoerName).MaximumLength(200);
    }
}
