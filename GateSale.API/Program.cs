

using GateSale.API.Middleware;
using GateSale.Core.Entities;
using GateSale.Core.Interfaces;
using GateSale.Core.Models;
using GateSale.Infrastructure.Data;
using GateSale.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Amazon.Extensions.NETCore.Setup;
using Amazon.CognitoIdentityProvider;
using Amazon;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GateSale API", Version = "v1" });
    
    // Configure Swagger to use JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure AWS services
var awsOptions = builder.Configuration.GetAWSOptions();
// Only add explicit credentials if they are provided in the configuration
var accessKey = builder.Configuration["AWS:AccessKey"];
var secretKey = builder.Configuration["AWS:SecretKey"];

if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
{
    awsOptions.Credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
}
else
{
    // Log a warning but continue with default credential providers
    Console.WriteLine("WARNING: AWS credentials not found in configuration, using default credential provider chain");
}

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();

// Configure Cognito settings
// builder.Services.Configure<CognitoSettings>(builder.Configuration.GetSection("AWS:Cognito"));
builder.Services.Configure<CognitoSettings>(
    builder.Configuration.GetSection("AWS:Cognito")
);

// Configure Pudo settings
builder.Services.Configure<PudoSettings>(
    builder.Configuration.GetSection("Pudo")
);

// Add HttpClient for Pudo service
builder.Services.AddHttpClient<IPudoLockerService, PudoLockerService>();


// Use PostgreSQL for the database context in production or SQLite for development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<GateSaleDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}
else
{
    builder.Services.AddDbContext<GateSaleDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("ProductionConnection")));
}

// Configure JWT Authentication with Cognito
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["AWS:Cognito:Authority"];
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = builder.Environment.IsProduction(),
        ValidateAudience = builder.Environment.IsProduction(),
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        // For development only - allow test tokens
        IssuerSigningKey = !builder.Environment.IsProduction() 
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyForDevelopmentPurposesOnly12345!@#$%"))
            : null
    };
});

// Register application services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IDomainValidationService, DomainValidationService>();
builder.Services.AddScoped<ICognitoService, CognitoService>();
builder.Services.AddScoped<IStorageService, S3StorageService>();
builder.Services.AddScoped<IOrderTrackingService, OrderTrackingService>();
builder.Services.AddScoped<IPudoLockerService, PudoLockerService>();
builder.Services.AddScoped<IUserLockerService, UserLockerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<CategoryService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Use the user status middleware
app.UseUserStatusMiddleware();

app.MapControllers();
app.MapGet("/", () => "🚀 API is running!");

app.Run();
