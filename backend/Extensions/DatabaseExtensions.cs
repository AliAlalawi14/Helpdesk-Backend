using backend.Database;
using backend.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace backend.Extensions;

public static class DatabaseExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        await using ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            await dbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Application database migrations applied successfully.");
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "An error occurred while applying database migrations.");
            throw;
        }
    }

    public static async Task ApplyIdentityMigrationsAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        await using ApplicationIdentityDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();

        try
        {
            await dbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Identity database migrations applied successfully.");
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "An error occurred while applying identity database migrations.");
            throw;
        }
    }

    public static async Task SeedInitialDataAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        await using ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // idempotent: only seed on an empty DB
        if (await dbContext.Categories.AnyAsync())
        {
            return;
        }

        try
        {
            // fixed IDs so tickets can reference them, and so real users later line up
            var itHardware = new Category { Id = "cat_it_hardware", Name = "IT - Hardware", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var itAccess = new Category { Id = "cat_it_access", Name = "IT - Access & VPN", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var hrPayroll = new Category { Id = "cat_hr_payroll", Name = "HR - Payroll", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

            dbContext.Categories.AddRange(itHardware, itAccess, hrPayroll);

            // placeholder user IDs — these become the real seeded user IDs once auth exists
            const string jordan = "usr_jordan";   // a "user" (requester)
            const string sam = "usr_sam";      // a "moderator" (assignee)
            const string priya = "usr_priya";    // a "moderator" (assignee)

            DateTime now = DateTime.UtcNow;
            var tickets = new List<Ticket>
            {
                NewTicket("VPN won't connect from home", TicketStatus.Open,       TicketPriority.High,   itAccess.Id,   jordan, null,  now.AddHours(-1)),
                NewTicket("Laptop battery not charging",  TicketStatus.InProgress, TicketPriority.Medium, itHardware.Id, jordan, sam,   now.AddHours(-5)),
                NewTicket("Payslip missing overtime",     TicketStatus.Open,       TicketPriority.Urgent, hrPayroll.Id,  jordan, null,  now.AddHours(-8)),
                NewTicket("Monitor flickering",           TicketStatus.Open,       TicketPriority.Low,    itHardware.Id, jordan, null,  now.AddHours(-12)),
                NewTicket("Can't access shared drive",    TicketStatus.InProgress, TicketPriority.High,   itAccess.Id,   jordan, priya, now.AddHours(-20)),
                NewTicket("New keyboard request",         TicketStatus.Resolved,   TicketPriority.Low,    itHardware.Id, jordan, sam,   now.AddDays(-2),            TimeSpan.FromHours(4)),
                NewTicket("Password reset loop",          TicketStatus.Open,       TicketPriority.Medium, itAccess.Id,   jordan, null,  now.AddDays(-2).AddHours(-3)),
                NewTicket("Bonus not reflected",          TicketStatus.Closed,     TicketPriority.Medium, hrPayroll.Id,  jordan, priya, now.AddDays(-3),            TimeSpan.FromHours(26)),
                NewTicket("Docking station dead",         TicketStatus.InProgress, TicketPriority.Urgent, itHardware.Id, jordan, sam,   now.AddDays(-4)),
                NewTicket("MFA device lost",              TicketStatus.Open,       TicketPriority.High,   itAccess.Id,   jordan, null,  now.AddDays(-5)),
                NewTicket("Tax form correction",          TicketStatus.Resolved,   TicketPriority.Low,    hrPayroll.Id,  jordan, priya, now.AddDays(-6),            TimeSpan.FromHours(51)),
                NewTicket("Headset mic not working",      TicketStatus.Closed,     TicketPriority.Low,    itHardware.Id, jordan, sam,   now.AddDays(-7),            TimeSpan.FromHours(9)),
            };

            dbContext.Tickets.AddRange(tickets);

            await dbContext.SaveChangesAsync();
            if (app.Logger.IsEnabled(LogLevel.Information))
            {
                app.Logger.LogInformation("Seeded {CategoryCount} categories and {TicketCount} tickets.",
                    3, tickets.Count);
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "An error occurred while seeding initial data.");
            throw;
        }
    }

    // resolvedAfter varies per ticket on purpose: a flat interval would make every
    // "average time to resolve" figure the same number and hide whether the metric
    // is actually working.
    private static Ticket NewTicket(
        string subject, TicketStatus status, TicketPriority priority,
        string categoryId, string requesterId, string? assigneeId, DateTime createdAt,
        TimeSpan? resolvedAfter = null)
    {
        bool isTerminal = status is TicketStatus.Resolved or TicketStatus.Closed;

        return new Ticket
        {
            Id = Ticket.NewId(),
            Subject = subject,
            Description = subject + " — details provided by the requester.",
            Status = status,
            Priority = priority,
            CategoryId = categoryId,
            RequesterId = requesterId,
            AssigneeId = assigneeId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ResolvedAt = isTerminal ? createdAt + (resolvedAfter ?? TimeSpan.FromHours(8)) : null
        };
    }

    // Creates BOTH records per user: the identity record that holds the credentials,
    // and the domain record that holds name/role/IsActive. Runs before the ticket
    // seed, because tickets now carry real FKs to ticket.users.
    public static async Task SeedUsersAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await using ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // idempotent: if any domain user exists, assume seeded
        if (await dbContext.Users.AnyAsync())
        {
            return;
        }

        try
        {
            const string devPassword = "Password123!";

            // fixed domain IDs so the already-seeded tickets resolve to real users
            await CreateUserAsync(userManager, dbContext, "usr_admin", "Amina Admin", "admin@example.com", UserRole.Admin, devPassword);
            await CreateUserAsync(userManager, dbContext, "usr_sam", "Sam Support", "sam@example.com", UserRole.Moderator, devPassword);
            await CreateUserAsync(userManager, dbContext, "usr_priya", "Priya Agent", "priya@example.com", UserRole.Moderator, devPassword);
            await CreateUserAsync(userManager, dbContext, "usr_jordan", "Jordan Employee", "jordan@example.com", UserRole.User, devPassword);

            await dbContext.SaveChangesAsync();

            app.Logger.LogInformation("Seeded identity and domain users.");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "An error occurred while seeding users.");
            throw;
        }
    }

    private static async Task CreateUserAsync(
        UserManager<IdentityUser> userManager,
        ApplicationDbContext dbContext,
        string id, string name, string email, UserRole role, string password)
    {
        var identityUser = new IdentityUser
        {
            Email = email,
            UserName = email,           // Identity requires a username; email is fine
            EmailConfirmed = true       // skip confirmation flow for dev
        };

        IdentityResult created = await userManager.CreateAsync(identityUser, password);
        if (!created.Succeeded)
        {
            string errors = string.Join("; ", created.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user '{email}': {errors}");
        }

        DateTime now = DateTime.UtcNow;
        dbContext.Users.Add(new User
        {
            Id = id,
            Name = name,
            Email = email,
            Role = role,
            IsActive = true,
            IdentityId = identityUser.Id,   // the link between the two schemas
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
