namespace Docx2Pdf.Services.Conversions;

public interface IDocumentConversionEngine
{
    bool IsConfigured { get; }
    string Provider { get; }
    Task<DocumentConversionResult> ConvertAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
}

public sealed class DocumentConversionResult
{
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
}
