using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Data;

public static class DataSeeder
{
    private static readonly (string Code, string Title, int MaxCapacity)[] Courses =
    [
        ("CSE-101", "Web Development Fundamentals", 30),
        ("CSE-102", "TypeScript Essentials", 30),
        ("CSE-103", "Git and Collaborative Workflows", 25),
        ("CSE-201", "ASP.NET Core Fundamentals", 28),
        ("CSE-202", "Entity Framework Core and PostgreSQL", 28),
        ("CSE-203", "Building RESTful Web APIs", 28),
        ("CSE-301", "Advanced Web API Patterns", 24),
        ("CSE-302", "Angular Fundamentals", 26),
        ("CSE-303", "Angular Advanced", 24),
        ("CSE-304", "Full-Stack Integration", 22),
        ("CSE-305", "Testing and Quality Assurance", 22),
        ("CSE-306", "Security and Authentication", 20),
        ("DAT-101", "Database Design Foundations", 30),
        ("DAT-201", "Advanced SQL and Indexing", 26),
        ("DAT-202", "Data Modelling for the Web", 26),
        ("ARC-101", "Software Architecture Patterns", 22),
        ("ARC-201", "Cloud-Native Architecture", 22),
        ("DEV-101", "DevOps Foundations", 24),
        ("DEV-201", "Continuous Delivery Pipelines", 22),
        ("MOB-101", "Mobile App Foundations", 24),
        ("MOB-201", "Cross-Platform Mobile", 22),
        ("AI-101", "Applied Machine Learning", 20),
        ("AI-201", "Generative AI for Developers", 18),
        ("UX-101", "UX Research and Wireframing", 24),
        ("UX-201", "Design Systems and Tokens", 22),
    ];

    public static async Task SeedAsync(
        TmsDbContext context,
        UserManager<TmsUser>? userManager = null,
        RoleManager<IdentityRole>? roleManager = null,
        CancellationToken ct = default)
    {
        if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory" && context.Database.IsRelational())
        {
            await context.Database.MigrateAsync(ct);
        }
        else
        {
            await context.Database.EnsureCreatedAsync(ct);
        }

        if (!await context.Courses.AnyAsync(ct))
        {
            foreach (var (code, title, maxCapacity) in Courses)
            {
                context.Courses.Add(new Course
                {
                    Code = code,
                    Title = title,
                    MaxCapacity = maxCapacity
                });
            }
            await context.SaveChangesAsync(ct);
        }

        if (userManager != null && roleManager != null)
        {
            string[] roles = ["Admin", "Instructor", "Student"];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Seed Admin account if it does not exist
            const string adminEmail = "admin@tms.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new TmsUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Admin",
                    Department = "Administration",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, "AdminPass123!");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Seed Instructor account if it does not exist
            const string instructorEmail = "instructor@tms.com";
            var instructorUser = await userManager.FindByEmailAsync(instructorEmail);
            if (instructorUser == null)
            {
                instructorUser = new TmsUser
                {
                    UserName = instructorEmail,
                    Email = instructorEmail,
                    FirstName = "System",
                    LastName = "Instructor",
                    Department = "Computer Science",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(instructorUser, "InstructorPass123!");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(instructorUser, "Instructor");
                }
            }

            // 3. Clear obsolete/demo student accounts and seed Ethiopian local students
            var localStudents = new (string Email, string FirstName, string LastName, string RegNo, decimal Gpa)[]
            {
                ("abebe@tms.com", "Abebe", "Alemu", "STU-1001", 3.85m),
                ("alemu@tms.com", "Alemu", "Tadesse", "STU-1002", 3.60m),
                ("rihana@tms.com", "Rihana", "Mohammed", "STU-1003", 3.95m)
            };

            // Remove any old non-local student users
            var allowedEmails = localStudents.Select(s => s.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var studentRoleUsers = await userManager.GetUsersInRoleAsync("Student");
            foreach (var existingStuUser in studentRoleUsers)
            {
                if (!allowedEmails.Contains(existingStuUser.Email ?? ""))
                {
                    await userManager.DeleteAsync(existingStuUser);
                }
            }

            // Clean up any obsolete Student records in context.Students
            var allowedNames = localStudents.Select(s => $"{s.FirstName} {s.LastName}").ToHashSet();
            var obsoleteStudents = await context.Students.Where(s => !allowedNames.Contains(s.Name)).ToListAsync(ct);
            if (obsoleteStudents.Count != 0)
            {
                var obsoleteStudentIds = obsoleteStudents.Select(s => s.Id).ToList();
                var obsoleteEnrollments = await context.Enrollments
                    .Where(e => obsoleteStudentIds.Contains(e.StudentId))
                    .ToListAsync(ct);
                if (obsoleteEnrollments.Count != 0)
                {
                    context.Enrollments.RemoveRange(obsoleteEnrollments);
                }

                context.Students.RemoveRange(obsoleteStudents);
                await context.SaveChangesAsync(ct);
            }

            // Seed each local student
            foreach (var (email, firstName, lastName, regNo, gpa) in localStudents)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new TmsUser
                    {
                        UserName = email,
                        Email = email,
                        FirstName = firstName,
                        LastName = lastName,
                        Department = "Software Engineering",
                        EmailConfirmed = true
                    };

                    var res = await userManager.CreateAsync(user, "StudentPass123!");
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Student");
                    }
                }

                var fullName = $"{firstName} {lastName}";
                var studentEntity = await context.Students.FirstOrDefaultAsync(s => s.Name == fullName, ct);
                if (studentEntity == null)
                {
                    studentEntity = new Student
                    {
                        RegistrationNumber = regNo,
                        Name = fullName,
                        GPA = gpa,
                        IsActive = true
                    };
                    context.Students.Add(studentEntity);
                    await context.SaveChangesAsync(ct);
                }
            }

            // Seed sample enrollments for courses if none exist
            var courses = await context.Courses.Take(4).ToListAsync(ct);
            var seededStudents = await context.Students.ToListAsync(ct);
            if (courses.Count > 0 && seededStudents.Count > 0 && !await context.Enrollments.AnyAsync(ct))
            {
                var now = DateTime.UtcNow;
                var rand = new Random(42);
                foreach (var st in seededStudents)
                {
                    foreach (var cr in courses.Take(2))
                    {
                        context.Enrollments.Add(new Enrollment
                        {
                            StudentId = st.Id,
                            CourseId = cr.Id,
                            EnrolledAt = now.AddDays(-rand.Next(1, 30)),
                            Year = 2026,
                            Grade = rand.Next(75, 99),
                            IsArchived = false
                        });
                    }
                }
                await context.SaveChangesAsync(ct);
            }
        }
    }
}
