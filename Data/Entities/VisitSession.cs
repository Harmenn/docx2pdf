namespace Docx2Pdf.Data.Entities;

public sealed class VisitSession
{
    public Guid SessionId { get; set; }
    public Guid VisitorId { get; set; }
    public string? IpAddress { get; set; }
    public string? LandingPath { get; set; }
    public bool IsBot { get; set; }
    public int PageCount { get; set; }
    public int EventCount { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime EndedUtc { get; set; } = DateTime.UtcNow;
    public Visitor? Visitor { get; set; }
    public List<TrackingEvent> Events { get; set; } = [];
}
