using Microsoft.Extensions.DependencyInjection;

namespace Splitr.Application.Mediator;

public sealed class Mediator(IServiceProvider serviceProvider) : ISender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = serviceProvider.GetServices(behaviorType).Cast<object>().ToList();

        var handleMethod = handlerType.GetMethod("Handle")!;
        RequestHandlerDelegate<TResponse> pipeline = () => (Task<TResponse>)handleMethod.Invoke(handler, [request, cancellationToken])!;

        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var current = pipeline;
            var behaviorHandleMethod = behaviorType.GetMethod("Handle")!;
            pipeline = () => (Task<TResponse>)behaviorHandleMethod.Invoke(behavior, [request, current, cancellationToken])!;
        }

        return pipeline();
    }
}
