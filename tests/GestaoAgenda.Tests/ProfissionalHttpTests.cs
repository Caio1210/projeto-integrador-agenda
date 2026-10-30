using System.Net;
using System.Text.RegularExpressions;
using GestaoAgenda.Controllers;
using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories;
using GestaoAgenda.Repositories.Interfaces;
using GestaoAgenda.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalHttpTests
{
    [Fact]
    public async Task FormularioRealGravaOfertasComBindingPtBrEReenvioSemDuplicidade()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var formulario = await AbrirAsync(client);
        using var primeira = await client.PostAsync("/profissional/novo", new FormUrlEncodedContent(formulario));
        Assert.Equal(HttpStatusCode.Redirect, primeira.StatusCode);
        using var segunda = await client.PostAsync("/profissional/novo", new FormUrlEncodedContent(formulario));
        Assert.Equal(HttpStatusCode.Redirect, segunda.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>();
        Assert.Equal(1, await db.Profissionais.CountAsync());
        var oferta = await db.ProfissionalServicos.SingleAsync();
        Assert.Equal(1234.56m, oferta.Preco);
        Assert.Equal(45, oferta.DuracaoMinutos);
        Assert.Equal(50m, (await db.Servicos.FirstAsync()).Valor);
    }

    [Theory]
    [InlineData("40.00", "45")]
    [InlineData("0,00", "45")]
    [InlineData("40,00", "0")]
    [InlineData("40,00", "1441")]
    public async Task PostManipuladoValidaPrecoEDuracaoNoServidor(string preco, string duracao)
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var formulario = await AbrirAsync(client);
        formulario["Servicos[0].Preco"] = preco;
        formulario["Servicos[0].DuracaoMinutos"] = duracao;
        using var response = await client.PostAsync("/profissional/novo", new FormUrlEncodedContent(formulario));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("text-danger", html);
        Assert.Contains("Maria HTTP", html);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Profissionais.ToListAsync());
    }

    [Fact]
    public async Task PostSemAntiforgeryERejeitado()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var formulario = await AbrirAsync(client);
        formulario.Remove("__RequestVerificationToken");
        using var response = await client.PostAsync("/profissional/novo", new FormUrlEncodedContent(formulario));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Profissionais.ToListAsync());
    }

    [Fact]
    public async Task CamposDeSaidaEGrafosNaoPodemSerGravadosPeloPost()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var formulario = await AbrirAsync(client);
        formulario["PodeSalvar"] = "false";
        formulario["Servicos[0].NomeServico"] = "Nome adulterado";
        formulario["ServicosOferecidos[0].Preco"] = "1";
        formulario["ServicosOferecidos[0].Servico.Nome"] = "Serviço adulterado";
        using var response = await client.PostAsync("/profissional/novo", new FormUrlEncodedContent(formulario));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>();
        Assert.Equal("Corte", (await db.Servicos.OrderBy(s => s.Id).FirstAsync()).Nome);
        Assert.Equal(1234.56m, (await db.ProfissionalServicos.SingleAsync()).Preco);
    }

    [Fact]
    public async Task EdicaoHttpAtualizaOfertaExistenteEPermiteDesativar()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var formulario = await AbrirAsync(client);
        using var cadastro = await client.PostAsync("/profissional/novo", new FormUrlEncodedContent(formulario));
        Assert.Equal(HttpStatusCode.Redirect, cadastro.StatusCode);
        int id;
        int ofertaId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>();
            id = (await db.Profissionais.SingleAsync()).Id;
            ofertaId = (await db.ProfissionalServicos.SingleAsync()).Id;
        }
        formulario = await AbrirAsync(client, $"/profissional/editar/{id}");
        formulario["Id"] = id.ToString();
        formulario["Servicos[0].Preco"] = "90,50";
        formulario["Servicos[0].DuracaoMinutos"] = "90";
        formulario["Servicos[0].Ativo"] = "false";
        using var edicao = await client.PostAsync($"/profissional/editar/{id}", new FormUrlEncodedContent(formulario));
        Assert.Equal(HttpStatusCode.Redirect, edicao.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>();
            var oferta = await db.ProfissionalServicos.SingleAsync();
            Assert.Equal(ofertaId, oferta.Id);
            Assert.Equal(90.50m, oferta.Preco);
            Assert.Equal(90, oferta.DuracaoMinutos);
            Assert.False(oferta.Ativo);
            Assert.Equal(50m, (await db.Servicos.OrderBy(s => s.Id).FirstAsync()).Valor);
        }
        using var detalhes = await client.GetAsync($"/profissional/{id}");
        var html = WebUtility.HtmlDecode(await detalhes.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detalhes.StatusCode);
        Assert.Contains("R$ 90,50", html);
        Assert.Contains("90 min", html);
        Assert.Contains("Inativo", html);
    }

    private static async Task<WebApplication> AplicacaoAsync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GestaoAgenda"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProfissionalController).Assembly.FullName, ContentRootPath = root
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ProfissionalController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        var nomeBanco = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<GestaoAgendaContext>(options => options.UseInMemoryDatabase(nomeBanco));
        builder.Services.AddScoped<IProfissionalRepository, ProfissionalRepository>();
        builder.Services.AddScoped<IServicoRepository, ServicoRepository>();
        builder.Services.AddScoped<ProfissionalServicoService>();
        builder.Services.AddSingleton<EnviosFormularioProfissional>();
        var app = builder.Build();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>();
            db.Servicos.AddRange(new Servico { Nome = "Corte", Valor = 50m, Duracao = 60 },
                new Servico { Nome = "Escova", Valor = 80m, Duracao = 45 });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        return app;
    }

    private static async Task<Dictionary<string, string>> AbrirAsync(HttpClient client, string url = "/profissional/novo")
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies.Select(c => c.Split(';')[0])));
        var html = await response.Content.ReadAsStringAsync();
        string Campo(string nome)
        {
            var match = Regex.Match(html, $"name=\"{Regex.Escape(nome)}\"[^>]*value=\"([^\"]*)\"");
            Assert.True(match.Success, $"Campo ausente: {nome}");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        return new()
        {
            ["__RequestVerificationToken"] = Campo("__RequestVerificationToken"),
            ["TokenEnvio"] = Campo("TokenEnvio"),
            ["Nome"] = "Maria HTTP", ["Telefone"] = "11987654321", ["Especialidade"] = "Corte",
            ["Ativo"] = "true",
            ["Servicos[0].ServicoId"] = Campo("Servicos[0].ServicoId"),
            ["Servicos[0].Selecionado"] = "true", ["Servicos[0].Ativo"] = "true",
            ["Servicos[0].Preco"] = "1.234,56", ["Servicos[0].DuracaoMinutos"] = "45",
            ["Servicos[1].ServicoId"] = Campo("Servicos[1].ServicoId"),
            ["Servicos[1].Selecionado"] = "false"
        };
    }
}