# Docx2Pdf

ASP.NET Core 9 MVC-app voor een Windows-first `docx -> pdf` product.

Wat er nu staat:

- registratie, login en accountbeheer via ASP.NET Identity
- creditsaldo per gebruiker
- staffelprijzen en checkout-flow via Mollie
- payment intents, webhook-verwerking en factuurdownload
- upload- en jobflow voor conversies
- admin dashboard voor users, credits en jobs
- verse PostgreSQL database en EF Core migraties

De echte conversielogica hangt achter `IDocumentConversionEngine`.
De standaard implementatie is expres een placeholder:

- bestand uploaden werkt
- jobs worden opgeslagen
- de app geeft duidelijk aan waar jouw eigen converter moet worden gekoppeld

## Lokaal draaien

1. Zorg dat PostgreSQL lokaal draait.
2. Database `docx2pdf_dev` is al aangemaakt in deze omgeving.
3. Pas indien nodig `appsettings.Development.json` aan.
4. Run:

```powershell
dotnet build
dotnet run
```

## Belangrijke integratiepunten

- conversie-engine: `Services/Conversions/IDocumentConversionEngine.cs`
- placeholder engine: `Services/Conversions/PlaceholderDocumentConversionEngine.cs`
- conversieflow: `Services/Conversions/DocumentConversionService.cs`
- billing: `Services/Payments/PaymentService.cs`
- datamodel: `Data/ApplicationDbContext.cs`
