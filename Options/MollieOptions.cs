namespace Docx2Pdf.Options;

public sealed class MollieOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.mollie.com/v2";
}
