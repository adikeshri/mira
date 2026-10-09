using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Mira.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s)
    {
        s.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<ValidationException>());
        s.AddSingleton<Music.NowPlayingState>();
        return s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    }
}
