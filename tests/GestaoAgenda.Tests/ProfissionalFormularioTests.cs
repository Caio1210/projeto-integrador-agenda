using System.ComponentModel.DataAnnotations;
using GestaoAgenda.Controllers;
using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories;
using GestaoAgenda.Repositories.Interfaces;
using GestaoAgenda.Services;
using GestaoAgenda.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalFormularioTests
{
    [Theory]
    [InlineData("40,00", "40.00")]
    [InlineData("40,5", "40.5")]
    [InlineData("40", "40")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("0,01", "0.01")]
    [InlineData("9.999.999.999.999.999,99", "9999999999999999.99")]
    public void ConvertePrecoPtBr(string texto, string esperado)
    {
        Assert.True(PrecoPtBr.TryParse(texto, out var preco));
        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), preco);
    }

    [Theory]
    [InlineData("40.00")]
    [InlineData("1.234")]
    [InlineData("1,234")]
    [InlineData("1,234.56")]
    [InlineData("1.23,45")]
    [InlineData("0,00")]
    [InlineData("-10,00")]
    [InlineData("1e3")]
    [InlineData("10000000000000000,00")]
    [InlineData("R$ 40,00")]
    [InlineData("")]
    [InlineData(null)]
    public void RejeitaPrecoInvalidoOuAmbiguo(string? texto) => Assert.False(PrecoPtBr.TryParse(texto, out _));

    [Fact]
    public void ValidaSomenteOfertasSelecionadas()
    {
        var item = new ServicoProfissionalFormViewModel { ServicoId = 1 };
        var erros = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(item, new ValidationContext(item), erros, true));
        item.Selecionado = true;
        Assert.False(Validator.TryValidateObject(item, new ValidationContext(item), erros, true));
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(item.Preco)));
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(item.DuracaoMinutos)));
    }

    [Fact]
    public async Task CadastroSugerePadroesESalvaPersonalizacao()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync();
        Assert.Equal(2, model.Servicos.Count);
        Assert.All(model.Servicos, o => Assert.False(o.Selecionado));
        Assert.Equal("50,00", model.Servicos[0].Preco);
        model.Servicos[0].Selecionado = true;
        model.Servicos[0].Preco = "75,50"; model.Servicos[0].DuracaoMinutos = 45;
        Assert.IsType<RedirectToActionResult>(await f.Controller.Create(model));
        f.Db.ChangeTracker.Clear();
        var oferta = await f.Db.ProfissionalServicos.SingleAsync();
        Assert.Equal(75.50m, oferta.Preco); Assert.Equal(45, oferta.DuracaoMinutos);
        Assert.All(await f.Db.Servicos.ToListAsync(), s => Assert.Equal(50m, s.Valor));
    }

    [Fact]
    public async Task EdicaoCarregaPersonalizacaoEInativaSemExcluir()
    {
        using var f = new Fixture();
        var novo = await f.NovoAsync();
        novo.Servicos[0].Selecionado = true; novo.Servicos[0].Preco = "80,00";
        await f.Controller.Create(novo);
        var id = (await f.Db.Profissionais.SingleAsync()).Id;
        var model = Assert.IsType<ProfissionalFormViewModel>(Assert.IsType<ViewResult>(await f.Controller.Edit(id)).Model);
        Assert.True(model.Servicos[0].Selecionado); Assert.Equal("80,00", model.Servicos[0].Preco);
        var idOferta = (await f.Db.ProfissionalServicos.SingleAsync()).Id;
        model.Servicos[0].Selecionado = false; model.Servicos[0].Preco = null; model.Servicos[0].DuracaoMinutos = null;
        model.Ativo = false;
        Assert.IsType<RedirectToActionResult>(await f.Controller.Edit(id, model));
        Assert.False((await f.Db.Profissionais.SingleAsync()).Ativo);
        var oferta = await f.Db.ProfissionalServicos.SingleAsync();
        Assert.Equal(idOferta, oferta.Id); Assert.False(oferta.Ativo); Assert.Equal(80m, oferta.Preco);
    }

    [Fact]
    public async Task PrecoInvalidoPreservaEntradaENaoGrava()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync();
        model.Servicos[0].Selecionado = true; model.Servicos[0].Preco = "40.00";
        Assert.Same(model, Assert.IsType<ViewResult>(await f.Controller.Create(model)).Model);
        Assert.Equal("40.00", model.Servicos[0].Preco); Assert.False(f.Controller.ModelState.IsValid);
        Assert.Empty(await f.Db.Profissionais.ToListAsync());
    }

    [Fact]
    public async Task ModelStateInvalidoNaoGrava()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync();
        f.Controller.ModelState.AddModelError("Nome", "Obrigatório");
        Assert.IsType<ViewResult>(await f.Controller.Create(model));
        Assert.Empty(await f.Db.Profissionais.ToListAsync());
    }

    [Fact]
    public async Task ListaDuplicadaOuIncompletaNaoInativaOfertasSilenciosamente()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync();
        model.Servicos.RemoveAt(1);
        Assert.IsType<ViewResult>(await f.Controller.Create(model));
        Assert.False(model.PodeSalvar); Assert.Empty(await f.Db.Profissionais.ToListAsync());
        f.Controller.ModelState.Clear();
        model = await f.NovoAsync(); model.Servicos.Add(model.Servicos[0]);
        Assert.IsType<ViewResult>(await f.Controller.Create(model));
        Assert.Empty(await f.Db.Profissionais.ToListAsync());
    }

    [Fact]
    public async Task IgnoraNomesEnviadosERejeitaServicoInexistente()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync();
        model.Servicos[0].NomeServico = "<script>injetado</script>";
        model.Servicos[0].Selecionado = true; model.Servicos[0].Preco = "texto";
        await f.Controller.Create(model);
        Assert.Equal("Corte", model.Servicos[0].NomeServico);
        f.Controller.ModelState.Clear(); model.Servicos[0].ServicoId = 999;
        Assert.IsType<ViewResult>(await f.Controller.Create(model));
        Assert.Empty(await f.Db.Profissionais.ToListAsync());
    }

    [Fact]
    public async Task ReenvioConcluidoNaoDuplicaCadastro()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync(); model.Servicos[0].Selecionado = true;
        Assert.IsType<RedirectToActionResult>(await f.Controller.Create(model));
        Assert.IsType<RedirectToActionResult>(await f.Controller.Create(model));
        Assert.Equal(1, await f.Db.Profissionais.CountAsync());
        Assert.Equal(1, await f.Db.ProfissionalServicos.CountAsync());
    }

    [Fact]
    public async Task TokenEmUsoOuInvalidoNaoGrava()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync();
        Assert.Equal(EstadoInicioEnvio.Adquirido, f.Envios.Iniciar(model.TokenEnvio, "criar"));
        Assert.IsType<ViewResult>(await f.Controller.Create(model));
        Assert.Equal(409, f.Controller.Response.StatusCode);
        f.Controller.ModelState.Clear();
        model = await f.NovoAsync(); model.TokenEnvio = "invalido";
        Assert.IsType<ViewResult>(await f.Controller.Create(model));
        Assert.Equal(409, f.Controller.Response.StatusCode);
        Assert.Empty(await f.Db.Profissionais.ToListAsync());
    }

    [Fact]
    public async Task FalhaPreservaFormularioELiberaNovaTentativa()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync(); model.Servicos[0].Selecionado = true;
        f.Falha.Falhar = true;
        Assert.Same(model, Assert.IsType<ViewResult>(await f.Controller.Create(model)).Model);
        Assert.Equal(503, f.Controller.Response.StatusCode); Assert.Empty(await f.Db.Profissionais.ToListAsync());
        f.Falha.Falhar = false; f.Controller.ModelState.Clear();
        Assert.IsType<RedirectToActionResult>(await f.Controller.Create(model));
        Assert.Equal(1, await f.Db.Profissionais.CountAsync());
    }

    [Fact]
    public async Task IdManipuladoOuInexistenteNaoGrava()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync(); model.Id = 2;
        Assert.IsType<BadRequestResult>(await f.Controller.Create(model));
        Assert.IsType<NotFoundResult>(await f.Controller.Edit(3, model));
        Assert.IsType<NotFoundResult>(await f.Controller.Edit(2, model));
        Assert.IsType<NotFoundResult>(await f.Controller.Edit(999));
        Assert.IsType<NotFoundResult>(await f.Controller.Details(999));
        Assert.Empty(await f.Db.Profissionais.ToListAsync());
    }

    [Fact]
    public async Task FalhaDeCatalogoBloqueiaEnvio()
    {
        using var f = new Fixture(falharCatalogo: true);
        var model = Assert.IsType<ProfissionalFormViewModel>(Assert.IsType<ViewResult>(await f.Controller.Create()).Model);
        Assert.False(model.PodeSalvar); Assert.Equal(503, f.Controller.Response.StatusCode);
        Assert.NotNull(f.Controller.ViewData["ErroServicos"]);
        model.Nome = "Maria"; model.Telefone = "11987654321"; model.Especialidade = "Corte";
        Assert.IsType<ViewResult>(await f.Controller.Create(model));
        Assert.Empty(await f.Db.Profissionais.ToListAsync());
    }

    [Fact]
    public async Task ListagemIncluiInativosEDetalhesIncluemOfertas()
    {
        using var f = new Fixture();
        var model = await f.NovoAsync(); model.Ativo = false; model.Servicos[0].Selecionado = true;
        await f.Controller.Create(model);
        var itens = Assert.IsType<List<Profissional>>(Assert.IsType<ViewResult>(await f.Controller.Index()).Model);
        Assert.False(Assert.Single(itens).Ativo);
        var detalhes = Assert.IsType<ProfissionalDetalhesViewModel>(
            Assert.IsType<ViewResult>(await f.Controller.Details(itens[0].Id)).Model);
        Assert.Equal("Corte", Assert.Single(detalhes.Servicos).Servico.Nome);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Edit")]
    public void EscritasExigemPostAntiforgeryEViewModel(string action)
    {
        var method = typeof(ProfissionalController).GetMethods()
            .Single(m => m.Name == action && m.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
        Assert.Equal(typeof(ProfissionalFormViewModel), method.GetParameters().Last().ParameterType);
    }

    [Fact]
    public async Task GuardaAdmiteUmUnicoEnvioConcorrente()
    {
        using var envios = new EnviosFormularioProfissional();
        var token = envios.Novo("criar");
        Assert.Equal(EstadoInicioEnvio.Invalido, envios.Iniciar(token, "editar:1"));
        var estados = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => envios.Iniciar(token, "criar"))));
        Assert.Single(estados, e => e == EstadoInicioEnvio.Adquirido);
        Assert.Equal(19, estados.Count(e => e == EstadoInicioEnvio.EmAndamento));
        envios.Finalizar(token, "criar", false);
        Assert.Equal(EstadoInicioEnvio.Adquirido, envios.Iniciar(token, "criar"));
        envios.Finalizar(token, "criar", true);
        Assert.Equal(EstadoInicioEnvio.Concluido, envios.Iniciar(token, "criar"));
    }

    [Fact]
    public async Task FalhaNaEdicaoPreservaFormularioEValoresPersistidos()
    {
        using var f = new Fixture();
        var novo = await f.NovoAsync();
        novo.Servicos[0].Selecionado = true;
        await f.Controller.Create(novo);
        var id = (await f.Db.Profissionais.SingleAsync()).Id;
        var model = Assert.IsType<ProfissionalFormViewModel>(
            Assert.IsType<ViewResult>(await f.Controller.Edit(id)).Model);
        model.Servicos[0].Preco = "90,00"; model.Servicos[0].DuracaoMinutos = 90;
        f.Falha.Falhar = true;
        Assert.Same(model, Assert.IsType<ViewResult>(await f.Controller.Edit(id, model)).Model);
        Assert.Equal("90,00", model.Servicos[0].Preco);
        Assert.Equal(503, f.Controller.Response.StatusCode);
        Assert.Equal(50m, (await f.Db.ProfissionalServicos.SingleAsync()).Preco);
        f.Falha.Falhar = false; f.Controller.ModelState.Clear();
        Assert.IsType<RedirectToActionResult>(await f.Controller.Edit(id, model));
        Assert.Equal(90m, (await f.Db.ProfissionalServicos.SingleAsync()).Preco);
        Assert.Equal(1, await f.Db.Profissionais.CountAsync());
    }

    private sealed class Fixture : IDisposable
    {
        public GestaoAgendaContext Db { get; }
        public EnviosFormularioProfissional Envios { get; } = new();
        public FalhaGravacao Falha { get; } = new();
        public ProfissionalController Controller { get; }
        public Fixture(bool falharCatalogo = false)
        {
            Db = new(new DbContextOptionsBuilder<GestaoAgendaContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(Falha).Options);
            Db.Servicos.AddRange(new Servico { Nome = "Corte", Valor = 50m, Duracao = 60 },
                new Servico { Nome = "Escova", Valor = 50m, Duracao = 45 });
            Db.SaveChanges();
            Controller = new(new ProfissionalRepository(Db),
                falharCatalogo ? new CatalogoComFalha() : new ServicoRepository(Db),
                new ProfissionalServicoService(Db, NullLogger<ProfissionalServicoService>.Instance),
                Envios, NullLogger<ProfissionalController>.Instance);
            var http = new DefaultHttpContext();
            Controller.ControllerContext = new ControllerContext { HttpContext = http };
            Controller.TempData = new TempDataDictionary(http, new TempDataFalso());
        }
        public async Task<ProfissionalFormViewModel> NovoAsync()
        {
            var model = Assert.IsType<ProfissionalFormViewModel>(Assert.IsType<ViewResult>(await Controller.Create()).Model);
            model.Nome = "Maria"; model.Telefone = "11987654321"; model.Especialidade = "Corte";
            return model;
        }
        public void Dispose() { Db.Dispose(); Envios.Dispose(); }
    }
    private sealed class FalhaGravacao : SaveChangesInterceptor
    {
        public bool Falhar { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Falhar) throw new DbUpdateException("Falha simulada");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
    private sealed class TempDataFalso : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class CatalogoComFalha : IServicoRepository
    {
        public Task<List<Servico>> GetServicosAsync() => throw new InvalidOperationException("Falha simulada");
        public Task<Servico?> GetServicoAsync(int? id) => throw new NotSupportedException();
        public Task CreateServicoAsync(Servico s) => throw new NotSupportedException();
        public Task UpdateServicoAsync(Servico s) => throw new NotSupportedException();
        public Task DeleteServicoAsync(int id) => throw new NotSupportedException();
    }
}