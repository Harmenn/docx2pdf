namespace Docx2Pdf.Data.Entities;

public sealed class TrackingEvent
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Label { get; set; }
    public string? Value { get; set; }
    public string? MetaJson { get; set; }
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
    public VisitSession? Session { get; set; }
}
