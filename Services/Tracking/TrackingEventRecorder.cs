using System.Text.Json;
using Docx2Pdf.Data;
using Docx2Pdf.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Docx2Pdf.Services.Tracking;

public sealed class TrackingEventRecorder : ITrackingEventRecorder
{
    private readonly ApplicationDbContext _db;

    public TrackingEventRecorder(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(HttpContext context, string type, string? label = null, string? value = null, object? meta = null, CancellationToken cancellationToken = default)
    {
        if (!context.Request.Cookies.TryGetValue("sid", out var sidRaw) || !Guid.TryParse(sidRaw, out var sessionId))
        {
            return;
        }

        var session = await _db.VisitSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
        if (session == null)
        {
            return;
        }

        _db.TrackingEvents.Add(new TrackingEvent
        {
            SessionId = sessionId,
            Type = type,
            Path = context.Request.Path,
            Label = label,
            Value = value,
            MetaJson = meta == null ? null : JsonSerializer.Serialize(meta),
            OccurredUtc = DateTime.UtcNow
        });
        session.EventCount += 1;
        session.EndedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
