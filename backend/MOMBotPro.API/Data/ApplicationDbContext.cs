using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Models;
using MOMBotPro.API.Models.Entities;

namespace MOMBotPro.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // ── Auth entities (existing) ──────────────────────────
    public DbSet<AppUser>      Users             => Set<AppUser>();
    public DbSet<Integration>  Integrations      => Set<Integration>();
    public DbSet<OAuthState>   OAuthStates       => Set<OAuthState>();
    public DbSet<ZoomSettings> ZoomSettings { get; set; } = null!;
    public DbSet<ZoomMeeting>  ZoomMeetings { get; set; } = null!;

    // ── Pipeline entities ─────────────────────────────────
    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();
    public DbSet<PipelineEntity>     Pipelines     => Set<PipelineEntity>();
    public DbSet<PipelineStepEntity> PipelineSteps => Set<PipelineStepEntity>();
    public DbSet<JiraTicketEntity>   JiraTickets   => Set<JiraTicketEntity>();
    public DbSet<BugAnalysisEntity>  BugAnalyses   => Set<BugAnalysisEntity>();
    public DbSet<GitHubResultEntity> GitHubResults => Set<GitHubResultEntity>();
    public DbSet<ZoomSessionEntity>  ZoomSessions  => Set<ZoomSessionEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ── AppUser ──────────────────────────────────────
        b.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).ValueGeneratedNever();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasDefaultValue("User");
            e.Property(u => u.SubscriptionPlan).HasDefaultValue("free_trial");
            e.Property(u => u.FreeTrialMeetingsLeft).HasDefaultValue(3);
            e.Property(u => u.IsActive).HasDefaultValue(true);
        });

        // ── Integration ───────────────────────────────────
        b.Entity<Integration>(e =>
        {
            e.ToTable("Integrations");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).ValueGeneratedNever();
            e.HasIndex(i => new { i.UserId, i.Type }).IsUnique();
        });

        // ── OAuthState ────────────────────────────────────
        b.Entity<OAuthState>(e =>
        {
            e.ToTable("OAuthStates");
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).ValueGeneratedNever();
            e.HasIndex(o => o.State).IsUnique();
        });

        // ── Subscription → Users ──────────────────────────
        b.Entity<SubscriptionEntity>(e =>
        {
            e.ToTable("Subscriptions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.PriceUSD).HasPrecision(10, 2);
            e.Property(s => s.PriceINR).HasPrecision(10, 2);
            e.HasOne<AppUser>().WithMany()
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Pipeline → Users ──────────────────────────────
        b.Entity<PipelineEntity>(e =>
        {
            e.ToTable("Pipelines");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Transcript).HasColumnType("nvarchar(max)");
            e.Property(p => p.BugSummary).HasColumnType("nvarchar(max)");
            e.Property(p => p.MOMSummary).HasColumnType("nvarchar(max)");
            e.Property(p => p.RecordingVideoUrl).HasColumnType("nvarchar(max)");
            e.Property(p => p.RecordingAudioUrl).HasColumnType("nvarchar(max)");
            e.Property(p => p.RecordingTranscriptJson).HasColumnType("nvarchar(max)");
            e.HasOne<AppUser>().WithMany()
             .HasForeignKey(p => p.UserId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── PipelineStep → Pipeline ───────────────────────
        b.Entity<PipelineStepEntity>(e =>
        {
            e.ToTable("PipelineSteps");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).ValueGeneratedNever();
            e.HasOne(s => s.Pipeline)
             .WithMany(p => p.Steps)
             .HasForeignKey(s => s.PipelineId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── JiraTicket → Pipeline (1:1) ───────────────────
        b.Entity<JiraTicketEntity>(e =>
        {
            e.ToTable("JiraTickets");
            e.HasKey(j => j.Id);
            e.Property(j => j.Id).ValueGeneratedNever();
            e.Property(j => j.Description).HasColumnType("nvarchar(max)");
            e.HasOne(j => j.Pipeline)
             .WithOne(p => p.JiraTicket)
             .HasForeignKey<JiraTicketEntity>(j => j.PipelineId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── BugAnalysis → Pipeline (1:1) ──────────────────
        b.Entity<BugAnalysisEntity>(e =>
        {
            e.ToTable("BugAnalyses");
            e.HasKey(ba => ba.Id);
            e.Property(ba => ba.Id).ValueGeneratedNever();
            e.Property(ba => ba.BugDescription).HasColumnType("nvarchar(max)");
            e.Property(ba => ba.RootCause).HasColumnType("nvarchar(max)");
            e.Property(ba => ba.SuggestedFix).HasColumnType("nvarchar(max)");
            e.Property(ba => ba.OriginalCode).HasColumnType("nvarchar(max)");
            e.Property(ba => ba.FixedCode).HasColumnType("nvarchar(max)");
            e.HasOne(ba => ba.Pipeline)
             .WithOne(p => p.BugAnalysis)
             .HasForeignKey<BugAnalysisEntity>(ba => ba.PipelineId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── GitHubResult → Pipeline (1:1) ─────────────────
        b.Entity<GitHubResultEntity>(e =>
        {
            e.ToTable("GitHubResults");
            e.HasKey(g => g.Id);
            e.Property(g => g.Id).ValueGeneratedNever();
            e.Property(g => g.PRDescription).HasColumnType("nvarchar(max)");
            e.HasOne(g => g.Pipeline)
             .WithOne(p => p.GitHubResult)
             .HasForeignKey<GitHubResultEntity>(g => g.PipelineId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ZoomSession → Users ───────────────────────────
        b.Entity<ZoomSessionEntity>(e =>
        {
            e.ToTable("ZoomSessions");
            e.HasKey(z => z.Id);
            e.Property(z => z.Id).ValueGeneratedNever();
            e.HasOne<AppUser>().WithMany()
             .HasForeignKey(z => z.UserId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── ZoomMeeting → Users ───────────────────────────
        b.Entity<ZoomMeeting>(e =>
        {
            e.ToTable("ZoomMeetings");
            e.HasKey(z => z.Id);
            e.Property(z => z.Id).ValueGeneratedNever();
            e.HasIndex(z => new { z.UserId, z.ZoomMeetingId }).IsUnique();
            e.HasOne<AppUser>().WithMany()
             .HasForeignKey(z => z.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ZoomSettings → Users ──────────────────────────
        b.Entity<ZoomSettings>(e =>
        {
            e.ToTable("ZoomSettings");
            e.HasKey(z => z.Id);
            e.Property(z => z.Id).ValueGeneratedNever();
            e.HasIndex(z => z.UserId).IsUnique();   // one row per user
            e.HasOne<AppUser>().WithMany()
             .HasForeignKey(z => z.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
