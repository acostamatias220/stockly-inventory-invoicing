using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Stockly.Api.Infrastructure;
using Stockly.Application;
using Stockly.Application.Common;
using Stockly.Infrastructure;
using Stockly.Infrastructure.Persistence;
using Stockly.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// ---- Authentication: stateless JWT bearer tokens ---------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwt) =>
    {
        options.MapInboundClaims = false; // keep "sub" / "role" as issued
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = jwt.Value.SigningKey(),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });

// ---- Authorization: secure by default + role based policies ---------------------------------
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin))
    .AddPolicy(Policies.ManageInventory, p => p.RequireRole(Roles.Admin, Roles.Deposito))
    .AddPolicy(Policies.Sell, p => p.RequireRole(Roles.Admin, Roles.Vendedor));

// ---- Brute-force protection on login --------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.GetValue("RateLimiting:LoginPermitLimit", 10),
            Window = TimeSpan.FromMinutes(1),
        }));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

// ---- OpenAPI / Swagger ----------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Stockly API",
        Version = "v1",
        Description = "Inventario multi-depósito y facturación con IVA. Autenticá con POST /api/auth/login y usá el botón Authorize.",
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Pegá el accessToken devuelto por /api/auth/login",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "Stockly.Api.xml");
    if (File.Exists(xml))
    {
        options.IncludeXmlComments(xml);
    }
});

var app = builder.Build();

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Stockly API v1");
        o.DocumentTitle = "Stockly API";
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapGet("/api/health", async (AppDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ok" })
            : Results.Json(new { status = "database unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous()
    .ExcludeFromDescription();

await InitializeDatabaseAsync(app);
await app.RunAsync();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var config = app.Configuration;
    await using var scope = app.Services.CreateAsyncScope();
    if (config.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    var seed = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
    if (seed.Enabled)
    {
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(seed.DemoPassword);
    }
}

/// <summary>Exposed for WebApplicationFactory in the integration tests.</summary>
public partial class Program;
