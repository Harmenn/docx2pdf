using System.Security.Claims;
using Docx2Pdf.Models.ViewModels;
using Docx2Pdf.Options;
using Docx2Pdf.Services.Credits;
using Docx2Pdf.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Docx2Pdf.Controllers;

public sealed class PaymentsController : Controller
{
    private readonly IPaymentService _payments;
    private readonly ICreditsService _credits;
    private readonly PaymentsOptions _paymentOptions;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService payments, ICreditsService credits, IOptions<PaymentsOptions> paymentOptions, ILogger<PaymentsController> logger)
    {
        _payments = payments;
        _credits = credits;
        _paymentOptions = paymentOptions.Value;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("/credits/summary", Name = "CreditPurchaseOverview")]
    public async Task<IActionResult> Summary(int credits)
    {
        try
        {
            var quote = _payments.CalculateQuote(credits);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                var returnUrl = Url.RouteUrl("CreditPurchaseOverview", new { credits = quote.Credits }) ?? "/";
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl });
            }

            return View(new BillingSummaryViewModel
            {
                CurrentCredits = await _credits.GetCreditsAsync(userId),
                PaymentsEnabled = _paymentOptions.Enabled,
                Slider = BillingViewModelFactory.CreateSlider(_paymentOptions, quote.Credits),
                Quote = BillingViewModelFactory.CreateQuote(quote)
            });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Billing", "Account", new { credits });
        }
    }

    [Authorize]
    [HttpPost("/credits/summary")]
    [ValidateAntiForgeryToken]
    public IActionResult SummaryPost(int credits) => RedirectToAction(nameof(Summary), new { credits });

    [Authorize]
    [HttpPost("/credits/checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(int credits)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var result = await _payments.CreateCheckoutAsync(userId, credits, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            if (string.IsNullOrWhiteSpace(result.CheckoutUrl))
            {
                TempData["Error"] = "Checkout URL ontbreekt.";
                return RedirectToAction("Billing", "Account", new { credits });
            }

            return Redirect(result.CheckoutUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Checkout start failed for {UserId}", userId);
            TempData["Error"] = ex.Message;
            return RedirectToAction("Billing", "Account", new { credits });
        }
    }

    [Authorize]
    [HttpGet("/payments/return")]
    public async Task<IActionResult> Return(long? intentId = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!intentId.HasValue)
        {
            TempData["Error"] = "Geen betaling gevonden.";
            return RedirectToAction("Billing", "Account");
        }

        var intent = await _payments.RefreshIntentAsync(intentId.Value, userId);
        if (intent == null)
        {
            TempData["Error"] = "Betaling niet gevonden.";
            return RedirectToAction("Billing", "Account");
        }

        return View(new BillingReturnViewModel
        {
            IntentId = intent.IntentId,
            Status = intent.Status,
            CreditsApplied = intent.CreditsApplied,
            CurrentCredits = intent.CurrentCredits,
            ProviderPaymentId = intent.ProviderPaymentId,
            InvoiceDownloadUrl = Url.Action(nameof(Invoice), new { intentId = intent.IntentId }) ?? string.Empty,
            Quote = BillingViewModelFactory.CreateQuote(intent.Quote)
        });
    }

    [Authorize]
    [HttpGet("/payments/invoice")]
    public async Task<IActionResult> Invoice(long intentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var invoice = await _payments.GetInvoiceAsync(intentId, userId);
        return invoice == null ? NotFound() : File(invoice.Content, invoice.ContentType, invoice.FileName);
    }

    [AllowAnonymous]
    [HttpPost("/payments/webhook")]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        try
        {
            await _payments.ProcessWebhookAsync(body, Request.Headers, HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook processing failed");
        }

        return Ok();
    }
}
