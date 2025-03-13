using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using pentyflixApi.Data;
using pentyflixApi.Models;
using pentyflixApi.Models.UserModel;
using pentyflixApi.Services;
using System.Net;
using System.Net.Http.Headers;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add PostgreSQL DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]))
    };

    // Add custom unauthorized response
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            // Avoid default response
            context.HandleResponse();

            // Custom response
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new { message = "You are not authorized to access this resource" });

            await context.Response.WriteAsync(result);
        }
    };
});

// Register AuthService
builder.Services.AddScoped<AuthService>();

// Configure HttpClient for RedditService
builder.Services.AddHttpClient<RedditService>(client =>
{
    // Set User-Agent to look like a normal browser to avoid being blocked
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

    // Add other headers to make the request look more like a browser
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
    client.DefaultRequestHeaders.Add("Connection", "keep-alive");

    // Set a reasonable timeout
    client.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    AllowAutoRedirect = true,
    UseCookies = true,
    CookieContainer = new CookieContainer()
})
.AddTransientHttpErrorPolicy(policy => policy
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            var serviceProvider = builder.Services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<RedditService>>();
            logger.LogWarning($"Delaying for {timespan.TotalSeconds} seconds, then making retry {retryCount} for {context.OperationKey}");
        }))
.AddTransientHttpErrorPolicy(policy => policy
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// Configure HttpClient for RedditCategoryService  
builder.Services.AddHttpClient<RedditCategoryService>(client =>
{
    // Random rotation of realistic user agents
    var userAgents = new[]
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15",
        "Mozilla/5.0 (iPhone; CPU iPhone OS 14_7_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.2 Mobile/15E148 Safari/604.1"
    };
    var random = new Random();
    var selectedUserAgent = userAgents[random.Next(userAgents.Length)];

    client.DefaultRequestHeaders.UserAgent.ParseAdd(selectedUserAgent);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
    client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");

    // Some sites check for this header
    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
    client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

    client.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    AllowAutoRedirect = true,
    UseCookies = true,
    CookieContainer = new CookieContainer(),
    // In some cases, you might need to bypass certificate validation for testing
    // ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
})
.AddPolicyHandler(GetRetryPolicy(builder))
.AddPolicyHandler(GetCircuitBreakerPolicy(builder));

builder.Services.AddScoped<RedditService>();
builder.Services.AddScoped<INsfwKeywordService, NsfwKeywordService>();

builder.Services.AddMemoryCache();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use CORS middleware (must be before auth middleware)
app.UseCors("AllowAllOrigins");

// Add these middleware in the correct order
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Create the database if it doesn't exist
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(WebApplicationBuilder builder)
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            3, // Number of retries
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                var serviceProvider = builder.Services.BuildServiceProvider();
                var logger = serviceProvider.GetService<ILogger<Program>>();

                logger?.LogWarning($"Request failed with {outcome.Result?.StatusCode}. Delaying for {timespan.TotalSeconds} seconds before retry {retryAttempt}");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(WebApplicationBuilder builder)
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, timespan) =>
            {
                var serviceProvider = builder.Services.BuildServiceProvider();
                var logger = serviceProvider.GetService<ILogger<Program>>();

                logger?.LogWarning($"Circuit breaker opened for {timespan.TotalSeconds} seconds due to failures");
            },
            onReset: () =>
            {
                var serviceProvider = builder.Services.BuildServiceProvider();
                var logger = serviceProvider.GetService<ILogger<Program>>();

                logger?.LogInformation("Circuit breaker reset");
            });
}

app.Run();