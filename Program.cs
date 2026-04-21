using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SRAAS.Api.Data;
using SRAAS.Api.Enums;
using SRAAS.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════
//  DATABASE
// ═══════════════════════════════════════════════════
builder.Services.AddDbContext<SraasDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.MapEnum<AppTypeEnum>("app_type_enum");
            npgsql.MapEnum<MemberRoleEnum>("member_role_enum");
            npgsql.MapEnum<MemberStatusEnum>("member_status_enum");
            npgsql.MapEnum<InviteTypeEnum>("invite_type_enum");
            npgsql.MapEnum<ChannelTypeEnum>("channel_type_enum");
            npgsql.MapEnum<ContentTypeEnum>("content_type_enum");
        });
});

// ═══════════════════════════════════════════════════
//  JWT AUTHENTICATION
// ═══════════════════════════════════════════════════
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════
//  RATE LIMITING
// ═══════════════════════════════════════════════════
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests       = false;
    options.GeneralRules               = new List<RateLimitRule>
    {
        new() { Endpoint = "POST:/api/auth/login",   Period = "5m",  Limit = 10 },
        new() { Endpoint = "POST:/api/invites/join", Period = "10m", Limit = 5  },
        new() { Endpoint = "POST:/api/messages",     Period = "1m",  Limit = 60 },
        new() { Endpoint = "POST:/api/files/upload", Period = "1m",  Limit = 10 }
    };
});
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// ═══════════════════════════════════════════════════
//  SERVICES
// ═══════════════════════════════════════════════════
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ═══════════════════════════════════════════════════
//  OPENAPI (.NET 10 built-in) + Scalar
// ═══════════════════════════════════════════════════
builder.Services.AddOpenApi("v1");

// ═══════════════════════════════════════════════════
//  CONTROLLERS + JSON
// ═══════════════════════════════════════════════════
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ═══════════════════════════════════════════════════
//  CORS
// ═══════════════════════════════════════════════════
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000", "http://localhost:5173"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ═══════════════════════════════════════════════════
//  MIDDLEWARE PIPELINE
// ═══════════════════════════════════════════════════

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// OpenAPI JSON always available (needed by Scalar)
// → /openapi/v1.json
app.MapOpenApi();

// Scalar API Explorer UI
// → http://localhost:<port>/scalar/v1
app.MapScalarApiReference(options =>
{
    options.Title             = "SRAAS API Explorer";
    options.Theme             = ScalarTheme.DeepSpace;
    options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    options.ShowSidebar = true;
    options.HideModels  = false;
    options.DarkMode    = true;
    options.AddPreferredSecuritySchemes("Bearer");
});

app.UseHttpsRedirection();
app.UseIpRateLimiting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
