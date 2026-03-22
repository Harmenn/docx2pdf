using System.Text.Json;
using Docx2Pdf.Data;
using Docx2Pdf.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Docx2Pdf.Services.Credits;

public sealed class CreditsService : ICreditsService
{
    private readonly ApplicationDbContext _db;

    public CreditsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<int> GetCreditsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _db.Users.Where(x => x.Id == userId).Select(x => x.Credits).SingleAsync(cancellationToken);
    }

    public Task TopUpAsync(string userId, int amount, string reason, string? referenceType = null, string? referenceId = null, object? metadata = null, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(userId, amount, reason, referenceType, referenceId, metadata, cancellationToken);
    }

    public Task ConsumeAsync(string userId, int amount, string reason, string? referenceType = null, string? referenceId = null, object? metadata = null, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(userId, -amount, reason, referenceType, referenceId, metadata, cancellationToken);
    }

    private async Task ChangeAsync(string userId, int delta, string reason, string? referenceType, string? referenceId, object? metadata, CancellationToken cancellationToken)
    {
        if (delta == 0)
        {
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("Gebruiker niet gevonden.");
        }

        if (user.Credits + delta < 0)
        {
            throw new InvalidOperationException("Onvoldoende credits.");
        }

        user.Credits += delta;
        _db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            UserId = userId,
            Delta = delta,
            Reason = reason,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            MetadataJson = metadata == null ? null : JsonSerializer.Serialize(metadata),
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
