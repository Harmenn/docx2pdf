namespace Docx2Pdf.Options;

public sealed class PaymentsOptions
{
    public bool Enabled { get; set; }
    public string Currency { get; set; } = "EUR";
    public int MinCredits { get; set; } = 100;
    public int MaxCredits { get; set; } = 2000;
    public int DefaultCredits { get; set; } = 250;
    public decimal MinPriceEuro { get; set; } = 0.45m;
    public decimal MaxPriceEuro { get; set; } = 0.20m;
    public double PriceCurveExponent { get; set; } = 0.28d;
    public decimal VatRate { get; set; } = 0.21m;
    public string ReturnPath { get; set; } = "/payments/return";
    public string WebhookPath { get; set; } = "/payments/webhook";
}
