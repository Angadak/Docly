using Docly.Domain.Entities;
using Pgvector.EntityFrameworkCore;
using Docly.Infra.DbContexts;
using Docly.Infra.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Docly.Infra.Tests;

/// <summary>Runs only when DOCLY_TEST_DB points at a disposable PostgreSQL database with pgvector available.</summary>
public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "DOCLY_TEST_DB";

    public RequiresDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
            Skip = $"Set {EnvironmentVariable} to a PostgreSQL connection string (e.g. Host=localhost;Database=docly_test;Username=postgres;Password=...) to run database tests.";
    }
}

public class DatabaseTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable(RequiresDatabaseFactAttribute.EnvironmentVariable), o => o.UseVector())
            .Options;
        return new AppDbContext(options);
    }

    [RequiresDatabaseFact]
    public async Task Migrations_apply_and_docx_document_round_trips()
    {
        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        const string docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        var user = new User($"{Guid.NewGuid():N}@test.local", "hash");
        var document = new Document("Round trip", "a.docx", $"{Guid.NewGuid()}.docx", docx, 1024, user.Id);

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            await new DocumentRepository(write).AddAsync(document);
            await write.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var loaded = await new DocumentRepository(read).GetByIdAsync(document.Id, user.Id);

        Assert.NotNull(loaded);
        Assert.Equal(docx, loaded!.ContentType);
        Assert.Null(await new DocumentRepository(read).GetByIdAsync(document.Id, Guid.NewGuid())); // other users can't see it

        read.Documents.Remove(loaded);
        read.Users.Remove(await read.Users.SingleAsync(u => u.Id == user.Id));
        await read.SaveChangesAsync();
    }
}
