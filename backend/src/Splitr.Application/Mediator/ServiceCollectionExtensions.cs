using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Splitr.Application.Mediator;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMediator(Assembly assembly)
        {
            services.AddScoped<ISender, Mediator>();

            var handlerInterfaceType = typeof(IRequestHandler<,>);

            foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
            {
                foreach (var iface in type.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == handlerInterfaceType)
                    {
                        services.AddScoped(iface, type);
                    }
                }
            }

            return services;
        }

        public IServiceCollection AddPipelineBehavior(Type behaviorType)
        {
            services.AddTransient(typeof(IPipelineBehavior<,>), behaviorType);
            return services;
        }
    }
}
