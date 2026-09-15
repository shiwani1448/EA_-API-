using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;
namespace Jarvis5.Common.EaFms;
public static class TatClassification
{
    // Mapped explicitly so EF does not substitute its broader btrim whitespace overload.
    public static string TrimForMatch(string value) => throw new NotSupportedException("SQL only");

    // Use PostgreSQL's collation/case rules for both parameters and persisted values.
    public static Task<string> NormalizeAsync(EaFmsDbContext db, string value, CancellationToken ct) =>
        db.Database.SqlQuery<string>($"SELECT lower(btrim({value})) AS \"Value\"").SingleAsync(ct);
}
