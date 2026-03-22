using Microsoft.AspNetCore.Identity;

namespace Docx2Pdf.Models;

public sealed class ApplicationUser : IdentityUser
{
    public int Credits { get; set; } = 10;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
