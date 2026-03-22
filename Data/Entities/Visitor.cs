namespace Docx2Pdf.Data.Entities;

public sealed class Visitor
{
    public Guid VisitorId { get; set; }
    public string? FirstPath { get; set; }
    public string? EntryReferrer { get; set; }
    public string? UserAgent { get; set; }
    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public List<VisitSession> Sessions { get; set; } = [];
    public List<VisitorUserLink> UserLinks { get; set; } = [];
}
