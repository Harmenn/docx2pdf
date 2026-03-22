namespace Docx2Pdf.Data.Entities;

public sealed class ConversionJob
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredInputPath { get; set; } = string.Empty;
    public string? StoredOutputPath { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public bool CreditCharged { get; set; }
    public int CreditsCharged { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public Models.ApplicationUser? User { get; set; }
}
