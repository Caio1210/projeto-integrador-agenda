using GestaoAgenda.Data;
using GestaoAgenda.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalServicoMigrationTests
{
    private static AssociarServicosAProfissionais Migration() =>
        new() { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };

    [Fact]
    public void MigrationContemSomenteAdicoesPrevistas()
    {
        var operacoes = Migration().UpOperations;
        Assert.All(operacoes, op => Assert.True(op is CreateTableOperation or AddColumnOperation
            or CreateIndexOperation or AddCheckConstraintOperation or AddForeignKeyOperation,
            $"Operação inesperada: {op.GetType().Name}"));
        Assert.Equal("ProfissionalServico", Assert.Single(operacoes.OfType<CreateTableOperation>()).Name);
        Assert.Equal(3, operacoes.OfType<AddColumnOperation>().Count());
        Assert.Equal(3, operacoes.OfType<CreateIndexOperation>().Count());
        Assert.Equal(2, operacoes.OfType<AddCheckConstraintOperation>().Count());
        var fk = Assert.Single(operacoes.OfType<AddForeignKeyOperation>());
        Assert.Equal("Agendamento", fk.Table);
        Assert.Equal("ProfissionalServico", fk.PrincipalTable);
        Assert.Equal(ReferentialAction.Restrict, fk.OnDelete);
    }

    [Fact]
    public void ColunasLegadasContinuamNulasSemDefaultOuBackfill()
    {
        var colunas = Migration().UpOperations.OfType<AddColumnOperation>().ToList();
        Assert.Equal(new[] { "DuracaoMinutosAplicada", "PrecoAplicado", "ProfissionalServicoId" },
            colunas.Select(c => c.Name).OrderBy(n => n));
        Assert.All(colunas, c =>
        {
            Assert.Equal("Agendamento", c.Table);
            Assert.True(c.IsNullable);
            Assert.Null(c.DefaultValue);
            Assert.Null(c.DefaultValueSql);
            Assert.Null(c.ComputedColumnSql);
        });
        var preco = colunas.Single(c => c.Name == "PrecoAplicado");
        Assert.Equal("decimal(18,2)", preco.ColumnType);
    }

    [Fact]
    public void NovaTabelaTemUnicidadeChecksEFksSemCascata()
    {
        var operacoes = Migration().UpOperations;
        var tabela = Assert.Single(operacoes.OfType<CreateTableOperation>());
        Assert.Equal(2, tabela.ForeignKeys.Count);
        Assert.All(tabela.ForeignKeys, fk => Assert.Equal(ReferentialAction.Restrict, fk.OnDelete));
        Assert.Equal(new[] { "Profissional", "Servico" }, tabela.ForeignKeys.Select(fk => fk.PrincipalTable).OrderBy(n => n));
        Assert.Equal(2, tabela.CheckConstraints.Count);
        var indice = Assert.Single(operacoes.OfType<CreateIndexOperation>(), i => i.IsUnique);
        Assert.Equal("ProfissionalServico", indice.Table);
        Assert.Equal(new[] { "ProfissionalId", "ServicoId" }, indice.Columns);
        var preco = tabela.Columns.Single(c => c.Name == "Preco");
        Assert.Equal("decimal(18,2)", preco.ColumnType);
    }

    [Fact]
    public void ReversaoLimitaSeAosObjetosAdicionados()
    {
        var operacoes = Migration().DownOperations;
        Assert.Equal("ProfissionalServico", Assert.Single(operacoes.OfType<DropTableOperation>()).Name);
        var colunas = operacoes.OfType<DropColumnOperation>().ToList();
        Assert.Equal(new[] { "DuracaoMinutosAplicada", "PrecoAplicado", "ProfissionalServicoId" },
            colunas.Select(c => c.Name).OrderBy(n => n));
        Assert.All(colunas, c => Assert.Equal("Agendamento", c.Table));
        Assert.All(operacoes, op => Assert.True(op is DropTableOperation or DropColumnOperation
            or DropIndexOperation or DropCheckConstraintOperation or DropForeignKeyOperation));
    }

    [Fact]
    public void ScriptIncrementalNaoRecriaTabelasNemAtualizaDadosExistentes()
    {
        using var db = new GestaoAgendaContext(new DbContextOptionsBuilder<GestaoAgendaContext>()
            .UseSqlServer("Server=localhost;Database=SomenteMetadados;Integrated Security=True").Options);
        var script = db.GetService<IMigrator>().GenerateScript("AdicionarProfissionais",
            "AssociarServicosAProfissionais", MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [ProfissionalServico]", script);
        Assert.Contains("ALTER TABLE [Agendamento] ADD [PrecoAplicado] decimal(18,2) NULL", script);
        Assert.Contains("CREATE UNIQUE INDEX [IX_ProfissionalServico_ProfissionalId_ServicoId]", script);
        Assert.Contains("IF NOT EXISTS", script);
        Assert.Contains("[__EFMigrationsHistory]", script);
        Assert.DoesNotContain("UPDATE ", script);
        Assert.DoesNotContain("DELETE FROM ", script);
        Assert.DoesNotContain("DROP ", script);
        Assert.DoesNotContain("ON DELETE CASCADE", script);
        foreach (var tabela in new[] { "Cliente", "Servico", "Profissional", "Agendamento" })
            Assert.DoesNotContain($"CREATE TABLE [{tabela}]", script);
        foreach (var tabela in new[] { "Cliente", "Servico", "Profissional" })
            Assert.DoesNotContain($"ALTER TABLE [{tabela}]", script);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}