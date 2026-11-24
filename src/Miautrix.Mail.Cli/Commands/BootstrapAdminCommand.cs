using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class BootstrapAdminSettings : CommandSettings
{
    [CommandOption("--email <EMAIL>")]
    public string Email { get; set; } = string.Empty;

    [CommandOption("--reset")]
    public bool Reset { get; set; }

    public override ValidationResult Validate()
    {
        try { _ = new System.Net.Mail.MailAddress(Email); }
        catch { return ValidationResult.Error("A valid --email is required."); }
        return ValidationResult.Success();
    }
}

public sealed class BootstrapAdminCommand(AppDbContext db, IPasswordHasher passwordHasher) : Command<BootstrapAdminSettings>
{
    protected override int Execute(CommandContext context, BootstrapAdminSettings settings, CancellationToken cancellationToken)
    {
        var password = ReadSecret("New admin password: ");
        var confirm = ReadSecret("Confirm password: ");
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            AnsiConsole.MarkupLine("[red]Passwords do not match.[/]");
            return 1;
        }
        if (!IsPasswordAllowed(password))
        {
            AnsiConsole.MarkupLine("[red]Password must contain at least 8 characters and cannot be whitespace.[/]");
            return 1;
        }
        return ExecuteAsync(settings, password).GetAwaiter().GetResult();
    }

    public static bool IsPasswordAllowed(string password) => password.Length >= 8 && !string.IsNullOrWhiteSpace(password);
    public static bool CanReplaceCredential(bool credentialExists, bool resetRequested) => !credentialExists || resetRequested;

    private async Task<int> ExecuteAsync(BootstrapAdminSettings settings, string password)
    {
        var email = settings.Email.Trim().ToLowerInvariant();
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == "default");
        if (tenant is null) { AnsiConsole.MarkupLine("[red]Default tenant is not seeded.[/]"); return 1; }
        var at = email.LastIndexOf('@');
        var domainName = at > 0 ? email[(at + 1)..] : string.Empty;
        var domain = await db.Domains.SingleOrDefaultAsync(d => d.TenantId == tenant.Id && d.Name.ToLower() == domainName);
        if (domain is null)
        {
            domain = new Miautrix.Mail.Domain.Domain { TenantId = tenant.Id, Name = domainName, IsVerified = false, IsPrimary = !await db.Domains.AnyAsync(d => d.TenantId == tenant.Id) };
            db.Domains.Add(domain);
            await db.SaveChangesAsync();
        }
        var ownerRole = await db.Roles.SingleOrDefaultAsync(r => r.TenantId == tenant.Id && r.Code == "owner");
        if (ownerRole is null) { AnsiConsole.MarkupLine("[red]Owner role is not seeded.[/]"); return 1; }

        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email.ToLower() == email);
        if (user is null)
        {
            user = new User { TenantId = tenant.Id, Email = email, Name = email, IsActive = true };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var credential = await db.UserCredentials.SingleOrDefaultAsync(c => c.TenantId == tenant.Id && c.UserId == user.Id);
        var hadCredential = credential is not null;
        if (!CanReplaceCredential(credential is not null, settings.Reset))
        {
            AnsiConsole.MarkupLine("[red]An admin credential already exists; pass --reset to replace it.[/]");
            return 1;
        }
        if (credential is null)
        {
            credential = new UserCredential { TenantId = tenant.Id, UserId = user.Id };
            db.UserCredentials.Add(credential);
        }
        credential.PasswordHash = passwordHasher.HashPassword(password);
        credential.Algorithm = "argon2id";
        credential.MustChangePassword = false;
        credential.UpdatedAt = DateTimeOffset.UtcNow;

        if (!await db.Memberships.AnyAsync(m => m.TenantId == tenant.Id && m.UserId == user.Id && m.RoleId == ownerRole.Id))
            db.Memberships.Add(new Membership { TenantId = tenant.Id, UserId = user.Id, RoleId = ownerRole.Id });
        var mailbox = await db.Mailboxes.SingleOrDefaultAsync(m => m.TenantId == tenant.Id && m.Address.ToLower() == email);
        if (mailbox is null)
        {
            mailbox = new Mailbox { TenantId = tenant.Id, DomainId = domain.Id, Address = email, Name = user.Name, IsActive = true };
            db.Mailboxes.Add(mailbox);
            await db.SaveChangesAsync();
        }
        var folders = new[] { ("INBOX", "inbox"), ("Sent", "sent"), ("Drafts", "drafts"), ("Trash", "trash"), ("Junk", "junk"), ("Archive", "archive") };
        foreach (var (name, role) in folders)
            if (!await db.Folders.AnyAsync(f => f.TenantId == tenant.Id && f.MailboxId == mailbox.Id && f.Role == role))
                db.Folders.Add(new Folder { TenantId = tenant.Id, MailboxId = mailbox.Id, Name = name, Role = role });
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenant.Id, ActorId = user.Id, Action = hadCredential ? "admin.credential.reset" : "admin.bootstrap",
            TargetType = "UserCredential", TargetId = user.Id,
            DetailsJson = JsonSerializer.Serialize(new { email, credential.MustChangePassword })
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        AnsiConsole.MarkupLine("[green]Admin credential created successfully.[/]");
        return 0;
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var chars = new List<char>();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace && chars.Count > 0) chars.RemoveAt(chars.Count - 1);
            else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }
}
