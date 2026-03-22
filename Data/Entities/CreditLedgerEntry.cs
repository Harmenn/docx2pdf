namespace Docx2Pdf.Data.Entities;

public sealed class CreditLedgerEntry
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Models.ApplicationUser? User { get; set; }
}
