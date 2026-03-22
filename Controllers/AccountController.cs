using System.Security.Claims;
using Docx2Pdf.Data;
using Docx2Pdf.Models.ViewModels;
using Docx2Pdf.Options;
using Docx2Pdf.Services.Credits;
using Docx2Pdf.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Docx2Pdf.Controllers;

[Authorize]
public sealed class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICreditsService _credits;
    private readonly IPaymentService _payments;
    private readonly PaymentsOptions _paymentOptions;

    public AccountController(ApplicationDbContext db, ICreditsService credits, IPaymentService payments, IOptions<PaymentsOptions> paymentOptions)
    {
        _db = db;
        _credits = credits;
        _payments = payments;
        _paymentOptions = paymentOptions.Value;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var windowStart = DateTime.UtcNow.AddDays(-30);
        var currentCredits = await _credits.GetCreditsAsync(userId);
        var payments = await _db.PaymentIntents.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedUtc).Take(20).ToListAsync();
        var recentJobs = await _db.ConversionJobs.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedUtc).Take(25).ToListAsync();
        var ledger = await _db.CreditLedgerEntries.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync();

        return View(new AccountOverviewViewModel
        {
            CurrentCredits = currentCredits,
            CreditsPurchased = ledger.Where(x => x.Delta > 0 && x.Reason == "mollie_credit_purchase").Sum(x => x.Delta),
            CreditsUsed = Math.Abs(ledger.Where(x => x.Delta < 0).Sum(x => x.Delta)),
            SuccessfulPayments = payments.Count(x => x.Status == PaymentIntentStatuses.Paid && x.CreditsApplied),
            ConversionsLast30Days = recentJobs.Count(x => x.CreatedUtc >= windowStart),
            SuccessfulConversionsLast30Days = recentJobs.Count(x => x.CreatedUtc >= windowStart && x.Status == "completed"),
            FailedConversionsLast30Days = recentJobs.Count(x => x.CreatedUtc >= windowStart && x.Status != "completed"),
            Payments = payments.Select(x => new AccountPaymentHistoryItemViewModel
            {
                IntentId = x.Id,
                Status = x.Status,
                ProviderPaymentId = x.ProviderPaymentId ?? string.Empty,
                Credits = x.Credits,
                TotalInclVat = x.AmountCents / 100m,
                CreatedUtc = x.CreatedUtc,
                PaidUtc = x.PaidUtc,
                InvoiceDownloadUrl = Url.Action("Invoice", "Payments", new { intentId = x.Id }) ?? string.Empty
            }).ToList(),
            RecentConversions = recentJobs.Select(x => new AccountConversionHistoryItemViewModel
            {
                Id = x.Id,
                CreatedUtc = x.CreatedUtc,
                OriginalFileName = x.OriginalFileName,
                Status = x.Status,
                CreditCharged = x.CreditCharged,
                CreditsDelta = x.CreditCharged ? -x.CreditsCharged : 0,
                DownloadUrl = x.Status == "completed" ? Url.Action("Download", "Convert", new { id = x.Id }) : null,
                FailureReason = x.FailureReason
            }).ToList()
        });
    }

    [AllowAnonymous]
    public async Task<IActionResult> Billing(int? credits = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var slider = BillingViewModelFactory.CreateSlider(_paymentOptions, credits ?? _paymentOptions.DefaultCredits);
        var quote = _payments.CalculateQuote(slider.DefaultCredits);
        return View(new BillingPageViewModel
        {
            CurrentCredits = string.IsNullOrWhiteSpace(userId) ? 0 : await _credits.GetCreditsAsync(userId),
            PaymentsEnabled = _paymentOptions.Enabled,
            Slider = slider,
            Quote = BillingViewModelFactory.CreateQuote(quote)
        });
    }
}
