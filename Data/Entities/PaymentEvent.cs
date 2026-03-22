namespace Docx2Pdf.Data.Entities;

public sealed class PaymentEvent
{
    public long Id { get; set; }
    public long? PaymentIntentId { get; set; }
    public string Provider { get; set; } = "mollie";
    public string? EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? RemoteIp { get; set; }
    public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedUtc { get; set; }
    public PaymentIntent? PaymentIntent { get; set; }
}
