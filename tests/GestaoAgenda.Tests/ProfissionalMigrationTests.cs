using GestaoAgenda.Data;
using GestaoAgenda.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalMigrationTests
{
    [Fact]
    public void UpCriaSomenteProfissionalSemModificarDadosOuTabelasLegadas()
    {
        var migration = new AdicionarProfissionais { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
        var create = Assert.IsType<CreateTableOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("Profissional", create.Name);
        Assert.Equal(new[] { "Id", "Nome", "Telefone", "Email", "Especialidade", "Ativo" },
            create.Columns.Select(c => c.Name));
        Assert.True(create.Columns.Single(c => c.Name == "Email").IsNullable);
        Assert.All(create.Columns.Where(c => c.Name != "Email"), c => Assert.False(c.IsNullable));
        Assert.Equal("1, 1", create.Columns.Single(c => c.Name == "Id")["SqlServer:Identity"]);
    }

    [Fact]
    public void ReversaoNaoRemoveTabelasLegadas()
    {
        var migration = new AdicionarProfissionais { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
        Assert.Equal("Profissional", Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations)).Name);
    }

    [Fact]
    public void SnapshotAtualizadoEScriptDaPrimeiraMigrationPreservado()
    {
        using var db = new GestaoAgendaContext(new DbContextOptionsBuilder<GestaoAgendaContext>()
            .UseSqlServer("Server=localhost;Database=SomenteMetadados;Integrated Security=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("0", "AdicionarProfissionais",
            MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [Profissional]", script);
        foreach (var tabela in new[] { "Cliente", "Servico", "Agendamento" })
        {
            Assert.DoesNotContain($"CREATE TABLE [{tabela}]", script);
            Assert.DoesNotContain($"DROP TABLE [{tabela}]", script);
            Assert.DoesNotContain($"ALTER TABLE [{tabela}]", script);
        }
    }
}