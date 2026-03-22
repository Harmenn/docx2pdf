namespace Docx2Pdf.Services.Tracking;

public interface ITrackingEventRecorder
{
    Task RecordAsync(HttpContext context, string type, string? label = null, string? value = null, object? meta = null, CancellationToken cancellationToken = default);
}
