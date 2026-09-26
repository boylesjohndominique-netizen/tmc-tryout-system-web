using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<SportAssignment> SportAssignments => Set<SportAssignment>();
    public DbSet<TryoutSchedule> Schedules => Set<TryoutSchedule>();
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();

        b.Entity<Sport>().HasIndex(s => s.Name).IsUnique();

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
    }
}
