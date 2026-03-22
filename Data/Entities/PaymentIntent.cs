namespace Docx2Pdf.Data.Entities;

public sealed class PaymentIntent
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Provider { get; set; } = "mollie";
    public string Status { get; set; } = "created";
    public string Currency { get; set; } = "EUR";
    public int AmountCents { get; set; }
    public int Credits { get; set; }
    public string? ProductCode { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? ProviderReference { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? ReturnUrl { get; set; }
    public string? WebhookUrl { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? RequestedIp { get; set; }
    public string? UserAgent { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public bool CreditsApplied { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidUtc { get; set; }
    public DateTime? CreditedUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Models.ApplicationUser? User { get; set; }
    public List<PaymentEvent> Events { get; set; } = [];
}
