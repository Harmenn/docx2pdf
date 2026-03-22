using Docx2Pdf.Options;

namespace Docx2Pdf.Services.Payments;

public sealed class BillingCalculator
{
    private readonly PaymentsOptions _options;

    public BillingCalculator(PaymentsOptions options)
    {
        _options = options;
    }

    public CreditPurchaseQuote CalculateQuote(int credits)
    {
        ValidateCredits(credits);

        var range = Math.Max(1, _options.MaxCredits - _options.MinCredits);
        var ratio = (double)(credits - _options.MinCredits) / range;
        var curveRatio = Math.Pow(Math.Max(0d, ratio), _options.PriceCurveExponent);
        var unitPriceExVat = _options.MinPriceEuro - ((_options.MinPriceEuro - _options.MaxPriceEuro) * (decimal)curveRatio);
        var subtotalExVat = decimal.Round(credits * unitPriceExVat, 2, MidpointRounding.AwayFromZero);
        var vatAmount = decimal.Round(subtotalExVat * _options.VatRate, 2, MidpointRounding.AwayFromZero);
        var totalInclVat = subtotalExVat + vatAmount;
        var discountPercent = Math.Max(0, (int)Math.Round(((_options.MinPriceEuro - unitPriceExVat) / _options.MinPriceEuro) * 100m, MidpointRounding.AwayFromZero));

        return new CreditPurchaseQuote
        {
            Credits = credits,
            Currency = _options.Currency,
            UnitPriceExVat = unitPriceExVat,
            SubtotalExVat = subtotalExVat,
            VatRate = _options.VatRate,
            VatAmount = vatAmount,
            TotalInclVat = totalInclVat,
            DiscountPercent = discountPercent
        };
    }

    public void ValidateCredits(int credits)
    {
        if (credits < _options.MinCredits || credits > _options.MaxCredits)
        {
            throw new InvalidOperationException($"Kies een aantal credits tussen {_options.MinCredits} en {_options.MaxCredits}.");
        }

        if (credits < 500 && credits % 50 != 0)
        {
            throw new InvalidOperationException("Onder 500 credits werkt billing in stappen van 50.");
        }

        if (credits >= 500 && (credits - 500) % 100 != 0)
        {
            throw new InvalidOperationException("Vanaf 500 credits werkt billing in stappen van 100.");
        }
    }
}

public sealed class CreditPurchaseQuote
{
    public int Credits { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal UnitPriceExVat { get; set; }
    public decimal SubtotalExVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalInclVat { get; set; }
    public int DiscountPercent { get; set; }
    public int SubtotalExVatCents => (int)decimal.Round(SubtotalExVat * 100m, 0, MidpointRounding.AwayFromZero);
    public int VatAmountCents => (int)decimal.Round(VatAmount * 100m, 0, MidpointRounding.AwayFromZero);
    public int TotalInclVatCents => (int)decimal.Round(TotalInclVat * 100m, 0, MidpointRounding.AwayFromZero);
}
