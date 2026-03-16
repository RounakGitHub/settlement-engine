using System.Reflection;
using FluentValidation;
using Splitr.Application.Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Splitr.Application.Behaviours;
using Splitr.Application.Configuration;

namespace Splitr.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediator(assembly);

        services.AddValidatorsFromAssembly(assembly);

        services.AddPipelineBehavior(typeof(ValidationBehaviour<,>));
        services.AddPipelineBehavior(typeof(AuthorisationBehaviour<,>));

        // Application-layer options
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<GroupOptions>(configuration.GetSection(GroupOptions.SectionName));
        services.Configure<SettlementOptions>(configuration.GetSection(SettlementOptions.SectionName));

        return services;
    }
}
