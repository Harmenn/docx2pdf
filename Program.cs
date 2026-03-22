using Docx2Pdf.Data;
using Docx2Pdf.Middleware;
using Docx2Pdf.Models;
using Docx2Pdf.Options;
using Docx2Pdf.Services.Conversions;
using Docx2Pdf.Services.Credits;
using Docx2Pdf.Services.Payments;
using Docx2Pdf.Services.Tracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Docx2Pdf;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllersWithViews();
        builder.Services.AddRazorPages();
        builder.Services.AddHttpContextAccessor();

        builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
        builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection("Payments"));
        builder.Services.Configure<MollieOptions>(builder.Configuration.GetSection("Mollie"));
        builder.Services.Configure<ConversionOptions>(builder.Configuration.GetSection("Conversions"));
        builder.Services.Configure<AdminBootstrapOptions>(builder.Configuration.GetSection("BootstrapAdmin"));

        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' not found.");

        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services
            .AddDefaultIdentity<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        builder.Services.AddScoped<ICreditsService, CreditsService>();
        builder.Services.AddScoped<ITrackingEventRecorder, TrackingEventRecorder>();
        builder.Services.AddScoped<IDocumentConversionEngine, PlaceholderDocumentConversionEngine>();
        builder.Services.AddScoped<IDocumentConversionService, DocumentConversionService>();
        builder.Services.AddHttpClient<IPaymentService, PaymentService>(client => client.Timeout = TimeSpan.FromSeconds(30));

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<VisitorTrackingMiddleware>();
        app.UseAuthorization();

        app.MapStaticAssets();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();
        app.MapRazorPages().WithStaticAssets();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var bootstrap = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetSection("BootstrapAdmin").Get<AdminBootstrapOptions>() ?? new AdminBootstrapOptions();

            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            var admin = await userManager.FindByEmailAsync(bootstrap.Email);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = bootstrap.Email,
                    Email = bootstrap.Email,
                    EmailConfirmed = true,
                    Credits = 100,
                    CreatedUtc = DateTime.UtcNow
                };

                var created = await userManager.CreateAsync(admin, bootstrap.Password);
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        await app.RunAsync();
    }
}
