using Docx2Pdf.Data.Entities;
using Docx2Pdf.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Docx2Pdf.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<ConversionJob> ConversionJobs => Set<ConversionJob>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<VisitSession> VisitSessions => Set<VisitSession>();
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();
    public DbSet<VisitorUserLink> VisitorUserLinks => Set<VisitorUserLink>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.Credits).HasDefaultValue(10);
            entity.Property(x => x.CreatedUtc).HasColumnName("created_utc");
        });

        builder.Entity<CreditLedgerEntry>(entity =>
        {
            entity.ToTable("app_credit_ledger");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(x => x.CreatedUtc).HasColumnName("created_utc");
            entity.HasIndex(x => new { x.UserId, x.CreatedUtc }).HasDatabaseName("ix_app_credit_ledger_user_time");
        });

        builder.Entity<PaymentIntent>(entity =>
        {
            entity.ToTable("app_payment_intents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(x => x.CreatedUtc).HasColumnName("created_utc");
            entity.Property(x => x.UpdatedUtc).HasColumnName("updated_utc");
            entity.Property(x => x.PaidUtc).HasColumnName("paid_utc");
            entity.Property(x => x.CreditedUtc).HasColumnName("credited_utc");
            entity.HasIndex(x => new { x.Provider, x.ProviderPaymentId }).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedUtc }).HasDatabaseName("ix_app_payment_intents_user_time");
            entity.HasMany(x => x.Events).WithOne(x => x.PaymentIntent).HasForeignKey(x => x.PaymentIntentId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PaymentEvent>(entity =>
        {
            entity.ToTable("app_payment_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(x => x.ReceivedUtc).HasColumnName("received_utc");
            entity.Property(x => x.ProcessedUtc).HasColumnName("processed_utc");
            entity.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
        });

        builder.Entity<ConversionJob>(entity =>
        {
            entity.ToTable("app_conversion_jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedUtc).HasColumnName("created_utc");
            entity.Property(x => x.UpdatedUtc).HasColumnName("updated_utc");
            entity.Property(x => x.CompletedUtc).HasColumnName("completed_utc");
            entity.HasIndex(x => new { x.UserId, x.CreatedUtc }).HasDatabaseName("ix_app_conversion_jobs_user_time");
        });

        builder.Entity<Visitor>(entity =>
        {
            entity.ToTable("app_visitors");
            entity.HasKey(x => x.VisitorId);
            entity.Property(x => x.VisitorId).HasColumnName("visitor_id");
            entity.Property(x => x.FirstSeenUtc).HasColumnName("first_seen_utc");
            entity.Property(x => x.LastSeenUtc).HasColumnName("last_seen_utc");
        });

        builder.Entity<VisitSession>(entity =>
        {
            entity.ToTable("app_sessions");
            entity.HasKey(x => x.SessionId);
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.VisitorId).HasColumnName("visitor_id");
            entity.Property(x => x.StartedUtc).HasColumnName("started_utc");
            entity.Property(x => x.EndedUtc).HasColumnName("ended_utc");
            entity.HasIndex(x => new { x.VisitorId, x.StartedUtc }).HasDatabaseName("ix_app_sessions_visitor_time");
            entity.HasOne(x => x.Visitor).WithMany(x => x.Sessions).HasForeignKey(x => x.VisitorId);
        });

        builder.Entity<TrackingEvent>(entity =>
        {
            entity.ToTable("app_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.OccurredUtc).HasColumnName("occurred_utc");
            entity.Property(x => x.MetaJson).HasColumnName("meta").HasColumnType("jsonb");
            entity.HasIndex(x => new { x.SessionId, x.OccurredUtc }).HasDatabaseName("ix_app_events_session_time");
            entity.HasOne(x => x.Session).WithMany(x => x.Events).HasForeignKey(x => x.SessionId);
        });

        builder.Entity<VisitorUserLink>(entity =>
        {
            entity.ToTable("app_visitor_user_links");
            entity.HasKey(x => new { x.VisitorId, x.UserId });
            entity.Property(x => x.VisitorId).HasColumnName("visitor_id");
            entity.Property(x => x.LinkedUtc).HasColumnName("linked_utc");
            entity.HasOne(x => x.Visitor).WithMany(x => x.UserLinks).HasForeignKey(x => x.VisitorId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });
    }
}
