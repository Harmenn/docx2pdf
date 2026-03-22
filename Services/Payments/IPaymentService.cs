using Microsoft.AspNetCore.Http;

namespace Docx2Pdf.Services.Payments;

public interface IPaymentService
{
    CreditPurchaseQuote CalculateQuote(int credits);
    Task<PaymentCheckoutResult> CreateCheckoutAsync(string userId, int credits, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task ProcessWebhookAsync(string body, IHeaderDictionary headers, string? remoteIp, CancellationToken cancellationToken = default);
    Task<PaymentIntentResult?> RefreshIntentAsync(long intentId, string userId, CancellationToken cancellationToken = default);
    Task<InvoiceDocumentResult?> GetInvoiceAsync(long intentId, string userId, CancellationToken cancellationToken = default);
}

public sealed class PaymentCheckoutResult
{
    public long IntentId { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string Status { get; set; } = PaymentIntentStatuses.Created;
}

public sealed class PaymentIntentResult
{
    public long IntentId { get; set; }
    public string Status { get; set; } = PaymentIntentStatuses.Created;
    public bool CreditsApplied { get; set; }
    public int CurrentCredits { get; set; }
    public string ProviderPaymentId { get; set; } = string.Empty;
    public CreditPurchaseQuote Quote { get; set; } = new();
}

public sealed class InvoiceDocumentResult
{
    public long IntentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/html; charset=utf-8";
    public byte[] Content { get; set; } = [];
    public string InvoiceNumber { get; set; } = string.Empty;
}
