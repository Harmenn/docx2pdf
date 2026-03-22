using Docx2Pdf.Data.Entities;

namespace Docx2Pdf.Models.ViewModels;

public sealed class ConversionPageViewModel
{
    public int CurrentCredits { get; set; }
    public bool ConverterEnabled { get; set; }
    public string ConverterStatusText { get; set; } = string.Empty;
    public List<ConversionJob> RecentJobs { get; set; } = [];
}
