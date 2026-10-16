using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<SportCategory> SportCategories => Set<SportCategory>();
    public DbSet<EvaluationCriterion> EvaluationCriteria => Set<EvaluationCriterion>();
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<SportAssignment> SportAssignments => Set<SportAssignment>();
    public DbSet<TryoutSchedule> Schedules => Set<TryoutSchedule>();
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<EvaluationScore> EvaluationScores => Set<EvaluationScore>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        // Students sign in with their student ID, so it must stay unique. MySQL allows many NULLs
        // in a unique index, which keeps staff accounts (no student ID) unaffected.
        b.Entity<AppUser>().HasIndex(u => u.StudentId).IsUnique();

        b.Entity<Sport>().HasIndex(s => s.Name).IsUnique();

        b.Entity<SportCategory>().HasIndex(c => c.Name).IsUnique();
        b.Entity<EvaluationCriterion>()
            .HasOne(c => c.Category).WithMany(cat => cat.Criteria)
            .HasForeignKey(c => c.SportCategoryId).OnDelete(DeleteBehavior.Cascade);
        // A sport without a category keeps working; deleting a category leaves its sports uncategorised.
        b.Entity<Sport>()
            .HasOne(s => s.Category).WithMany(c => c.Sports)
            .HasForeignKey(s => s.SportCategoryId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<SportAssignment>().HasKey(x => new { x.SportId, x.UserId });
        b.Entity<SportAssignment>()
            .HasOne(x => x.Sport).WithMany(s => s.Staff)
            .HasForeignKey(x => x.SportId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SportAssignment>()
            .HasOne(x => x.User).WithMany(u => u.SportAssignments)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TryoutSchedule>()
            .HasOne(t => t.Sport).WithMany(s => s.Schedules)
            .HasForeignKey(t => t.SportId).OnDelete(DeleteBehavior.Cascade);

        // One tryout entry per student per sport; the service layer also checks this explicitly.
        b.Entity<Applicant>().HasIndex(a => new { a.StudentId, a.SportId }).IsUnique();
        b.Entity<Applicant>().HasIndex(a => new { a.UserId, a.SportId }).IsUnique();
        b.Entity<Applicant>()
            .HasOne(a => a.Sport).WithMany(s => s.Applicants)
            .HasForeignKey(a => a.SportId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Applicant>()
            .HasOne(a => a.Schedule).WithMany(t => t.Applicants)
            .HasForeignKey(a => a.TryoutScheduleId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Applicant>()
            .HasOne(a => a.User).WithMany(u => u.Registrations)
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Evaluation>().HasIndex(e => new { e.ApplicantId, e.EvaluatorId }).IsUnique();
        b.Entity<Evaluation>()
            .HasOne(e => e.Applicant).WithMany(a => a.Evaluations)
            .HasForeignKey(e => e.ApplicantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Evaluation>()
            .HasOne(e => e.Evaluator).WithMany()
            .HasForeignKey(e => e.EvaluatorId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<EvaluationScore>().HasIndex(s => new { s.EvaluationId, s.EvaluationCriterionId }).IsUnique();
        b.Entity<EvaluationScore>()
            .HasOne(s => s.Evaluation).WithMany(e => e.Scores)
            .HasForeignKey(s => s.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<EvaluationScore>()
            .HasOne(s => s.Criterion).WithMany()
            .HasForeignKey(s => s.EvaluationCriterionId).OnDelete(DeleteBehavior.Restrict);

        // One row per granted capability; deleting a staff account clears its permissions.
        b.Entity<UserPermission>().HasIndex(p => new { p.UserId, p.Permission }).IsUnique();
        b.Entity<UserPermission>()
            .HasOne(p => p.User).WithMany(u => u.Permissions)
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
