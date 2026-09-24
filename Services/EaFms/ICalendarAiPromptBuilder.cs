namespace Jarvis5.Services.EaFms;

public interface ICalendarAiPromptBuilder
{
    string BuildQuickAddSystemPrompt();
    string BuildQuickAddUserPrompt(string text, DateTime nowLocal);

    string BuildConflictSummarySystemPrompt();
    string BuildConflictSummaryUserPrompt(DateTime from, DateTime to, List<(string FirstTitle, string SecondTitle, string OverlapDescription)> conflicts);
}
