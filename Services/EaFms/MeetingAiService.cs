using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Preview-only AI action-point extraction for a completed Meeting's existing evidence
/// (CompletionMom / CompletionPdfAttachmentId). Orchestration only — all provider-specific
/// logic (Claude HTTP/SDK, JSON parsing, PDF/OCR extraction) lives in already-registered
/// shared services; this class never talks to Claude or the filesystem directly.
///
/// Never writes to the database: no MeetingAction, Delegation or EaTask row is created,
/// and Meeting/TAT/completion fields are never touched. The Meeting -> Delegation
/// conversion timing mismatch (MeetingAction->Delegation conversion currently happens
/// during Meeting Complete, while this analyzes evidence after completion) is a known,
/// deliberately deferred concern for the future confirmation phase — this phase stops at
/// returning a proposal for EA review.
/// </summary>
public class MeetingAiService : IMeetingAiService
{
    private readonly EaFmsDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMeetingActionExtractionPromptBuilder _promptBuilder;
    private readonly hrms_api.Services.IDocumentExtractionService _documentExtraction;
    private readonly IMeetingCompletionFileStore _files;
    private readonly ILogger<MeetingAiService> _logger;
    private readonly int _maxAiAttempts;

    // IClaudeClient is resolved lazily via IServiceProvider (not injected directly) so that
    // constructing this service never requires Claude to be configured. IClaudeClient is a
    // singleton whose constructor throws if AnthropicSettings:ApiKey is missing — a direct
    // constructor dependency would make ConfirmActionsAsync (which never calls Claude) fail
    // in any environment without a configured key, discovered via a live local run where
    // POST .../ai/actions/confirm returned 409 "Anthropic API key is missing" purely from
    // being constructed, before its own method body ever ran.
    public MeetingAiService(
        EaFmsDbContext db,
        IServiceProvider serviceProvider,
        IMeetingActionExtractionPromptBuilder promptBuilder,
        hrms_api.Services.IDocumentExtractionService documentExtraction,
        IMeetingCompletionFileStore files,
        ILogger<MeetingAiService> logger,
        IOptions<ClaudeOptions> claudeOptions)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _promptBuilder = promptBuilder;
        _documentExtraction = documentExtraction;
        _files = files;
        _logger = logger;
        _maxAiAttempts = Math.Clamp(claudeOptions.Value.MaxRetries, 1, 3);
    }

    public async Task<MeetingAiAnalysisResponseDto> AnalyzeAsync(long meetingId, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == meetingId && !m.IsDeleted, ct)
            ?? throw new NotFoundException($"Meeting {meetingId} not found.");

        var mom = string.IsNullOrWhiteSpace(meeting.CompletionMom) ? null : meeting.CompletionMom;
        string? pdfText = null;
        string? warning = null;

        if (meeting.CompletionPdfAttachmentId.HasValue)
        {
            var attachment = await _db.Attachments.AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == meeting.CompletionPdfAttachmentId.Value && a.IsActive && !a.IsDeleted, ct);

            if (attachment is null)
            {
                warning = "Completion PDF attachment record is missing.";
            }
            else
            {
                try
                {
                    var fullPath = _files.Resolve(attachment.ObjectKey);
                    var extraction = await _documentExtraction.ExtractAsync(fullPath, ct);

                    if (extraction.Success && !string.IsNullOrWhiteSpace(extraction.Text))
                        pdfText = extraction.Text;
                    else
                        warning = $"Completion PDF could not be read ({extraction.FailureReason ?? "no extractable text"}).";
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to extract text from Meeting {MeetingId} completion PDF.", meetingId);
                    warning = "Completion PDF could not be read.";
                }
            }
        }

        var momUsed = !string.IsNullOrWhiteSpace(mom);
        var pdfUsed = !string.IsNullOrWhiteSpace(pdfText);

        if (!momUsed && !pdfUsed)
        {
            throw new BusinessRuleException(warning is null
                ? "Meeting has no completion minutes or completion PDF to analyze."
                : $"No usable meeting completion evidence to analyze. {warning}");
        }

        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userPrompt = _promptBuilder.BuildUserPrompt(mom, pdfText);

        var aiResult = await GenerateAndParseAsync<MeetingAiExtractionResultDto>(
            systemPrompt, userPrompt, "Meeting action extraction", ct);

        var response = new MeetingAiAnalysisResponseDto
        {
            MeetingId = meetingId,
            ProposedActions = aiResult.ProposedActions
                .Where(a => !string.IsNullOrWhiteSpace(a.Title))
                .Select(a => new MeetingAiProposedActionDto
                {
                    Title = a.Title,
                    Description = a.Description,
                    DoerName = a.DoerName,
                    Priority = a.Priority,
                    DueDate = a.DueDate,
                })
                .ToList(),
            MomUsed = momUsed,
            PdfUsed = pdfUsed,
            WarningMessage = warning,
        };
        await AiSuggestionWriters.LogMeetingActionExtractionAsync(_db, meetingId, response, ct);
        return response;
    }

    public async Task<MeetingAiActionsConfirmResponseDto> ConfirmActionsAsync(
        long meetingId, ConfirmMeetingAiActionsRequestDto dto, string actor, CancellationToken ct = default)
    {
        var meetingExists = await _db.Meetings.AnyAsync(m => m.Id == meetingId && !m.IsDeleted, ct);
        if (!meetingExists)
            throw new NotFoundException($"Meeting {meetingId} not found.");

        var now = Clock.UtcNowTz;
        var created = new List<MeetingAction>();

        // One transaction for the whole batch: either every confirmed action is created or
        // none is (a single SaveChangesAsync call is itself atomic) — no per-action business
        // rule exists at this stage (no doer-existence check, no cross-action uniqueness), so
        // application-level partial failure mid-loop is not reachable; this transaction is
        // defense-in-depth for infrastructure-level failures, same pattern as
        // MeetingLifecycleService.CompleteAsync.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var action in dto.Actions)
            {
                var entity = MeetingActionFactory.Build(meetingId, action, actor, now);
                _db.MeetingActions.Add(entity);
                created.Add(entity);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }

        var response = new MeetingAiActionsConfirmResponseDto
        {
            MeetingId = meetingId,
            CreatedActions = created.Select(a => MeetingActionFactory.ToDto(a, now)).ToList(),
        };
        // Links this confirmation back to the extraction suggestion it came from — if the EA
        // never called /ai/actions/extract first (e.g. typed the actions manually), there is
        // nothing to link and this is a no-op.
        await AiSuggestionWriters.MarkMeetingActionExtractionAppliedAsync(_db, meetingId, response, ct);
        return response;
    }

    // Same retry-on-malformed-JSON pattern as AnalysisService.GenerateAndParseAsync: reuses
    // the shared IClaudeClient + AiJsonResponseParser, retrying only on parse failure (not
    // transport errors), up to the configured AnthropicSettings:MaxRetries (clamped 1-3).
    private async Task<T> GenerateAndParseAsync<T>(
        string systemPrompt,
        string userPrompt,
        string entityName,
        CancellationToken ct) where T : new()
    {
        BusinessRuleException? lastParseError = null;

        for (var attempt = 1; attempt <= _maxAiAttempts; attempt++)
        {
            var attemptPrompt = attempt == 1
                ? userPrompt
                : userPrompt + """

                    IMPORTANT RETRY: The prior response was not valid JSON. Return one complete, compact JSON
                    object only. Do not use markdown fences, comments, smart quotes, trailing commas, or text
                    before/after the object.
                    """;

            var claudeClient = _serviceProvider.GetRequiredService<IClaudeClient>();
            var rawResponse = await claudeClient.GenerateJsonAsync(systemPrompt, attemptPrompt, ct);

            try
            {
                return AiJsonResponseParser.Parse<T>(rawResponse, _logger, entityName);
            }
            catch (BusinessRuleException ex) when (attempt < _maxAiAttempts)
            {
                lastParseError = ex;
                _logger.LogWarning(
                    "AI {Entity} returned invalid JSON on attempt {Attempt}/{MaxAttempts}; retrying.",
                    entityName, attempt, _maxAiAttempts);
            }
        }

        throw lastParseError
            ?? new BusinessRuleException($"AI {entityName} service did not return valid JSON.");
    }
}

/// <summary>Internal shape for parsing Claude's raw JSON response only — never returned
/// from the public API. The public contract is <see cref="MeetingAiAnalysisResponseDto"/>.</summary>
internal class MeetingAiExtractionResultDto
{
    public List<MeetingAiExtractionActionDto> ProposedActions { get; set; } = new();
}

internal class MeetingAiExtractionActionDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? DoerName { get; set; }
    public string? Priority { get; set; }
    public DateTime? DueDate { get; set; }
}
