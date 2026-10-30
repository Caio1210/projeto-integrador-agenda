using System.ComponentModel.DataAnnotations;
using GestaoAgenda.Controllers;
using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalTests
{
    private static Profissional Valido() => new()
    {
        Id = 1, Nome = "Maria Souza", Telefone = "(11) 98765-4321",
        Especialidade = "Cabeleireira", Email = "maria@example.com"
    };

    private static List<ValidationResult> Validar(Profissional profissional)
    {
        var erros = new List<ValidationResult>();
        Validator.TryValidateObject(profissional, new ValidationContext(profissional), erros, true);
        return erros;
    }

    [Fact]
    public void CadastroValidoIniciaAtivo()
    {
        Assert.Empty(Validar(Valido()));
        Assert.True(new Profissional().Ativo);
    }

    [Theory]
    [InlineData("Nome")]
    [InlineData("Telefone")]
    [InlineData("Especialidade")]
    public void CamposObrigatoriosRejeitamEspacos(string campo)
    {
        var profissional = Valido();
        typeof(Profissional).GetProperty(campo)!.SetValue(profissional, "   ");
        Assert.Contains(Validar(profissional), e => e.MemberNames.Contains(campo));
    }

    [Theory]
    [InlineData("Nome", 120)]
    [InlineData("Telefone", 20)]
    [InlineData("Email", 120)]
    [InlineData("Especialidade", 120)]
    public void LimitesDeTamanhoSaoValidados(string campo, int limite)
    {
        var profissional = Valido();
        typeof(Profissional).GetProperty(campo)!.SetValue(profissional, new string('a', limite + 1));
        Assert.Contains(Validar(profissional), e => e.MemberNames.Contains(campo));
    }

    [Fact]
    public void EmailOpcionalEFormatoValidadoQuandoPreenchido()
    {
        var profissional = Valido();
        profissional.Email = null;
        Assert.Empty(Validar(profissional));
        profissional.Email = "invalido";
        Assert.Contains(Validar(profissional), e => e.MemberNames.Contains("Email"));
    }

    [Fact]
    public void TelefoneInvalidoRejeitado()
    {
        var profissional = Valido();
        profissional.Telefone = "nao-e-telefone";
        Assert.Contains(Validar(profissional), e => e.MemberNames.Contains("Telefone"));
    }

    [Fact]
    public void MapeamentoSqlServerPreservaCadastroIndependente()
    {
        // Construir o modelo não abre conexão nem executa DDL.
        var options = new DbContextOptionsBuilder<GestaoAgendaContext>()
            .UseSqlServer("Server=localhost;Database=SomenteMetadados;Integrated Security=True").Options;
        using var db = new GestaoAgendaContext(options);
        var entity = db.Model.FindEntityType(typeof(Profissional))!;
        Assert.Equal("Profissional", entity.GetTableName());
        Assert.Equal(nameof(Profissional.Id), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(120, entity.FindProperty(nameof(Profissional.Nome))!.GetMaxLength());
        Assert.Equal(20, entity.FindProperty(nameof(Profissional.Telefone))!.GetMaxLength());
        Assert.Equal(120, entity.FindProperty(nameof(Profissional.Email))!.GetMaxLength());
        Assert.Equal(120, entity.FindProperty(nameof(Profissional.Especialidade))!.GetMaxLength());
        Assert.True(entity.FindProperty(nameof(Profissional.Email))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(Profissional.Ativo))!.IsNullable);
        Assert.Empty(entity.GetForeignKeys());
        Assert.Null(db.Model.FindEntityType(typeof(Agendamento))!.FindProperty("ProfissionalId"));
    }

}
