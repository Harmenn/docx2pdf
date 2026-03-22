using Docx2Pdf.Data;
using Docx2Pdf.Data.Entities;
using Docx2Pdf.Options;
using Docx2Pdf.Services.Credits;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Docx2Pdf.Services.Conversions;

public sealed class DocumentConversionService : IDocumentConversionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICreditsService _credits;
    private readonly IDocumentConversionEngine _engine;
    private readonly ConversionOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DocumentConversionService> _logger;

    public DocumentConversionService(
        ApplicationDbContext db,
        ICreditsService credits,
        IDocumentConversionEngine engine,
        IOptions<ConversionOptions> options,
        IWebHostEnvironment environment,
        ILogger<DocumentConversionService> logger)
    {
        _db = db;
        _credits = credits;
        _engine = engine;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ConversionJob> SubmitAsync(string userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length <= 0)
        {
            throw new InvalidOperationException("Upload een DOCX-bestand.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Alleen .docx bestanden zijn toegestaan.");
        }

        if (file.Length > _options.MaxUploadMb * 1024L * 1024L)
        {
            throw new InvalidOperationException($"Bestand is groter dan {_options.MaxUploadMb} MB.");
        }

        var storageRoot = Path.IsPathRooted(_options.StorageRoot)
            ? _options.StorageRoot
            : Path.Combine(_environment.ContentRootPath, _options.StorageRoot);
        var uploadDir = Path.Combine(storageRoot, "uploads", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var outputDir = Path.Combine(storageRoot, "outputs", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        Directory.CreateDirectory(uploadDir);
        Directory.CreateDirectory(outputDir);

        var inputPath = Path.Combine(uploadDir, $"{Guid.NewGuid():N}.docx");
        await using (var stream = File.Create(inputPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var job = new ConversionJob
        {
            UserId = userId,
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredInputPath = inputPath,
            Status = "uploaded",
            Provider = _engine.Provider,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _db.ConversionJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        if (!_options.Enabled || !_engine.IsConfigured)
        {
            job.Status = "configuration_required";
            job.FailureReason = "Converter is nog niet geconfigureerd. Koppel hier je eigen conversielogica.";
            job.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return job;
        }

        var outputPath = Path.Combine(outputDir, $"{job.Id:D8}.pdf");
        try
        {
            await _credits.ConsumeAsync(
                userId,
                1,
                "document_conversion",
                "conversion_job",
                job.Id.ToString(),
                new { job.OriginalFileName },
                cancellationToken);

            job.CreditCharged = true;
            job.CreditsCharged = 1;
            job.Status = "processing";
            job.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var result = await _engine.ConvertAsync(inputPath, outputPath, cancellationToken);
            if (!result.Success)
            {
                await _credits.TopUpAsync(
                    userId,
                    1,
                    "document_conversion_refund",
                    "conversion_job",
                    job.Id.ToString(),
                    new { result.FailureReason },
                    cancellationToken);

                job.CreditCharged = false;
                job.CreditsCharged = 0;
                job.Status = "failed";
                job.FailureReason = result.FailureReason;
                job.UpdatedUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return job;
            }

            job.Status = "completed";
            job.StoredOutputPath = outputPath;
            job.CompletedUtc = DateTime.UtcNow;
            job.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return job;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Conversion job {JobId} failed", job.Id);
            if (job.CreditCharged)
            {
                await _credits.TopUpAsync(userId, 1, "document_conversion_refund", "conversion_job", job.Id.ToString(), new { ex.Message }, cancellationToken);
                job.CreditCharged = false;
                job.CreditsCharged = 0;
            }

            job.Status = "failed";
            job.FailureReason = ex.Message;
            job.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return job;
        }
    }

    public Task<ConversionJob?> GetAsync(long id, string userId, CancellationToken cancellationToken = default)
    {
        return _db.ConversionJobs.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    }

    public Task<List<ConversionJob>> GetRecentJobsAsync(string userId, int take = 20, CancellationToken cancellationToken = default)
    {
        return _db.ConversionJobs.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedUtc).Take(take).ToListAsync(cancellationToken);
    }
}
