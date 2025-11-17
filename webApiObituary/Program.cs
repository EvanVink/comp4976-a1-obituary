using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Assignment1.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Assignment1.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ==========================
// DATABASE
// ==========================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var serverVersion = new MySqlServerVersion(new Version(8, 0, 32)); // set your MySQL version here

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ==========================
// JWT AUTHENTICATION
// ==========================
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? "your-super-secret-jwt-key-that-is-at-least-32-characters-long!";

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MemorialRegistry";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MemorialRegistry";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
})
.AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ==========================
// CORS
// ==========================
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5180",                                // Local Blazor WASM (HTTP)
            "https://localhost:5180",                               // Local Blazor WASM (HTTPS)  
            "http://localhost:7259",                                // Local Blazor WASM (HTTP alt)
            "https://localhost:7259",                               // Local Blazor WASM (HTTPS alt)
            "https://red-dune-0446b1110.6.azurestaticapps.net"      // Azure SWA
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// ==========================
// OPENAI OPTIONS + HTTP CLIENT
// ==========================
var openAiKey =
    builder.Configuration["AzureOpenAI:ApiKey"] ??
    Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

if (string.IsNullOrEmpty(openAiKey))
{
    throw new Exception("ERROR: Azure OpenAI API Key is missing.");
}

// register strongly typed config
builder.Services.AddSingleton(new AzureOpenAIOptions
{
    Endpoint = builder.Configuration["AzureOpenAI:Endpoint"]!,
    ApiKey = openAiKey,
    ApiVersion = builder.Configuration["AzureOpenAI:ApiVersion"]!,
    Model = builder.Configuration["AzureOpenAI:Model"]!,
    MaxTokens = int.Parse(builder.Configuration["AzureOpenAI:MaxTokens"] ?? "40000")
});

// configure HttpClient with auth header
builder.Services.AddHttpClient<AzureOpenAIService>((sp, client) =>
{
    var opts = sp.GetRequiredService<AzureOpenAIOptions>();
    client.DefaultRequestHeaders.Add("api-key", opts.ApiKey);
});

// ==========================
// MVC + SWAGGER
// ==========================
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Aspire defaults
builder.AddServiceDefaults();

var app = builder.Build();

// ==========================
// PIPELINE
// ==========================
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Fix HTTPS redirect for Azure App Service
if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    });
}

app.UseHttpsRedirection();

app.UseCors("BlazorClient");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Obituary}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapIdentityApi<IdentityUser>();
app.MapRazorPages().WithStaticAssets();

// RUN MIGRATIONS IN PRODUCTION (with timeout protection)
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(1000); // Give the app a moment to start
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting database migrations in background...");

            // Apply migrations with timeout protection
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                await db.Database.MigrateAsync(cts.Token);
            }

            logger.LogInformation("Database migrations completed successfully.");

            // Test connection
            var canConnect = await db.Database.CanConnectAsync();
            logger.LogInformation($"Database connection test: {(canConnect ? "SUCCESS" : "FAILED")}");
        }
    }
    catch (Exception ex)
    {
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Database migration failed: {Message}", ex.Message);
        }
    }
});

app.Run();
