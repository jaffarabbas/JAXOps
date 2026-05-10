using FluentValidation;
using IISManager.Application.Interfaces;
using IISManager.Application.Services;
using IISManager.Application.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace IISManager.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IServerAppService, ServerAppService>();
        services.AddScoped<IDeploymentAppService, DeploymentAppService>();
        services.AddScoped<IAuditAppService, AuditAppService>();
        services.AddSingleton<IAgentConnectionRegistry, AgentConnectionRegistry>();

        services.AddValidatorsFromAssemblyContaining<CreateServerValidator>();

        return services;
    }
}
