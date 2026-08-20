using EventApi.Application.Services;
using EventApi.Domain.Entities;
using EventApi.Domain.Enums;
using EventApi.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context)
    {
        var hasUsersTable = await TableExistsAsync(context, "Users");

        if (!hasUsersTable)
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await ApplyLegacySchemaUpdatesAsync(context);
        }

        await SeedUsersAsync(context);
        await SeedCatalogAsync(context);
        await SeedEventsAsync(context);
        await SeedUc12DemoDataAsync(context);
    }

    private static async Task SeedUsersAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;

        await UpsertUserAsync(context, "admin@fpt.edu.vn", "System Admin", "Admin@123", UserRole.Admin, now);
        await UpsertUserAsync(context, "staff@fpt.edu.vn", "Event Staff", "Staff@123", UserRole.Staff, now);
        await UpsertUserAsync(context, "student@fpt.edu.vn", "Demo Student", "Student@123", UserRole.Student, now, "SE170001");
        await UpsertUserAsync(context, "student2@fpt.edu.vn", "Demo Student 2", "Student@123", UserRole.Student, now, "SE170002");
        await context.SaveChangesAsync();
    }

    private static async Task UpsertUserAsync(
        AppDbContext context, string email, string fullName, string password, UserRole role, DateTime now, string? studentCode = null)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        var (hash, salt) = PasswordHasher.HashPassword(password);

        if (user is null)
        {
            context.Users.Add(new User
            {
                Email = email,
                FullName = fullName,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = role,
                StudentCode = studentCode,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            return;
        }

        // Luôn đồng bộ mật khẩu cho tài khoản seed (tránh login fail sau nhiều lần test)
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.Role = role;
        user.FullName = fullName;
        user.StudentCode = studentCode ?? user.StudentCode;
        user.IsActive = true;
        user.UpdatedAt = now;
    }

    private static async Task SeedCatalogAsync(AppDbContext context)
    {
        if (await context.Locations.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        context.Locations.AddRange(
            new Location
            {
                Name = "Hall A",
                Address = "FPT University, Building A",
                Description = "Main auditorium",
                Capacity = 500,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Location
            {
                Name = "Room B201",
                Address = "FPT University, Building B, Floor 2",
                Description = "Seminar room",
                Capacity = 80,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Location
            {
                Name = "Outdoor Stage",
                Address = "FPT University, Central Campus",
                Description = "Open-air event space",
                Capacity = 1000,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });

        context.Organizers.AddRange(
            new Organizer
            {
                Name = "FPT Student Council",
                ContactEmail = "council@fpt.edu.vn",
                ContactPhone = "0901234567",
                Description = "Official student organization",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Organizer
            {
                Name = "Tech Club",
                ContactEmail = "techclub@fpt.edu.vn",
                ContactPhone = "0909876543",
                Description = "Technology and innovation club",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedEventsAsync(AppDbContext context)
    {
        if (await context.Events.AnyAsync())
            return;

        var staff = await context.Users.FirstOrDefaultAsync(u => u.Email == "staff@fpt.edu.vn");
        var hallA = await context.Locations.FirstOrDefaultAsync(l => l.Name == "Hall A");
        var roomB = await context.Locations.FirstOrDefaultAsync(l => l.Name == "Room B201");
        var outdoor = await context.Locations.FirstOrDefaultAsync(l => l.Name == "Outdoor Stage");
        var council = await context.Organizers.FirstOrDefaultAsync(o => o.Name == "FPT Student Council");
        var techClub = await context.Organizers.FirstOrDefaultAsync(o => o.Name == "Tech Club");

        if (staff is null || hallA is null || roomB is null || council is null || techClub is null)
            return;

        var now = DateTime.UtcNow;

        context.Events.AddRange(
            new Event
            {
                Title = "Freshman Orientation 2026",
                Description = "Welcome event for new students",
                StartTime = now.AddDays(30),
                EndTime = now.AddDays(30).AddHours(4),
                RegistrationDeadline = now.AddDays(28),
                Capacity = 400,
                Status = "Published",
                LocationId = hallA.Id,
                OrganizerId = council.Id,
                CreatedById = staff.Id,
                CreatedAt = now
            },
            new Event
            {
                Title = "AI Workshop Series",
                Description = "Hands-on machine learning workshop",
                StartTime = now.AddDays(14),
                EndTime = now.AddDays(14).AddHours(3),
                RegistrationDeadline = now.AddDays(12),
                Capacity = 60,
                Status = "Draft",
                LocationId = roomB.Id,
                OrganizerId = techClub.Id,
                CreatedById = staff.Id,
                CreatedAt = now
            },
            new Event
            {
                Title = "Campus Music Festival",
                Description = "Annual outdoor music festival",
                StartTime = now.AddDays(-1),
                EndTime = now.AddDays(-1).AddHours(6),
                RegistrationDeadline = now.AddDays(-3),
                Capacity = 800,
                Status = "Ongoing",
                LocationId = outdoor!.Id,
                OrganizerId = council.Id,
                CreatedById = staff.Id,
                CreatedAt = now.AddDays(-10)
            },
            new Event
            {
                Title = "Tech Talk: Cloud Computing",
                Description = "Industry expert shares cloud trends",
                StartTime = now.AddDays(-30),
                EndTime = now.AddDays(-30).AddHours(2),
                RegistrationDeadline = now.AddDays(-32),
                Capacity = 100,
                Status = "Completed",
                LocationId = roomB.Id,
                OrganizerId = techClub.Id,
                CreatedById = staff.Id,
                CreatedAt = now.AddDays(-45)
            },
            new Event
            {
                Title = "Cancelled Seminar",
                Description = "This event was cancelled due to scheduling conflict",
                StartTime = now.AddDays(7),
                EndTime = now.AddDays(7).AddHours(2),
                RegistrationDeadline = now.AddDays(5),
                Capacity = 50,
                Status = "Cancelled",
                LocationId = roomB.Id,
                OrganizerId = techClub.Id,
                CreatedById = staff.Id,
                CreatedAt = now.AddDays(-5)
            });

        await context.SaveChangesAsync();
        await EventWorkflow.SyncAllAsync(context);
    }

    /// <summary>
    /// Demo data — mỗi event có thời gian map sang 1 status (tự động).
    /// </summary>
    private static async Task SeedUc12DemoDataAsync(AppDbContext context)
    {
        const string prefix = "[DEMO AUTO]";

        if (await context.Events.AnyAsync(e => e.Title.StartsWith(prefix)))
            return;

        var staff = await context.Users.FirstOrDefaultAsync(u => u.Email == "staff@fpt.edu.vn");
        var student1 = await context.Users.FirstOrDefaultAsync(u => u.Email == "student@fpt.edu.vn");
        var student2 = await context.Users.FirstOrDefaultAsync(u => u.Email == "student2@fpt.edu.vn");
        var location = await context.Locations.OrderBy(l => l.Id).FirstOrDefaultAsync();
        var organizer = await context.Organizers.OrderBy(o => o.Id).FirstOrDefaultAsync();

        if (staff is null || location is null || organizer is null)
            return;

        var now = DateTime.UtcNow;

        var draft = new Event
        {
            Title = $"{prefix} Draft — trước hạn đăng ký",
            Description = "now < RegistrationDeadline → Draft (chỉ Staff/Admin thấy).",
            StartTime = now.AddDays(20),
            EndTime = now.AddDays(20).AddHours(3),
            RegistrationDeadline = now.AddDays(5),
            Capacity = 50,
            LocationId = location.Id,
            OrganizerId = organizer.Id,
            CreatedById = staff.Id,
            CreatedAt = now.AddMinutes(-5)
        };

        var published = new Event
        {
            Title = $"{prefix} Published — đang mở, chờ bắt đầu",
            Description = "RegistrationDeadline <= now < StartTime → Published. Có 2 đăng ký.",
            StartTime = now.AddDays(10),
            EndTime = now.AddDays(10).AddHours(2),
            RegistrationDeadline = now.AddDays(-1),
            Capacity = 50,
            LocationId = location.Id,
            OrganizerId = organizer.Id,
            CreatedById = staff.Id,
            CreatedAt = now.AddMinutes(-4)
        };

        var ongoing = new Event
        {
            Title = $"{prefix} Ongoing — đang diễn ra",
            Description = "StartTime <= now < EndTime → Ongoing.",
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(5),
            RegistrationDeadline = now.AddDays(-2),
            Capacity = 100,
            LocationId = location.Id,
            OrganizerId = organizer.Id,
            CreatedById = staff.Id,
            CreatedAt = now.AddDays(-7)
        };

        var completed = new Event
        {
            Title = $"{prefix} Completed — đã kết thúc",
            Description = "now >= EndTime → Completed. Chỉ Admin sửa được.",
            StartTime = now.AddDays(-14),
            EndTime = now.AddDays(-14).AddHours(2),
            RegistrationDeadline = now.AddDays(-16),
            Capacity = 80,
            LocationId = location.Id,
            OrganizerId = organizer.Id,
            CreatedById = staff.Id,
            CreatedAt = now.AddDays(-30)
        };

        foreach (var evt in new[] { draft, published, ongoing, completed })
            EventWorkflow.ApplyResolvedStatus(evt);

        context.Events.AddRange(draft, published, ongoing, completed);
        await context.SaveChangesAsync();

        if (student1 is not null)
        {
            context.Registrations.Add(new Registration
            {
                EventId = published.Id,
                StudentId = student1.Id,
                Status = "Registered",
                RegisteredAt = now.AddDays(-2),
                CheckedInAt = null
            });
        }

        if (student2 is not null)
        {
            context.Registrations.Add(new Registration
            {
                EventId = published.Id,
                StudentId = student2.Id,
                Status = "Registered",
                RegisteredAt = now.AddDays(-1),
                CheckedInAt = now.AddDays(-1).AddHours(1)
            });
        }

        if (student1 is not null)
        {
            context.Feedbacks.Add(new Feedback
            {
                EventId = completed.Id,
                StudentId = student1.Id,
                Rating = 5,
                Comment = "Demo feedback — event rất hay!",
                SubmittedAt = now.AddDays(-13)
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<bool> TableExistsAsync(AppDbContext context, string tableName)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName";
        var param = command.CreateParameter();
        param.ParameterName = "@tableName";
        param.Value = tableName;
        command.Parameters.Add(param);

        if (command.Connection!.State != System.Data.ConnectionState.Open)
            await command.Connection.OpenAsync();

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static async Task ApplyLegacySchemaUpdatesAsync(AppDbContext context)
    {
        const string sql = """
            IF COL_LENGTH('Users', 'PasswordSalt') IS NULL
                ALTER TABLE Users ADD PasswordSalt nvarchar(500) NOT NULL CONSTRAINT DF_Users_PasswordSalt DEFAULT '';
            IF COL_LENGTH('Users', 'Phone') IS NULL
                ALTER TABLE Users ADD Phone nvarchar(20) NULL;
            IF COL_LENGTH('Users', 'UpdatedAt') IS NULL
                ALTER TABLE Users ADD UpdatedAt datetime2 NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT sysdatetime();

            IF COL_LENGTH('Locations', 'Description') IS NULL
                ALTER TABLE Locations ADD Description nvarchar(500) NULL;
            IF COL_LENGTH('Locations', 'IsActive') IS NULL
                ALTER TABLE Locations ADD IsActive bit NOT NULL CONSTRAINT DF_Locations_IsActive DEFAULT 1;
            IF COL_LENGTH('Locations', 'CreatedAt') IS NULL
                ALTER TABLE Locations ADD CreatedAt datetime2 NOT NULL CONSTRAINT DF_Locations_CreatedAt DEFAULT sysdatetime();
            IF COL_LENGTH('Locations', 'UpdatedAt') IS NULL
                ALTER TABLE Locations ADD UpdatedAt datetime2 NOT NULL CONSTRAINT DF_Locations_UpdatedAt DEFAULT sysdatetime();
            UPDATE Locations SET Address = Name WHERE Address IS NULL;

            IF COL_LENGTH('Organizers', 'ContactPhone') IS NULL
                ALTER TABLE Organizers ADD ContactPhone nvarchar(20) NOT NULL CONSTRAINT DF_Organizers_ContactPhone DEFAULT '';
            IF COL_LENGTH('Organizers', 'IsActive') IS NULL
                ALTER TABLE Organizers ADD IsActive bit NOT NULL CONSTRAINT DF_Organizers_IsActive DEFAULT 1;
            IF COL_LENGTH('Organizers', 'CreatedAt') IS NULL
                ALTER TABLE Organizers ADD CreatedAt datetime2 NOT NULL CONSTRAINT DF_Organizers_CreatedAt DEFAULT sysdatetime();
            IF COL_LENGTH('Organizers', 'UpdatedAt') IS NULL
                ALTER TABLE Organizers ADD UpdatedAt datetime2 NOT NULL CONSTRAINT DF_Organizers_UpdatedAt DEFAULT sysdatetime();
            UPDATE Organizers SET ContactEmail = 'unknown@fpt.edu.vn' WHERE ContactEmail IS NULL;
            """;

        await context.Database.ExecuteSqlRawAsync(sql);
    }
}
