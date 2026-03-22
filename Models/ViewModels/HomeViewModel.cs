namespace Docx2Pdf.Models.ViewModels;

public sealed class HomeViewModel
{
    public PricingCalculatorViewModel Pricing { get; set; } = new();
    public int CompletedConversions { get; set; }
    public int RegisteredUsers { get; set; }
}
