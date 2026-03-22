namespace Docx2Pdf.Models.ViewModels;

public sealed class AccountOverviewViewModel
{
    public int CurrentCredits { get; set; }
    public int CreditsPurchased { get; set; }
    public int CreditsUsed { get; set; }
    public int SuccessfulPayments { get; set; }
    public int ConversionsLast30Days { get; set; }
    public int SuccessfulConversionsLast30Days { get; set; }
    public int FailedConversionsLast30Days { get; set; }
    public List<AccountPaymentHistoryItemViewModel> Payments { get; set; } = [];
    public List<AccountConversionHistoryItemViewModel> RecentConversions { get; set; } = [];
}

public sealed class AccountPaymentHistoryItemViewModel
{
    public long IntentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProviderPaymentId { get; set; } = string.Empty;
    public int Credits { get; set; }
    public decimal TotalInclVat { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? PaidUtc { get; set; }
    public string InvoiceDownloadUrl { get; set; } = string.Empty;
}

public sealed class AccountConversionHistoryItemViewModel
{
    public long Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool CreditCharged { get; set; }
    public int CreditsDelta { get; set; }
    public string? DownloadUrl { get; set; }
    public string? FailureReason { get; set; }
}
