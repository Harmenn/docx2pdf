namespace Docx2Pdf.Options;

public sealed class ConversionOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "custom";
    public string StorageRoot { get; set; } = "App_Data";
    public int MaxUploadMb { get; set; } = 25;
}
