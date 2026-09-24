using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

// A fresh, disposable database: never migrates or changes the application's database.
public sealed class MeetingDelegationDatabase : IAsyncLifetime
{
    private bool created;
    private readonly string name = "meeting_delegation_test_" + Guid.NewGuid().ToString("N");
    private string AdminConnection => Environment.GetEnvironmentVariable("MEETING_TEST_POSTGRES")
        ?? throw new InvalidOperationException("Set MEETING_TEST_POSTGRES to a local PostgreSQL connection with CREATE DATABASE permission. Tests use disposable databases.");
    public EaFmsDbContext CreateContext()
    {
        var builder = new NpgsqlConnectionStringBuilder(AdminConnection) { Database = name };
        return new(new DbContextOptionsBuilder<EaFmsDbContext>().UseNpgsql(builder.ConnectionString).Options);
    }
    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
        await create.ExecuteNonQueryAsync();
        created = true;
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        db.BusinessModules.AddRange(new BusinessModule { Name = "Meeting", CreatedBy = "test", CreatedDate = DateTime.UtcNow },
            new BusinessModule { Name = "Delegation", CreatedBy = "test", CreatedDate = DateTime.UtcNow });
        db.Statuses.Add(new Status { Name = "In Progress", CreatedBy = "test", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        if (!created) return;
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
