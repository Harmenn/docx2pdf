using Docx2Pdf.Data;
using Docx2Pdf.Models.ViewModels;
using Docx2Pdf.Options;
using Docx2Pdf.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Docx2Pdf.Controllers;

public sealed class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PaymentsOptions _paymentsOptions;

    public HomeController(ApplicationDbContext db, IOptions<PaymentsOptions> paymentsOptions)
    {
        _db = db;
        _paymentsOptions = paymentsOptions.Value;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index()
    {
        var calculator = new BillingCalculator(_paymentsOptions);
        var slider = BillingViewModelFactory.CreateSlider(_paymentsOptions, _paymentsOptions.DefaultCredits);
        var pricing = new PricingCalculatorViewModel
        {
            Slider = slider,
            DefaultQuote = BillingViewModelFactory.CreateQuote(calculator.CalculateQuote(slider.DefaultCredits)),
            LowestUnitPriceExVat = calculator.CalculateQuote(_paymentsOptions.MaxCredits).UnitPriceExVat,
            QuoteOptions = BuildQuoteOptions(calculator)
        };

        return View(new HomeViewModel
        {
            Pricing = pricing,
            CompletedConversions = await _db.ConversionJobs.CountAsync(x => x.Status == "completed"),
            RegisteredUsers = await _db.Users.CountAsync()
        });
    }

    [HttpGet("/privacy")]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new Models.ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private List<PricingQuoteOptionViewModel> BuildQuoteOptions(BillingCalculator calculator)
    {
        var results = new List<PricingQuoteOptionViewModel>();
        for (var credits = _paymentsOptions.MinCredits; credits < 500; credits += 50)
        {
            var quote = calculator.CalculateQuote(credits);
            results.Add(new PricingQuoteOptionViewModel
            {
                Credits = credits,
                SliderPosition = BillingViewModelFactory.CalculateSliderPositionForCredits(credits, _paymentsOptions.MinCredits, _paymentsOptions.MaxCredits),
                UnitPriceExVat = quote.UnitPriceExVat,
                SubtotalExVat = quote.SubtotalExVat,
                VatAmount = quote.VatAmount,
                TotalInclVat = quote.TotalInclVat,
                DiscountPercent = quote.DiscountPercent
            });
        }

        for (var credits = 500; credits <= _paymentsOptions.MaxCredits; credits += 100)
        {
            var quote = calculator.CalculateQuote(credits);
            results.Add(new PricingQuoteOptionViewModel
            {
                Credits = credits,
                SliderPosition = BillingViewModelFactory.CalculateSliderPositionForCredits(credits, _paymentsOptions.MinCredits, _paymentsOptions.MaxCredits),
                UnitPriceExVat = quote.UnitPriceExVat,
                SubtotalExVat = quote.SubtotalExVat,
                VatAmount = quote.VatAmount,
                TotalInclVat = quote.TotalInclVat,
                DiscountPercent = quote.DiscountPercent
            });
        }

        return results;
    }
}
