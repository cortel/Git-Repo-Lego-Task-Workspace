using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhoneNumbersManagement.Infrastructure;

namespace PhoneNumbersManagement.Tests;

public sealed class ApiTests
{
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Fact]
    public async Task OpenApiIs31AndShowsRoles()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client();
        var json = await client.GetStringAsync("/openapi/v1.json");
        using var doc = JsonDocument.Parse(json);
        Assert.StartsWith("3.1", doc.RootElement.GetProperty("openapi").GetString());
        Assert.Contains("admin", json);
        Assert.Contains("/v1/reservations", json);
        Assert.Equal("bearer", doc.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("read", HttpStatusCode.Forbidden)]
    [InlineData("write", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.Created)]
    public async Task RangeCreationRequiresAdmin(string? role, HttpStatusCode status)
    {
        using var factory = new ApiFactory();
        using var client = factory.Client(role);
        using var response = await client.PostAsJsonAsync("/v1/ranges", RegistryTests.Input(), ApiJson);
        Assert.Equal(status, response.StatusCode);
    }

    [Fact]
    public async Task PatchCanClearNullableFieldAndReportsValidationErrors()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client("admin");
        using var create = await client.PostAsJsonAsync("/v1/ranges", RegistryTests.Input() with
        {
            Description = "To clear"
        }, ApiJson);
        create.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        using var patch = await client.PatchAsJsonAsync($"/v1/ranges/{id}", new
        {
            description = (string?)null
        });
        patch.EnsureSuccessStatusCode();
        using var updated = JsonDocument.Parse(await patch.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, updated.RootElement.GetProperty("description").ValueKind);
        using var invalid = await client.PatchAsJsonAsync($"/v1/ranges/{id}", new
        {
            name = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly TestDatabase database = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Entra:TenantId", "11111111-1111-1111-1111-111111111111");
        builder.UseSetting("Entra:Audience", "api://test");
        builder.UseSetting("ConnectionStrings:Registry", "Host=unused;Database=test;Username=test");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<RegistryDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RegistryDbContext>>();
            services.AddDbContext<RegistryDbContext>(options => options.UseSqlite(database.Connection)
                .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>());
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        });
    }

    public HttpClient Client(string? role = null)
    {
        var client = CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            database.Dispose();
        }
    }
}

internal sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test"), new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
