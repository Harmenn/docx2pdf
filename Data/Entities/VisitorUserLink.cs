namespace Docx2Pdf.Data.Entities;

public sealed class VisitorUserLink
{
    public Guid VisitorId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime LinkedUtc { get; set; } = DateTime.UtcNow;
    public Visitor? Visitor { get; set; }
    public Models.ApplicationUser? User { get; set; }
}
