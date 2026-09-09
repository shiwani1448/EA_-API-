using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Dtos.Attachment;

namespace Jarvis5.Validators;

public class UploadAttachmentRequestDtoValidator : AbstractValidator<UploadAttachmentRequestDto>
{
    public UploadAttachmentRequestDtoValidator()
    {
        RuleFor(d => d.EntityType)
            .NotEmpty().WithMessage("Entity type is required.")
            .Must(t => SCIHAttachmentEntityType.Allowed.Contains(t.Trim().ToUpperInvariant()))
            .WithMessage($"Entity type must be one of: {string.Join(", ", SCIHAttachmentEntityType.Allowed)}.");

        RuleFor(d => d.EntityId)
            .GreaterThan(0).WithMessage("A valid entity id is required.");

        RuleFor(d => d.UploadedBy)
            .GreaterThan(0).WithMessage("UploadedBy is required.");

        RuleFor(d => d.Files)
            .NotEmpty().WithMessage("At least one file is required.");

        RuleForEach(d => d.Files).SetValidator(new AttachmentFileDtoValidator());
    }
}

public class AttachmentFileDtoValidator : AbstractValidator<AttachmentFileDto>
{
    private static readonly string[] AllowedExtensions =
        { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

    public AttachmentFileDtoValidator()
    {
        RuleFor(f => f.OriginalName)
            .NotEmpty().WithMessage("File name is required.")
            .Must(HaveAllowedExtension)
            .WithMessage($"Only image and PDF files are allowed ({string.Join(", ", AllowedExtensions)}).");

        RuleFor(f => f.Base64Content)
            .NotEmpty().WithMessage("File content (base64) is required.");
    }

    private static bool HaveAllowedExtension(string originalName) =>
        !string.IsNullOrWhiteSpace(originalName) &&
        AllowedExtensions.Contains(Path.GetExtension(originalName).ToLowerInvariant());
}
