using Microsoft.EntityFrameworkCore;
using TmsApi.Entities;
using TmsApi.Configurations;
using System.Linq;

namespace TmsApi.Data;

public class TmsDbContext(DbContextOptions<TmsDbContext> options)
    : DbContext(options)
{
    public override int SaveChanges()
    {
        UpdateAudit();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAudit()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added);

        foreach (var entry in entries)
        {
            entry.Property("LastUpdated").CurrentValue = DateTime.UtcNow;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TmsDbContext).Assembly);
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
}