using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        // Primary key
        builder.HasKey(s => s.Id);

        // Required fields (example)
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.RegistrationNumber)
            .IsRequired()
            .HasMaxLength(50);

        // GPA column precision (optional but good practice)
        builder.Property(s => s.GPA)
            .HasPrecision(3, 2);

        // ⭐ THIS IS THE IMPORTANT PART (EXERCISE 8)
        builder.Property<DateTime>("LastUpdated");

        // Concurrency token (EXERCISE 8)
        builder.Property<uint>("Version")
            .IsRowVersion();

        // Relationships (if not already elsewhere)
        builder.HasMany(s => s.Enrollments)
            .WithOne(e => e.Student)
            .HasForeignKey(e => e.StudentId);
    }
}