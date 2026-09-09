using FluentValidation;
using Jarvis5.Dtos.SolutionDesign;

namespace Jarvis5.Validators;

public class UpdateSolutionDesignDtoValidator : AbstractValidator<UpdateSolutionDesignDto>
{
    public UpdateSolutionDesignDtoValidator()
    {
        RuleForEach(d => d.Attachments).SetValidator(new AttachmentDtoValidator());
    }
}
