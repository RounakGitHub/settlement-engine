using Serilog;
using Splitr.API.Configuration;
using Splitr.API.Hubs;
using Splitr.API.Middleware;
using Splitr.API.Services;
using Splitr.Application;
using Splitr.Application.Interfaces;
using Splitr.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}",
        theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}",
            theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code));

    // Clean Architecture DI
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddAuth(builder.Configuration);

    // CORS
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // Controllers + Swagger
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // SignalR with Valkey backplane
    var valkeyConnectionString = builder.Configuration.GetConnectionString("Valkey")!;
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(valkeyConnectionString);

    builder.Services.AddSingleton<ISignalRDispatcher, GroupHubDispatcher>();

    // Health Checks
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseHsts();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseRateLimiter();

    app.UseMiddleware<IdempotencyMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers().RequireRateLimiting("general");

    app.MapHub<GroupHub>("/api/hubs/groups");

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false // Liveness: always healthy if process is running
    });

    app.MapHealthChecks("/health/ready");

    Log.Information("Splitr API starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
