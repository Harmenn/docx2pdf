using Docx2Pdf.Options;
using Docx2Pdf.Services.Payments;

namespace Docx2Pdf.Models.ViewModels;

public sealed class BillingPageViewModel
{
    public int CurrentCredits { get; set; }
    public bool PaymentsEnabled { get; set; }
    public BillingSliderViewModel Slider { get; set; } = new();
    public BillingQuoteViewModel Quote { get; set; } = new();
}

public sealed class BillingSummaryViewModel
{
    public int CurrentCredits { get; set; }
    public bool PaymentsEnabled { get; set; }
    public BillingSliderViewModel Slider { get; set; } = new();
    public BillingQuoteViewModel Quote { get; set; } = new();
}

public sealed class BillingReturnViewModel
{
    public long IntentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool CreditsApplied { get; set; }
    public int CurrentCredits { get; set; }
    public string ProviderPaymentId { get; set; } = string.Empty;
    public string InvoiceDownloadUrl { get; set; } = string.Empty;
    public BillingQuoteViewModel Quote { get; set; } = new();
}

public sealed class BillingSliderViewModel
{
    public int MinCredits { get; set; }
    public int MaxCredits { get; set; }
    public int SliderDefaultPosition { get; set; }
    public int SliderMaxPosition { get; set; } = 100;
    public int SliderMinCredits { get; set; }
    public int DefaultCredits { get; set; }
    public decimal MinPriceEuro { get; set; }
    public decimal MaxPriceEuro { get; set; }
    public double PriceCurveExponent { get; set; }
    public decimal VatRate { get; set; }
}

public sealed class BillingQuoteViewModel
{
    public int Credits { get; set; }
    public decimal UnitPriceExVat { get; set; }
    public decimal SubtotalExVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalInclVat { get; set; }
    public int DiscountPercent { get; set; }
}

public sealed class PricingCalculatorViewModel
{
    public BillingSliderViewModel Slider { get; set; } = new();
    public BillingQuoteViewModel DefaultQuote { get; set; } = new();
    public List<PricingQuoteOptionViewModel> QuoteOptions { get; set; } = [];
    public decimal LowestUnitPriceExVat { get; set; }
}

public sealed class PricingQuoteOptionViewModel
{
    public int Credits { get; set; }
    public int SliderPosition { get; set; }
    public decimal UnitPriceExVat { get; set; }
    public decimal SubtotalExVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalInclVat { get; set; }
    public int DiscountPercent { get; set; }
}

public static class BillingViewModelFactory
{
    public static BillingSliderViewModel CreateSlider(PaymentsOptions options, int? selectedCredits = null)
    {
        var credits = Math.Clamp(selectedCredits ?? options.DefaultCredits, options.MinCredits, options.MaxCredits);
        return new BillingSliderViewModel
        {
            MinCredits = options.MinCredits,
            MaxCredits = options.MaxCredits,
            SliderMinCredits = 0,
            SliderMaxPosition = 100,
            SliderDefaultPosition = CalculateSliderPositionForCredits(credits, options.MinCredits, options.MaxCredits),
            DefaultCredits = credits,
            MinPriceEuro = options.MinPriceEuro,
            MaxPriceEuro = options.MaxPriceEuro,
            PriceCurveExponent = options.PriceCurveExponent,
            VatRate = options.VatRate
        };
    }

    public static int CalculateSliderPositionForCredits(int credits, int minCredits, int maxCredits)
    {
        if (credits <= 250)
        {
            var ratio = (double)(credits - minCredits) / Math.Max(1, 250 - minCredits);
            return (int)Math.Round(Math.Clamp(ratio, 0d, 1d) * 25d);
        }

        var upperRatio = (double)(credits - 250) / Math.Max(1, maxCredits - 250);
        return 25 + (int)Math.Round(Math.Clamp(upperRatio, 0d, 1d) * 75d);
    }

    public static BillingQuoteViewModel CreateQuote(CreditPurchaseQuote quote)
    {
        return new BillingQuoteViewModel
        {
            Credits = quote.Credits,
            UnitPriceExVat = quote.UnitPriceExVat,
            SubtotalExVat = quote.SubtotalExVat,
            VatRate = quote.VatRate,
            VatAmount = quote.VatAmount,
            TotalInclVat = quote.TotalInclVat,
            DiscountPercent = quote.DiscountPercent
        };
    }
}
