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

// Register HttpClient
builder.Services.AddHttpClient<RedditService>();

// Register RedditService
builder.Services.AddScoped<RedditService>();
// Register HttpClient and services for Reddit category functionality
builder.Services.AddHttpClient<RedditCategoryService>();
builder.Services.AddScoped<RedditCategoryService>();
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

app.Run();