using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Docx2Pdf.Data;
using Docx2Pdf.Data.Entities;
using Docx2Pdf.Options;
using Docx2Pdf.Services.Credits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Docx2Pdf.Services.Payments;

public sealed class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly PaymentsOptions _paymentsOptions;
    private readonly MollieOptions _mollieOptions;
    private readonly SiteOptions _siteOptions;
    private readonly BillingCalculator _billing;
    private readonly HttpClient _httpClient;
    private readonly ICreditsService _credits;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        ApplicationDbContext db,
        IOptions<PaymentsOptions> paymentsOptions,
        IOptions<MollieOptions> mollieOptions,
        IOptions<SiteOptions> siteOptions,
        ICreditsService credits,
        HttpClient httpClient,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _paymentsOptions = paymentsOptions.Value;
        _mollieOptions = mollieOptions.Value;
        _siteOptions = siteOptions.Value;
        _credits = credits;
        _httpClient = httpClient;
        _logger = logger;
        _billing = new BillingCalculator(_paymentsOptions);
    }

    public CreditPurchaseQuote CalculateQuote(int credits) => _billing.CalculateQuote(credits);

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(string userId, int credits, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        EnsurePaymentsEnabled();
        EnsureMollieConfigured();

        var quote = _billing.CalculateQuote(credits);
        var intent = new PaymentIntent
        {
            UserId = userId,
            Provider = "mollie",
            Status = PaymentIntentStatuses.Created,
            Currency = quote.Currency,
            AmountCents = quote.TotalInclVatCents,
            Credits = quote.Credits,
            ProductCode = "dynamic",
            ReturnUrl = BuildReturnUrl(),
            WebhookUrl = BuildWebhookUrl(),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestedIp = ipAddress,
            UserAgent = userAgent,
            MetadataJson = JsonSerializer.Serialize(new PaymentIntentMetadata
            {
                Credits = quote.Credits,
                UnitPriceExVat = quote.UnitPriceExVat,
                SubtotalExVatCents = quote.SubtotalExVatCents,
                VatRate = quote.VatRate,
                VatAmountCents = quote.VatAmountCents,
                TotalInclVatCents = quote.TotalInclVatCents,
                DiscountPercent = quote.DiscountPercent
            }),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _db.PaymentIntents.Add(intent);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["amount"] = new { currency = quote.Currency, value = quote.TotalInclVat.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) },
                ["description"] = $"{quote.Credits} Docx2Pdf credits",
                ["redirectUrl"] = $"{intent.ReturnUrl}?intentId={intent.Id}",
                ["metadata"] = new { intentId = intent.Id, userId, credits = quote.Credits }
            };

            if (!string.IsNullOrWhiteSpace(intent.WebhookUrl))
            {
                payload["webhookUrl"] = intent.WebhookUrl;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_mollieOptions.ApiBaseUrl.TrimEnd('/')}/payments");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _mollieOptions.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Mollie checkout kon niet worden gestart: {raw}");
            }

            var payment = ParseMolliePayment(raw);
            intent.ProviderPaymentId = payment.Id;
            intent.ProviderReference = payment.CheckoutUrl;
            intent.CheckoutUrl = payment.CheckoutUrl;
            intent.Status = MapStatus(payment.Status);
            intent.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return new PaymentCheckoutResult { IntentId = intent.Id, CheckoutUrl = intent.CheckoutUrl ?? string.Empty, Status = intent.Status };
        }
        catch (Exception ex)
        {
            intent.Status = PaymentIntentStatuses.Failed;
            intent.ErrorCode = "MollieCreateFailed";
            intent.ErrorMessage = ex.Message;
            intent.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex, "Checkout creation failed for intent {IntentId}", intent.Id);
            throw;
        }
    }

    public async Task ProcessWebhookAsync(string body, IHeaderDictionary headers, string? remoteIp, CancellationToken cancellationToken = default)
    {
        var providerPaymentId = ExtractProviderPaymentId(body, headers);
        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            _db.PaymentEvents.Add(new PaymentEvent
            {
                Provider = "mollie",
                EventType = "webhook_invalid",
                Status = PaymentIntentStatuses.Failed,
                IsValid = false,
                Message = "Webhook body bevat geen Mollie payment id.",
                PayloadJson = JsonSerializer.Serialize(new { body }),
                RemoteIp = remoteIp,
                ReceivedUtc = DateTime.UtcNow,
                ProcessedUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var payment = await GetMolliePaymentAsync(providerPaymentId, cancellationToken);
        var intent = await _db.PaymentIntents.SingleOrDefaultAsync(x => x.Provider == "mollie" && x.ProviderPaymentId == providerPaymentId, cancellationToken);
        await AddPaymentEventIfMissingAsync(intent?.Id, $"{payment.Id}:payment.status", "payment.status", payment.Status, payment.RawJson, remoteIp, cancellationToken);

        if (intent == null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        intent.Status = MapStatus(payment.Status);
        intent.UpdatedUtc = DateTime.UtcNow;
        if (intent.Status == PaymentIntentStatuses.Paid && intent.PaidUtc == null)
        {
            intent.PaidUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await EnsureCreditsAppliedAsync(intent, cancellationToken);
    }

    public async Task<PaymentIntentResult?> RefreshIntentAsync(long intentId, string userId, CancellationToken cancellationToken = default)
    {
        var intent = await _db.PaymentIntents.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == intentId && x.UserId == userId, cancellationToken);
        if (intent == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(intent.ProviderPaymentId))
        {
            var payment = await GetMolliePaymentAsync(intent.ProviderPaymentId, cancellationToken);
            await AddPaymentEventIfMissingAsync(intent.Id, $"{payment.Id}:return.status_check", "return.status_check", payment.Status, payment.RawJson, null, cancellationToken);
            intent.Status = MapStatus(payment.Status);
            intent.UpdatedUtc = DateTime.UtcNow;
            if (intent.Status == PaymentIntentStatuses.Paid && intent.PaidUtc == null)
            {
                intent.PaidUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await EnsureCreditsAppliedAsync(intent, cancellationToken);
        }

        return new PaymentIntentResult
        {
            IntentId = intent.Id,
            Status = intent.Status,
            CreditsApplied = intent.CreditsApplied,
            CurrentCredits = await _credits.GetCreditsAsync(userId, cancellationToken),
            ProviderPaymentId = intent.ProviderPaymentId ?? string.Empty,
            Quote = ReadQuoteFromIntent(intent)
        };
    }

    public async Task<InvoiceDocumentResult?> GetInvoiceAsync(long intentId, string userId, CancellationToken cancellationToken = default)
    {
        var intent = await _db.PaymentIntents.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == intentId && x.UserId == userId, cancellationToken);
        if (intent == null)
        {
            return null;
        }

        var quote = ReadQuoteFromIntent(intent);
        var invoiceNumber = $"D2P-{intent.Id:D6}";
        var html = $"""
<!DOCTYPE html>
<html lang="nl">
<head><meta charset="utf-8"><title>Factuur {invoiceNumber}</title></head>
<body style="font-family:Segoe UI,Arial,sans-serif;padding:32px;color:#122033">
<h1>Factuur {invoiceNumber}</h1>
<p><strong>Product:</strong> Docx2Pdf credits</p>
<p><strong>Gebruiker:</strong> {intent.User?.Email ?? intent.UserId}</p>
<p><strong>Credits:</strong> {quote.Credits}</p>
<p><strong>Subtotaal excl. btw:</strong> EUR {quote.SubtotalExVat:N2}</p>
<p><strong>Btw:</strong> EUR {quote.VatAmount:N2}</p>
<p><strong>Totaal incl. btw:</strong> EUR {quote.TotalInclVat:N2}</p>
</body>
</html>
""";

        return new InvoiceDocumentResult
        {
            IntentId = intent.Id,
            InvoiceNumber = invoiceNumber,
            FileName = $"invoice-{invoiceNumber.ToLowerInvariant()}.html",
            ContentType = "text/html; charset=utf-8",
            Content = Encoding.UTF8.GetBytes(html)
        };
    }

    private async Task EnsureCreditsAppliedAsync(PaymentIntent intent, CancellationToken cancellationToken)
    {
        if (intent.Status != PaymentIntentStatuses.Paid || intent.CreditsApplied)
        {
            return;
        }

        await _credits.TopUpAsync(intent.UserId, intent.Credits, "mollie_credit_purchase", "payment_intent", intent.Id.ToString(), new { intent.ProviderPaymentId, intent.AmountCents, intent.Currency }, cancellationToken);
        intent.CreditsApplied = true;
        intent.CreditedUtc = DateTime.UtcNow;
        intent.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<MolliePayment> GetMolliePaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        EnsurePaymentsEnabled();
        EnsureMollieConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_mollieOptions.ApiBaseUrl.TrimEnd('/')}/payments/{paymentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _mollieOptions.ApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Mollie statuscontrole mislukte: {raw}");
        }

        return ParseMolliePayment(raw);
    }

    private async Task AddPaymentEventIfMissingAsync(long? paymentIntentId, string eventId, string eventType, string status, string payload, string? remoteIp, CancellationToken cancellationToken)
    {
        if (await _db.PaymentEvents.AnyAsync(x => x.Provider == "mollie" && x.EventId == eventId, cancellationToken))
        {
            return;
        }

        _db.PaymentEvents.Add(new PaymentEvent
        {
            PaymentIntentId = paymentIntentId,
            Provider = "mollie",
            EventId = eventId,
            EventType = eventType,
            Status = status,
            IsValid = true,
            PayloadJson = payload,
            RemoteIp = remoteIp,
            ReceivedUtc = DateTime.UtcNow,
            ProcessedUtc = DateTime.UtcNow
        });
    }

    private CreditPurchaseQuote ReadQuoteFromIntent(PaymentIntent intent)
    {
        var metadata = JsonSerializer.Deserialize<PaymentIntentMetadata>(intent.MetadataJson) ?? new PaymentIntentMetadata();
        return new CreditPurchaseQuote
        {
            Credits = intent.Credits,
            Currency = intent.Currency,
            UnitPriceExVat = metadata.UnitPriceExVat,
            SubtotalExVat = metadata.SubtotalExVatCents / 100m,
            VatRate = metadata.VatRate,
            VatAmount = metadata.VatAmountCents / 100m,
            TotalInclVat = metadata.TotalInclVatCents / 100m,
            DiscountPercent = metadata.DiscountPercent
        };
    }

    private void EnsurePaymentsEnabled()
    {
        if (!_paymentsOptions.Enabled)
        {
            throw new InvalidOperationException("Betalingen staan uit.");
        }
    }

    private void EnsureMollieConfigured()
    {
        if (string.IsNullOrWhiteSpace(_mollieOptions.ApiKey))
        {
            throw new InvalidOperationException("Mollie is nog niet volledig geconfigureerd.");
        }
    }

    private string BuildReturnUrl()
    {
        var path = _paymentsOptions.ReturnPath.StartsWith('/') ? _paymentsOptions.ReturnPath : "/" + _paymentsOptions.ReturnPath;
        return _siteOptions.PublicBaseUrl.TrimEnd('/') + path;
    }

    private string? BuildWebhookUrl()
    {
        if (!Uri.TryCreate(_siteOptions.PublicBaseUrl, UriKind.Absolute, out var uri) || uri.IsLoopback)
        {
            return null;
        }

        var path = _paymentsOptions.WebhookPath.StartsWith('/') ? _paymentsOptions.WebhookPath : "/" + _paymentsOptions.WebhookPath;
        return _siteOptions.PublicBaseUrl.TrimEnd('/') + path;
    }

    private static string ExtractProviderPaymentId(string body, IHeaderDictionary headers)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            if (!body.Contains('=') && body.StartsWith("tr_", StringComparison.OrdinalIgnoreCase))
            {
                return body.Trim();
            }

            var parsed = QueryHelpers.ParseQuery(body.StartsWith('?') ? body : "?" + body);
            if (parsed.TryGetValue("id", out var id))
            {
                return id.ToString();
            }
        }

        return headers.TryGetValue("X-Mollie-Payment-Id", out var value) ? value.ToString() : string.Empty;
    }

    private static string MapStatus(string? providerStatus) => providerStatus?.ToLowerInvariant() switch
    {
        "paid" => PaymentIntentStatuses.Paid,
        "open" => PaymentIntentStatuses.Pending,
        "pending" => PaymentIntentStatuses.Pending,
        "authorized" => PaymentIntentStatuses.Pending,
        "failed" => PaymentIntentStatuses.Failed,
        "canceled" => PaymentIntentStatuses.Canceled,
        "cancelled" => PaymentIntentStatuses.Canceled,
        "expired" => PaymentIntentStatuses.Expired,
        _ => PaymentIntentStatuses.Pending
    };

    private static MolliePayment ParseMolliePayment(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        var checkoutUrl = string.Empty;
        if (root.TryGetProperty("_links", out var links) &&
            links.TryGetProperty("checkout", out var checkout) &&
            checkout.TryGetProperty("href", out var href))
        {
            checkoutUrl = href.GetString() ?? string.Empty;
        }

        return new MolliePayment
        {
            Id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
            Status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? string.Empty : string.Empty,
            CheckoutUrl = checkoutUrl,
            RawJson = rawJson
        };
    }

    private sealed class MolliePayment
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string RawJson { get; set; } = "{}";
    }

    private sealed class PaymentIntentMetadata
    {
        public int Credits { get; set; }
        public decimal UnitPriceExVat { get; set; }
        public int SubtotalExVatCents { get; set; }
        public decimal VatRate { get; set; }
        public int VatAmountCents { get; set; }
        public int TotalInclVatCents { get; set; }
        public int DiscountPercent { get; set; }
    }
}
