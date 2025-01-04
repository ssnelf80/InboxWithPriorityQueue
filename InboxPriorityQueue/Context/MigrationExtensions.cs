using System.Reflection;
using System.Runtime.CompilerServices;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InboxPriorityQueue.Context;

public static class MigrationExtensions
{
    public static IHost MigrateInboxDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        migrationService.MigrateUp();
        return host;
    }

    public static void ConfigureFluentMigrator(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddPostgres()
                .WithGlobalConnectionString(s => s.GetService<IConfiguration>()!.GetConnectionString("SqlConnection"))
                .WithMigrationsIn(Assembly.GetAssembly(typeof(MigrationExtensions)))
            );
    }
}