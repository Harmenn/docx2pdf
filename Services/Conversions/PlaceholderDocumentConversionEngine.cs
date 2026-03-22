namespace Docx2Pdf.Services.Conversions;

public sealed class PlaceholderDocumentConversionEngine : IDocumentConversionEngine
{
    public bool IsConfigured => false;
    public string Provider => "placeholder";

    public Task<DocumentConversionResult> ConvertAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DocumentConversionResult
        {
            Success = false,
            FailureReason = "Converter is nog niet gekoppeld. Implementeer hier je eigen docx->pdf logica."
        });
    }
}
