using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "Api")]
public sealed class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
        }
        return conn;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task When_login_with_valid_credentials_returns_200_and_tokens()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var password = "TestPassword123!";
        var email = $"test-{Guid.NewGuid():N}@example.com";
        await SeedUserAsync(tenantId, userId, email, password);

        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { email_or_username = email, password = password }),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Response was {response.StatusCode}: {body}");
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            Assert.True(root.TryGetProperty("token", out var token));
            Assert.True(root.TryGetProperty("refresh_token", out var refreshToken));
            Assert.True(root.TryGetProperty("data", out var data));
            Assert.Equal(email, data.GetProperty("email").GetString());
            Assert.NotNull(token.GetString());
            Assert.NotNull(refreshToken.GetString());
        }
        finally
        {
            await DeleteUserAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_login_with_invalid_password_returns_401()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"test-{Guid.NewGuid():N}@example.com";
        await SeedUserAsync(tenantId, userId, email, "CorrectPassword!");

        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { email_or_username = email, password = "WrongPassword!" }),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            Assert.Equal("auth_failed", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
        finally
        {
            await DeleteUserAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_get_current_user_returns_200()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"me-{Guid.NewGuid():N}@example.com";
        await SeedUserAsync(tenantId, userId, email, "Password123!");

        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-User-Id", userId.ToString());

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(email, data.GetProperty("email").GetString());
        }
        finally
        {
            await DeleteUserAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_refresh_token_valid_returns_new_tokens()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { refresh_token = "valid-refresh-token-test-sample" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("token", out var token));
        Assert.True(json.RootElement.TryGetProperty("refresh_token", out var refreshToken));
        Assert.NotNull(token.GetString());
        Assert.NotNull(refreshToken.GetString());
    }

    [Fact]
    public async Task When_change_password_with_valid_current_password_succeeds()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"change-{Guid.NewGuid():N}@example.com";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewSecurePassword456!";
        await SeedUserAsync(tenantId, userId, email, oldPassword);

        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { current_password = oldPassword, new_password = newPassword }),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-User-Id", userId.ToString());

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Verify login with new password works
            using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { email_or_username = email, password = newPassword }),
                    Encoding.UTF8,
                    "application/json")
            };
            loginRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            var loginResponse = await client.SendAsync(loginRequest);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        }
        finally
        {
            await DeleteUserAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_logout_returns_204()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task SeedUserAsync(Guid tenantId, Guid userId, string email, string password)
    {
        await using var context = CreateContext();

        var hasher = new Argon2idPasswordHasher();
        var passwordHash = hasher.HashPassword(password);

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Slug = $"test-tenant-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = email,
            Name = "Test User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        context.UserCredentials.Add(new UserCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Algorithm = "argon2id",
            PasswordHash = passwordHash,
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();
    }

    private static async Task DeleteUserAsync(Guid tenantId)
    {
        await using var context = CreateContext();

        await context.UserCredentials.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Users.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Tenants.Where(x => x.Id == tenantId).ExecuteDeleteAsync();
    }
}
