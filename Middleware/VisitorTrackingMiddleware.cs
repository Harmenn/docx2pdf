using Docx2Pdf.Data;
using Docx2Pdf.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Docx2Pdf.Middleware;

public sealed class VisitorTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public VisitorTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        if (ShouldIgnore(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var visitorId = GetOrCreateCookie(context, "vid", 3650);
        var sessionId = GetOrCreateCookie(context, "sid", 1);
        var now = DateTime.UtcNow;

        var visitor = await db.Visitors.SingleOrDefaultAsync(x => x.VisitorId == visitorId);
        if (visitor == null)
        {
            visitor = new Visitor
            {
                VisitorId = visitorId,
                FirstPath = context.Request.Path,
                EntryReferrer = context.Request.Headers.Referer.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                FirstSeenUtc = now,
                LastSeenUtc = now
            };
            db.Visitors.Add(visitor);
        }
        else
        {
            visitor.LastSeenUtc = now;
        }

        var session = await db.VisitSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId);
        if (session == null)
        {
            session = new VisitSession
            {
                SessionId = sessionId,
                VisitorId = visitorId,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                LandingPath = context.Request.Path,
                StartedUtc = now,
                EndedUtc = now
            };
            db.VisitSessions.Add(session);
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId) &&
                !await db.VisitorUserLinks.AnyAsync(x => x.VisitorId == visitorId && x.UserId == userId))
            {
                db.VisitorUserLinks.Add(new VisitorUserLink
                {
                    VisitorId = visitorId,
                    UserId = userId,
                    LinkedUtc = now
                });
            }
        }

        if (HttpMethods.IsGet(context.Request.Method) && ExpectsHtml(context.Request))
        {
            db.TrackingEvents.Add(new TrackingEvent
            {
                SessionId = sessionId,
                Type = "page_view",
                Path = context.Request.Path,
                OccurredUtc = now
            });
            session.PageCount += 1;
            session.EventCount += 1;
        }

        session.EndedUtc = now;
        await db.SaveChangesAsync();
        await _next(context);
    }

    private static bool ShouldIgnore(PathString path)
    {
        return path.StartsWithSegments("/lib") ||
               path.StartsWithSegments("/css") ||
               path.StartsWithSegments("/js") ||
               path.StartsWithSegments("/favicon") ||
               path.Value?.Contains('.') == true;
    }

    private static bool ExpectsHtml(HttpRequest request)
    {
        var accept = request.Headers.Accept.ToString();
        return string.IsNullOrWhiteSpace(accept) || accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid GetOrCreateCookie(HttpContext context, string name, int days)
    {
        if (context.Request.Cookies.TryGetValue(name, out var raw) && Guid.TryParse(raw, out var existing))
        {
            return existing;
        }

        var value = Guid.NewGuid();
        context.Response.Cookies.Append(name, value.ToString(), new CookieOptions
        {
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(days)
        });
        return value;
    }
}
