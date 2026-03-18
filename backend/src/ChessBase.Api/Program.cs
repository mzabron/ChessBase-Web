using System.Text;
using ChessBase.Api.Authentication;
using ChessBase.Api.Email;
using ChessBase.Application.Abstractions;
using ChessBase.Application.Abstractions.Repositories;
using ChessBase.Application.Services;
using ChessBase.Domain.Engine.Abstractions;
using ChessBase.Domain.Engine.Factories;
using ChessBase.Domain.Engine.Serialization;
using ChessBase.Domain.Engine.Services;
using ChessBase.Infrastructure.Data;
using ChessBase.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException("Jwt:SigningKey configuration is required.");
}

builder.Services.AddDbContext<ChessBaseDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ChessBaseDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IPgnParser, PgnService>();
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IGameExplorerRepository, GameExplorerRepository>();
builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IDraftImportRepository, DraftImportRepository>();
builder.Services.AddScoped<IDraftPromotionRepository, DraftPromotionRepository>();
builder.Services.AddScoped<IPositionImportCoordinator, PositionImportCoordinator>();
builder.Services.AddScoped<IBoardStateSerializer, FenBoardStateSerializer>();
builder.Services.AddScoped<IBoardStateFactory, BoardStateFactory>();
builder.Services.AddScoped<IBoardStateTransition, BitboardBoardStateTransition>();
builder.Services.AddScoped<IPositionHasher, ZobristPositionHasher>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IPgnImportService, PgnImportService>();
builder.Services.AddScoped<IDraftImportService, DraftImportService>();
builder.Services.AddScoped<IDraftPromotionService, DraftPromotionService>();
builder.Services.AddScoped<IGameExplorerService, GameExplorerService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

var app = builder.Build();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is not null)
        {
            logger.LogError(exceptionFeature.Error, "Unhandled exception while processing request {Path}", context.Request.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred. Please try again later.",
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();