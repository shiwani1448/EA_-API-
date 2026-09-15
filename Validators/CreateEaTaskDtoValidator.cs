using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateEaTaskDtoValidator : AbstractValidator<CreateEaTaskDto>
{
    public CreateEaTaskDtoValidator()
    {
        RuleFor(x => x.ModuleId).GreaterThan(0);
        RuleFor(x => x.BusinessRecordId).MaximumLength(200);
        RuleFor(x => x.Task).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.WorkflowInstanceId).GreaterThan(0).When(x => x.WorkflowInstanceId.HasValue);
    }
}
