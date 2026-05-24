using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StartLine.Application.Auth;
using StartLine.Application.Events;
using StartLine.Application.Registrations;
using StartLine.Application.Users;
using StartLine.Infrastructure.Auth;
using StartLine.Infrastructure.Events;
using StartLine.Infrastructure.Persistence;
using StartLine.Infrastructure.Persistence.Repositories;
using StartLine.Infrastructure.Registrations;

namespace StartLine.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core / Postgres
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IRegistrationRepository, RegistrationRepository>();

        // JWT settings
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        // Auth services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Event services
        services.AddScoped<IEventService, EventService>();

        // Registration services
        services.AddScoped<IRegistrationService, RegistrationService>();

        // JWT authentication
        var jwtSection = configuration.GetSection("Jwt");
        var secretKey = jwtSection["SecretKey"]!;
        var issuer = jwtSection["Issuer"]!;
        var audience = jwtSection["Audience"]!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };
            });

        services.AddAuthorization();

        return services;
    }
}
