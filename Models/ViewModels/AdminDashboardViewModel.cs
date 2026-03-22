using Docx2Pdf.Data.Entities;

namespace Docx2Pdf.Models.ViewModels;

public sealed class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalCreditsOutstanding { get; set; }
    public int TotalCompletedConversions { get; set; }
    public int TotalFailedConversions { get; set; }
    public int TotalPaidPayments { get; set; }
    public List<AdminUserRowViewModel> Users { get; set; } = [];
    public List<ConversionJob> RecentJobs { get; set; } = [];
    public List<Docx2Pdf.Data.Entities.PaymentIntent> RecentPayments { get; set; } = [];
}

public sealed class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Credits { get; set; }
    public DateTime CreatedUtc { get; set; }
}
