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
    }

    private static async Task SeedUsersAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;

        await UpsertUserAsync(context, "admin@fpt.edu.vn", "System Admin", "Admin@123", UserRole.Admin, now);
        await UpsertUserAsync(context, "staff@fpt.edu.vn", "Event Staff", "Staff@123", UserRole.Staff, now);
        await context.SaveChangesAsync();
    }

    private static async Task UpsertUserAsync(
        AppDbContext context, string email, string fullName, string password, UserRole role, DateTime now)
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
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            return;
        }

        if (string.IsNullOrEmpty(user.PasswordSalt))
        {
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.Role = role;
            user.IsActive = true;
            user.UpdatedAt = now;
        }
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
