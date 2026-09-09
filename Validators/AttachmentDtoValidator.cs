using FluentValidation;
using Jarvis5.Dtos;

namespace Jarvis5.Validators;

public class AttachmentDtoValidator : AbstractValidator<AttachmentDto>
{
    public AttachmentDtoValidator()
    {
        RuleFor(a => a.FileName)
            .NotEmpty()
            .When(a => string.IsNullOrEmpty(a.Base64Content))
            .WithMessage("Attachment file name is required.");
        RuleFor(a => a.FileUrl)
            .NotEmpty()
            .When(a => string.IsNullOrEmpty(a.Base64Content))
            .WithMessage("Attachment file URL is required.");
        RuleFor(a => a.Base64Content)
            .NotEmpty()
            .When(a => string.IsNullOrEmpty(a.FileUrl))
            .WithMessage("Attachment content (base64) or file URL is required.");
    }
}
