using Docx2Pdf.Data;
using Docx2Pdf.Models.ViewModels;
using Docx2Pdf.Services.Credits;
using Docx2Pdf.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Docx2Pdf.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICreditsService _credits;

    public AdminController(ApplicationDbContext db, ICreditsService credits)
    {
        _db = db;
        _credits = credits;
    }

    [HttpGet("/admin")]
    public async Task<IActionResult> Index()
    {
        return View(new AdminDashboardViewModel
        {
            TotalUsers = await _db.Users.CountAsync(),
            TotalCreditsOutstanding = await _db.Users.SumAsync(x => x.Credits),
            TotalCompletedConversions = await _db.ConversionJobs.CountAsync(x => x.Status == "completed"),
            TotalFailedConversions = await _db.ConversionJobs.CountAsync(x => x.Status == "failed" || x.Status == "configuration_required"),
            TotalPaidPayments = await _db.PaymentIntents.CountAsync(x => x.Status == PaymentIntentStatuses.Paid),
            Users = await _db.Users.OrderByDescending(x => x.CreatedUtc).Take(20).Select(x => new AdminUserRowViewModel
            {
                Id = x.Id,
                Email = x.Email ?? x.UserName ?? x.Id,
                Credits = x.Credits,
                CreatedUtc = x.CreatedUtc
            }).ToListAsync(),
            RecentJobs = await _db.ConversionJobs.OrderByDescending(x => x.CreatedUtc).Take(20).ToListAsync(),
            RecentPayments = await _db.PaymentIntents.OrderByDescending(x => x.CreatedUtc).Take(20).ToListAsync()
        });
    }

    [HttpPost("/admin/topup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopUp(string userId, int amount)
    {
        try
        {
            await _credits.TopUpAsync(userId, amount, "admin_manual_topup", "user", userId, new { Admin = User.Identity?.Name });
            TempData["Success"] = $"{amount} credits toegevoegd.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
