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

    [Fact]
    public async Task ListagemIncluiAtivosEInativos()
    {
        var repo = new RepositorioFalso { Items = [Valido(), new Profissional { Id = 2, Ativo = false }] };
        var view = Assert.IsType<ViewResult>(await Controller(repo).Index());
        var itens = Assert.IsType<List<Profissional>>(view.Model);
        Assert.Equal(2, itens.Count);
        Assert.Contains(itens, p => !p.Ativo);
    }

    [Fact]
    public async Task CadastroValidoSalvaERedireciona()
    {
        var repo = new RepositorioFalso();
        var result = Assert.IsType<RedirectToActionResult>(await Controller(repo).Create(Valido()));
        Assert.Equal("Index", result.ActionName);
        Assert.Equal(1, repo.Gravacoes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModelStateInvalidoNaoGrava(bool edicao)
    {
        var repo = new RepositorioFalso();
        var controller = Controller(repo);
        controller.ModelState.AddModelError("Nome", "Obrigatório");
        var result = edicao ? await controller.Edit(1, Valido()) : await controller.Create(Valido());
        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, repo.Gravacoes);
    }

    [Fact]
    public async Task EdicaoPermiteInativarSemExcluir()
    {
        var repo = new RepositorioFalso();
        var profissional = Valido();
        profissional.Ativo = false;
        Assert.IsType<RedirectToActionResult>(await Controller(repo).Edit(1, profissional));
        Assert.False(repo.Ultimo!.Ativo);
        Assert.Equal(1, repo.Gravacoes);
    }

    [Fact]
    public async Task IdDivergenteNaoGrava()
    {
        var repo = new RepositorioFalso();
        Assert.IsType<NotFoundResult>(await Controller(repo).Edit(2, Valido()));
        Assert.Equal(0, repo.Gravacoes);
    }

    [Fact]
    public async Task ProfissionalInexistenteRetorna404()
    {
        var controller = Controller(new RepositorioFalso { Existe = false });
        Assert.IsType<NotFoundResult>(await controller.Details(123));
        Assert.IsType<NotFoundResult>(await controller.Edit(123));
        Assert.IsType<NotFoundResult>(await controller.Edit(1, Valido()));
    }

    [Fact]
    public async Task FalhaDeLeituraNaoApareceComoListaVazia()
    {
        var controller = Controller(new RepositorioFalso { Falhar = true });
        var view = Assert.IsType<ViewResult>(await controller.Index());
        Assert.Equal(503, controller.Response.StatusCode);
        Assert.NotNull(view.ViewData["Erro"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FalhaAoSalvarPreservaFormulario(bool edicao)
    {
        var controller = Controller(new RepositorioFalso { Falhar = true });
        var profissional = Valido();
        var result = edicao ? await controller.Edit(1, profissional) : await controller.Create(profissional);
        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(profissional, view.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(503, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Edit")]
    public void EscritasExigemPostEAntiforgery(string action)
    {
        var method = typeof(ProfissionalController).GetMethods()
            .Single(m => m.Name == action && m.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
        Assert.NotNull(method.GetParameters().Last().GetCustomAttributes(typeof(BindAttribute), true).Single());
    }

    private static ProfissionalController Controller(RepositorioFalso repo)
    {
        var http = new DefaultHttpContext();
        return new ProfissionalController(repo, NullLogger<ProfissionalController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new TempDataFalso())
        };
    }

    private class TempDataFalso : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private class RepositorioFalso : IProfissionalRepository
    {
        public List<Profissional> Items { get; set; } = [];
        public bool Existe { get; set; } = true;
        public bool Falhar { get; set; }
        public int Gravacoes { get; private set; }
        public Profissional? Ultimo { get; private set; }
        private void Verificar() { if (Falhar) throw new InvalidOperationException("Falha simulada"); }
        public Task<List<Profissional>> GetProfissionaisAsync() { Verificar(); return Task.FromResult(Items); }
        public Task<Profissional?> GetProfissionalAsync(int id) { Verificar(); return Task.FromResult<Profissional?>(Existe ? Valido() : null); }
        public Task CreateProfissionalAsync(Profissional profissional) { Verificar(); Gravacoes++; Ultimo = profissional; return Task.CompletedTask; }
        public Task<bool> UpdateProfissionalAsync(Profissional profissional)
        {
            Verificar();
            if (!Existe) return Task.FromResult(false);
            Gravacoes++; Ultimo = profissional;
            return Task.FromResult(true);
        }
    }
}