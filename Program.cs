using Microsoft.EntityFrameworkCore;
using SwipeService.Data;
using SwipeService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Swipe Service API",
        Version = "v1",
        Description = "API documentation for the Swipe Service."
    });
    // var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    // var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    // c.IncludeXmlComments(xmlPath); // Disabled to prevent crash if XML file is missing
});

// Configure MySQL database
builder.Services.AddDbContext<SwipeContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 32))
    ));

// Register SwipeService
builder.Services.AddScoped<SwipeService.Services.SwipeService>();

// Register MatchmakingNotifier
builder.Services.AddHttpClient<MatchmakingNotifier>();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = GetPublicKey()
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Swipe Service API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// ================================
// RSA KEY MANAGEMENT
// Public key validation for JWT tokens from AuthService
// ================================

static RsaSecurityKey GetPublicKey()
{
    try
    {
        var publicKeyPath = "public.key";
        if (File.Exists(publicKeyPath))
        {
            var publicKeyPem = File.ReadAllText(publicKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return new RsaSecurityKey(rsa);
        }
        else
        {
            // For demo mode or when no key file exists, create a temporary key
            // In production, this should always use the proper public key
            var rsa = RSA.Create(2048);
            return new RsaSecurityKey(rsa);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading public key: {ex.Message}");
        // Fallback to temporary key
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa);
    }
}
