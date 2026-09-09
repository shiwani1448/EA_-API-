using FluentValidation;
using Jarvis5.Dtos.Analysis;

namespace Jarvis5.Validators;

public class SaveAnalysisDtoValidator : AbstractValidator<SaveAnalysisDto>
{
    public SaveAnalysisDtoValidator()
    {
        RuleFor(d => d.Analysis).NotNull().WithMessage("Analysis is required.");
        RuleFor(d => d.Analysis).SetValidator(new AiAnalysisResultDtoValidator()).When(d => d.Analysis is not null);
    }
}

public class AiAnalysisResultDtoValidator : AbstractValidator<AiAnalysisResultDto>
{
    private static readonly string[] AllowedPriorities = { "Critical", "High", "Medium", "Low" };

    public AiAnalysisResultDtoValidator()
    {
        RuleFor(a => a.ExecutiveSummary).NotEmpty().WithMessage("Executive summary is required.");
        RuleFor(a => a.CurrentBusinessProcess.Description)
            .NotEmpty().WithMessage("Current business process description is required.");

        RuleForEach(a => a.Bottlenecks).ChildRules(b =>
        {
            b.RuleFor(x => x.Title).NotEmpty().WithMessage("Bottleneck title is required.");
            b.RuleFor(x => x.Description).NotEmpty().WithMessage("Bottleneck description is required.");
        });

        RuleForEach(a => a.RootCauses).ChildRules(r =>
        {
            r.RuleFor(x => x.Title).NotEmpty().WithMessage("Root cause title is required.");
        });

        RuleForEach(a => a.ImpactPriority).ChildRules(i =>
        {
            i.RuleFor(x => x.Priority)
                .Must(p => AllowedPriorities.Contains(p, StringComparer.OrdinalIgnoreCase))
                .WithMessage($"Impact priority must be one of: {string.Join(", ", AllowedPriorities)}.");
        });
    }
}
