using System.Security.Claims;
using Docx2Pdf.Models.ViewModels;
using Docx2Pdf.Options;
using Docx2Pdf.Services.Conversions;
using Docx2Pdf.Services.Credits;
using Docx2Pdf.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Docx2Pdf.Controllers;

[Authorize]
public sealed class ConvertController : Controller
{
    private readonly IDocumentConversionService _conversions;
    private readonly ICreditsService _credits;
    private readonly ConversionOptions _options;
    private readonly ITrackingEventRecorder _tracking;

    public ConvertController(IDocumentConversionService conversions, ICreditsService credits, IOptions<ConversionOptions> options, ITrackingEventRecorder tracking)
    {
        _conversions = conversions;
        _credits = credits;
        _options = options.Value;
        _tracking = tracking;
    }

    [HttpGet("/convert")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(new ConversionPageViewModel
        {
            CurrentCredits = await _credits.GetCreditsAsync(userId),
            ConverterEnabled = _options.Enabled,
            ConverterStatusText = _options.Enabled ? "Converter is actief." : "Converter wacht nog op jouw implementatie.",
            RecentJobs = await _conversions.GetRecentJobsAsync(userId)
        });
    }

    [HttpPost("/convert")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 30_000_000)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var job = await _conversions.SubmitAsync(userId, file);
            await _tracking.RecordAsync(HttpContext, "conversion_submitted", value: job.Id.ToString(), meta: new { job.Status, job.OriginalFileName });
            TempData["Success"] = job.Status == "completed"
                ? "Bestand geconverteerd."
                : $"Bestand opgeslagen. Status: {job.Status}. {job.FailureReason}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/convert/download/{id:long}")]
    public async Task<IActionResult> Download(long id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var job = await _conversions.GetAsync(id, userId);
        if (job == null || string.IsNullOrWhiteSpace(job.StoredOutputPath) || !System.IO.File.Exists(job.StoredOutputPath))
        {
            return NotFound();
        }

        return PhysicalFile(job.StoredOutputPath, "application/pdf", Path.GetFileName(job.StoredOutputPath));
    }
}
