using System.Text;
using backend.Database;
using backend.Entities;
using backend.Middleware;
using backend.Services;
using backend.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace backend;

public static class DependencyInjection
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions.TryAdd(
                    "requestId", context.HttpContext.TraceIdentifier));

        // ORDER MATTERS. IExceptionHandler implementations run in registration order,
        // and the first one to return true wins. ValidationExceptionHandler must come
        // first: it returns false for anything that isn't a ValidationException, so
        // those fall through to GlobalExceptionHandler. Registered the other way round,
        // the catch-all would swallow every validation failure and return 500 instead
        // of a 400 with the field-keyed errors dictionary.
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Origins come from config (Cors:AllowedOrigins) so the deployed origin
        // is a settings change, not a code change.
        string[] origins = configuration
            .GetSection(CorsSettings.SectionName)
            .Get<CorsSettings>()?.AllowedOrigins ?? [];

        services.AddCors(options =>
            options.AddPolicy(CorsSettings.PolicyName, policy =>
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()      // Authorization + Content-Type
                    .AllowAnyMethod()));   // includes PATCH and DELETE

        // No AllowCredentials: this API authenticates with a bearer token in a
        // header, never a cookie, so the browser has no credentials to send.

        return services;
    }

    public static WebApplicationBuilder AddAuthenticationServices(this WebApplicationBuilder builder)
    {
        builder.Services
            // stock IdentityUser: credentials only. The domain User (name, role,
            // IsActive) lives in the app schema and links back via IdentityId.
            .AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>();

        builder.Services.Configure<JwtAuthOptions>(
            builder.Configuration.GetSection(JwtAuthOptions.SectionName));
        JwtAuthOptions jwtAuthOptions = builder.Configuration
            .GetSection(JwtAuthOptions.SectionName)
            .Get<JwtAuthOptions>()!;

        builder.Services
            .AddAuthentication(options =>
            {
                // AddIdentity above sets the cookie schemes as default; these two lines
                // put JWT bearer back in front, so order matters here.
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtAuthOptions.Issuer,
                    ValidAudience = jwtAuthOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtAuthOptions.Key))
                };

                // [Authorize] failures are not exceptions, so they never reach the
                // IExceptionHandler pipeline. These two events route them through the
                // same IProblemDetailsService, so 401/403 bodies match every other error.
                options.Events = new JwtBearerEvents
                {
                    // 401 - no token, expired, or invalid signature
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();   // suppress the default empty 401
                        await WriteProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Unauthorized",
                            "Authentication is required to access this resource.");
                    },

                    // 403 - valid token, wrong role / not permitted
                    OnForbidden = async context =>
                    {
                        await WriteProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "Forbidden",
                            "You do not have permission to access this resource.");
                    }
                };
            });

        // fail closed: every endpoint requires an authenticated user unless it
        // explicitly opts out with [AllowAnonymous].
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        builder.Services.AddTransient<TokenProvider>();

        return builder;
    }

    private static async Task WriteProblemAsync(
        HttpContext context, int statusCode, string title, string detail)
    {
        // don't try to write if the response already started
        if (context.Response.HasStarted)
        {
            return;
        }

        IProblemDetailsService problemDetailsService =
            context.RequestServices.GetRequiredService<IProblemDetailsService>();

        context.Response.StatusCode = statusCode;

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });
    }
}
