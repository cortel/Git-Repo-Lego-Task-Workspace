using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using PhoneNumbersManagement;
using PhoneNumbersManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddRegistry(builder.Configuration);
var tenant = builder.Configuration["Entra:TenantId"];
var audience = builder.Configuration["Entra:Audience"];
if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("Configure Entra:TenantId and Entra:Audience before starting the API.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = $"https://login.microsoftonline.com/{tenant}/v2.0";
    options.Audience = audience;
    options.MapInboundClaims = false;
    options.TokenValidationParameters.RoleClaimType = "roles";
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("read", policy => policy.RequireAuthenticatedUser().RequireRole("read", "write", "admin"));
    options.AddPolicy("write", policy => policy.RequireAuthenticatedUser().RequireRole("write", "admin"));
    options.AddPolicy("admin", policy => policy.RequireAuthenticatedUser().RequireRole("admin"));
});
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "Phone Number Registry API";
        document.Info.Version = "v1";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" }
        };
        foreach (var path in document.Paths.Values)
        {
            foreach (var operation in (IEnumerable<OpenApiOperation>?)path.Operations?.Values ?? [])
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing or invalid bearer token." });
                operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Required application role is missing." });
                operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Invalid input." });
                operation.Responses.TryAdd("404", new OpenApiResponse { Description = "Entity was not found." });
                operation.Responses.TryAdd("409", new OpenApiResponse { Description = "Reservation, state, or concurrency conflict; reload before retrying." });
            }
        }

        document.Info.Description = "Entra JWT bearer authentication. App roles: read (queries), write (queries/create/update), admin (all operations including deletion and range management).";
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, ct) =>
    {
        var policies = context.Description.ActionDescriptor.EndpointMetadata.OfType<AuthorizeAttribute>()
            .Select(x => x.Policy).Where(x => x is not null).Distinct();
        operation.Description = $"Required authorization policy: {string.Join(", ", policies)}. Roles: read accepts read/write/admin; write accepts write/admin; admin accepts admin only.";
        return Task.CompletedTask;
    });
});
var app = builder.Build();
app.UseExceptionHandler();
app.UseHttpsRedirection();
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("../openapi/v1.json", "Phone Number Registry v1");
        options.DocumentTitle = "Phone Number Registry API";
        options.EnableFilter();
        options.DisplayRequestDuration();
        options.DefaultModelsExpandDepth(-1);
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        options.InjectStylesheet("./registry.css");

        // Keep tokens in memory only; do not persist authorization in browser storage.
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Document contains schemas and role names only; endpoints remain protected in all environments.
app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.Run();

public partial class Program;
