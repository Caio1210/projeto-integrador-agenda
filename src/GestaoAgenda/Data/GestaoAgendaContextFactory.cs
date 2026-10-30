using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GestaoAgenda.Data;

// As ferramentas EF não devem executar Program.cs e sua carga demonstrativa.
public class GestaoAgendaContextFactory : IDesignTimeDbContextFactory<GestaoAgendaContext>
{
    public GestaoAgendaContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Configure a conexão DefaultConnection para as ferramentas EF.");
        return new GestaoAgendaContext(new DbContextOptionsBuilder<GestaoAgendaContext>()
            .UseSqlServer(connection).Options);
    }
}