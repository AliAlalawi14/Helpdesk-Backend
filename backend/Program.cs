using System.Text.Json.Serialization;
using backend;
using backend.Database;
using backend.DTOs.Categories;
using backend.DTOs.Metrics;
using backend.DTOs.Tickets;
using backend.DTOs.Users;
using backend.Entities;
using backend.Extensions;
using backend.Services;
using backend.Settings;
using backend.Services.Sorting;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Database"),
            npgsqlOptions => npgsqlOptions
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
        .UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Database"),
            npgsqlOptions => npgsqlOptions
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Identity))
        .UseSnakeCaseNamingConvention());

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddErrorHandling();

builder.Services.AddCorsPolicy(builder.Configuration);

builder.AddAuthenticationServices();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();   // backs UserContext.GetUserAsync
builder.Services.AddScoped<UserContext>();

builder.Services.Configure<MetricsOptions>(
    builder.Configuration.GetSection(MetricsOptions.SectionName));

builder.Services.AddTransient<SortMappingProvider>();
builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<TicketDto, Ticket>>(_ =>
    TicketMappings.SortMapping);
builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<CategoryDto, Category>>(_ =>
    CategoryMappings.SortMapping);
// <UserDto, User> like the other two: role is a column on the domain user now, so user
// sorting runs before projection - see UserMappings.
builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<UserDto, User>>(_ =>
    UserMappings.SortMapping);
// Requester metrics sort on the aggregated shape, so source and destination match.
builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<RequesterMetricsDto, RequesterMetricsDto>>(_ =>
    RequesterMetricsMappings.SortMapping);


WebApplication app = builder.Build();

// CORS goes ahead of the exception handler on purpose. UseExceptionHandler
// clears the response (headers included) before writing its ProblemDetails, so
// a CORS middleware registered after it would have its headers wiped on every
// 400/500 - the browser would then report an opaque CORS failure instead of
// showing the validation errors the API actually returned.
app.UseCors(CorsSettings.PolicyName);

// Wraps everything downstream.
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    await app.ApplyMigrationsAsync();          // app context
    await app.ApplyIdentityMigrationsAsync();  // identity context
    await app.SeedUsersAsync();                // identity + domain users (must precede tickets)
    await app.SeedInitialDataAsync();          // categories + tickets
}


// Not in Development: the dev http profile publishes only :5000, and the
// container maps :8081 without a listener the browser can reach - either way a
// 307 to https sends the frontend to a dead port and the preflight dies there.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync().ConfigureAwait(false);
