using IISManager.Application.Interfaces;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;
using IISManager.Infrastructure.Repositories;
using IISManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IISManager.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseFactory, DatabaseFactory>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();
        services.AddScoped<IWebsiteRepository, WebsiteRepository>();
        services.AddScoped<IApplicationPoolRepository, ApplicationPoolRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IServerHealthRepository, ServerHealthRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPackageRepository, PackageRepository>();

        services.AddScoped<IAgentCommunicationService, AgentCommunicationService>();

        return services;
    }
}
