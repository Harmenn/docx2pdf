namespace Docx2Pdf.Services.Credits;

public interface ICreditsService
{
    Task<int> GetCreditsAsync(string userId, CancellationToken cancellationToken = default);
    Task TopUpAsync(string userId, int amount, string reason, string? referenceType = null, string? referenceId = null, object? metadata = null, CancellationToken cancellationToken = default);
    Task ConsumeAsync(string userId, int amount, string reason, string? referenceType = null, string? referenceId = null, object? metadata = null, CancellationToken cancellationToken = default);
}
