using Docx2Pdf.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace Docx2Pdf.Services.Conversions;

public interface IDocumentConversionService
{
    Task<ConversionJob> SubmitAsync(string userId, IFormFile file, CancellationToken cancellationToken = default);
    Task<ConversionJob?> GetAsync(long id, string userId, CancellationToken cancellationToken = default);
    Task<List<ConversionJob>> GetRecentJobsAsync(string userId, int take = 20, CancellationToken cancellationToken = default);
}
