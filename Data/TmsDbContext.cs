using Microsoft.EntityFrameworkCore;
using TmsApi.Entities;
using TmsApi.Configurations;

namespace TmsApi.Data;

public class TmsDbContext(DbContextOptions<TmsDbContext> options)
    : DbContext(options)
{
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