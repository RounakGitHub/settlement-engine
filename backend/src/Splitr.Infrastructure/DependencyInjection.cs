using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Splitr.Application.Interfaces;
using Splitr.Infrastructure.Caching;
using Splitr.Infrastructure.Configuration;
using Splitr.Infrastructure.Consumers;
using Splitr.Infrastructure.Jobs;
using Splitr.Infrastructure.Messaging;
using Splitr.Infrastructure.Persistence;
using Splitr.Infrastructure.Services;
using StackExchange.Redis;

namespace Splitr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));

        // Outbox channel (singleton — shared between interceptor and publisher)
        services.AddSingleton<OutboxChannel>();
        services.AddSingleton<OutboxInterceptor>();

        // PostgreSQL + EF Core
        services.AddDbContext<AppDbContext>((sp, options) =>
            options
                .UseNpgsql(configuration.GetConnectionString("PostgreSQL"))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<OutboxInterceptor>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Valkey
        var valkeyConnectionString = configuration.GetConnectionString("Valkey")!;
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(valkeyConnectionString));

        // Kafka producer
        services.AddSingleton<IProducer<string, string>>(sp =>
        {
            var kafkaOptions = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
            var config = new ProducerConfig
            {
                BootstrapServers = kafkaOptions.BootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = kafkaOptions.EnableIdempotence
            };
            return new ProducerBuilder<string, string>(config).Build();
        });

        services.AddSingleton<KafkaProducerService>();

        // Outbox publisher
        services.AddHostedService<OutboxPublisherService>();

        // Application services
        services.AddScoped<IIdempotencyService, ValkeyIdempotencyService>();
        services.AddScoped<IDistributedLockService, ValkeyDistributedLockService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Razorpay
        services.Configure<RazorpayOptions>(configuration.GetSection(RazorpayOptions.SectionName));
        services.AddSingleton<IWebhookVerifier, RazorpayWebhookVerifier>();

        // Email
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();

        // Kafka consumers
        services.AddHostedService<DebtSimplifierConsumer>();
        services.AddHostedService<SignalRDispatcherConsumer>();
        services.AddHostedService<EmailNotificationConsumer>();

        // Background jobs
        services.AddHostedService<SettlementExpiryJob>();
        services.AddHostedService<GroupArchiveCleanupJob>();

        // OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter()
                .AddSource("Splitr"))
            .WithMetrics(builder => builder
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter());

        return services;
    }
}
