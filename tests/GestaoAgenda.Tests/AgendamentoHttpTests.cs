using System.Net;
using System.Text.Json;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestaoAgenda.Tests;

public class AgendamentoHttpTests
{
    [Fact]
    public async Task EndpointListaSomenteProfissionaisElegiveisDoServico()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        using var resposta = await client.GetAsync("/agendamento/profissionais?servicoId=1");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var ofertas = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(new[] { "Ana", "Bia" }, ofertas.Select(o => o.GetProperty("nome").GetString()));
        Assert.Equal("40,00", ofertas[0].GetProperty("preco").GetString());
        Assert.Equal(60, ofertas[0].GetProperty("duracaoMinutos").GetInt32());
        using var inativos = await client.GetAsync("/agendamento/profissionais?servicoId=2");
        Assert.Equal("[]", await inativos.Content.ReadAsStringAsync());
        using var invalido = await client.GetAsync("/agendamento/profissionais?servicoId=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
    }

    [Fact]
    public async Task FormularioFuncionaSemJavascriptEGravaSnapshotsDoServidor()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var form = await AbrirAsync(client);
        form.Remove("ProfissionalServicoId");
        form["acao"] = "carregar-profissionais";
        using var carregar = await client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, carregar.StatusCode);
        var html = WebUtility.HtmlDecode(await carregar.Content.ReadAsStringAsync());
        Assert.Contains(">Ana</option>", html);
        Assert.Contains("data-preco=\"40,00\"", html);
        using (var scope = app.Services.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.ToListAsync());
        form.Remove("acao"); form["ProfissionalServicoId"] = "1";
        form["PrecoAplicado"] = "0,01"; form["DuracaoMinutosAplicada"] = "1";
        form["PrecoOriginal"] = "0,01"; form["DuracaoOriginal"] = "1";
        using var salvar = await client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, salvar.StatusCode);
        int id;
        using (var scope = app.Services.CreateScope())
        {
            var a = await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.SingleAsync();
            id = a.Id; Assert.Equal(40m, a.PrecoAplicado); Assert.Equal(60, a.DuracaoMinutosAplicada);
        }
        using var detalhes = await client.GetAsync($"/agendamento/{id}");
        html = WebUtility.HtmlDecode(await detalhes.Content.ReadAsStringAsync());
        Assert.Contains("R$ 40,00", html);
        Assert.Contains("60 min", html);
        Assert.Contains("07/01/2030 10:00", html);
        Assert.Contains("Ana", html);
        using var semana = await client.GetAsync("/agenda/semana?data=2030-01-07");
        html = WebUtility.HtmlDecode(await semana.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, semana.StatusCode);
        Assert.Contains("Ana", html);
        Assert.Contains("07/01 10:00", html);
    }

    [Theory]
    [InlineData("ProfissionalServicoId", "4")]
    [InlineData("ProfissionalServicoId", "3")]
    [InlineData("ProfissionalServicoId", "")]
    [InlineData("Status", "Inventado")]
    [InlineData("ClienteId", "999")]
    [InlineData("ServicoId", "2")]
    [InlineData("Data", "")]
    [InlineData("Hora", "")]
    public async Task PostManipuladoNaoGrava(string campo, string valor)
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var form = await AbrirAsync(client); form[campo] = valor;
        using var resposta = await client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());
        Assert.Contains("text-danger", html);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.ToListAsync());
    }

    [Fact]
    public async Task DesativacaoDepoisDoGetImpedeNovaReserva()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var form = await AbrirAsync(client);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>();
            (await db.ProfissionalServicos.SingleAsync(o => o.Id == 1)).Ativo = false;
            await db.SaveChangesAsync();
        }
        using var resposta = await client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var verificar = app.Services.CreateScope();
        Assert.Empty(await verificar.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.ToListAsync());
    }

    [Fact]
    public async Task EditaHistoricoInativoSemReprecificarERejeitaReagendamento()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var form = await AbrirAsync(client);
        using var criada = await client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, criada.StatusCode);
        int id;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>();
            id = (await db.Agendamentos.SingleAsync()).Id;
            var oferta = await db.ProfissionalServicos.SingleAsync(o => o.Id == 1);
            oferta.Preco = 999m; oferta.DuracaoMinutos = 120; oferta.Ativo = false;
            await db.SaveChangesAsync();
        }
        form = await AbrirAsync(client, $"/agendamento/editar/{id}");
        form["Id"] = id.ToString(); form["Observacao"] = "Preservar histórico";
        using var edicao = await client.PostAsync($"/agendamento/editar/{id}", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, edicao.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var a = await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.SingleAsync();
            Assert.Equal(40m, a.PrecoAplicado); Assert.Equal(60, a.DuracaoMinutosAplicada);
            Assert.Equal("Preservar histórico", a.Observacao);
        }
        form["Hora"] = "11:00:00";
        using var reagendar = await client.PostAsync($"/agendamento/editar/{id}", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, reagendar.StatusCode);
        Assert.Contains("oferta não está ativo", WebUtility.HtmlDecode(await reagendar.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task LegadoRenderizaEEditaObservacaoSemAtribuirOferta()
    {
        await using var app = await AplicacaoAsync();
        int id;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>();
            var a = new Agendamento { ClienteId = 1, ServicoId = 1, Data = new(2030, 1, 7), Hora = TimeSpan.FromHours(9) };
            db.Agendamentos.Add(a); await db.SaveChangesAsync(); id = a.Id;
        }
        using var client = app.GetTestClient();
        var form = await AbrirAsync(client, $"/agendamento/editar/{id}");
        form["Id"] = id.ToString(); form.Remove("ProfissionalServicoId"); form["Observacao"] = "Legado";
        using var resposta = await client.PostAsync($"/agendamento/editar/{id}", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        using var detalhes = await client.GetAsync($"/agendamento/{id}");
        Assert.Contains("Não informado", WebUtility.HtmlDecode(await detalhes.Content.ReadAsStringAsync()));
        using var verificar = app.Services.CreateScope();
        var antigo = await verificar.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.SingleAsync();
        Assert.Null(antigo.ProfissionalServicoId); Assert.Null(antigo.PrecoAplicado);
        Assert.Null(antigo.DuracaoMinutosAplicada);
    }

    [Fact]
    public async Task PostSemAntiforgeryNaoGrava()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var form = await AbrirAsync(client); form.Remove("__RequestVerificationToken");
        using var resposta = await client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.ToListAsync());
    }

    [Fact]
    public async Task DuasRequisicoesHttpConcorrentesNaoCriamSobreposicao()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var form = await AbrirAsync(client);
        var respostas = await Task.WhenAll(client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form)),
            client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form)));
        try
        {
            Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Redirect);
            Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.OK);
            using var scope = app.Services.CreateScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.CountAsync());
        }
        finally { foreach (var resposta in respostas) resposta.Dispose(); }
    }

    [Fact]
    public async Task ErroDeCatalogoExibeFormularioBloqueadoSemGravar()
    {
        await using var app = await AplicacaoAsync(falharCatalogo: true);
        using var client = app.GetTestClient();
        using var resposta = await client.GetAsync("/agendamento/novo");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());
        Assert.Contains("Não foi possível carregar os dados", html);
        Assert.Contains("Recarregar formulário", html);
        Assert.Contains("disabled=\"disabled\"", html);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.ToListAsync());
    }

    [Fact]
    public async Task CarregamentoSemElegiveisExibeMensagemENaoGrava()
    {
        await using var app = await AplicacaoAsync();
        using var client = app.GetTestClient();
        var form = await AbrirAsync(client);
        form["ServicoId"] = "2";
        form.Remove("ProfissionalServicoId");
        form["acao"] = "carregar-profissionais";
        using var resposta = await client.PostAsync("/agendamento/novo", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Nenhum profissional ativo oferece este serviço.",
            WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync()));
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GestaoAgendaContext>().Agendamentos.ToListAsync());
    }

    private sealed class CatalogoComFalha : IServicoRepository
    {
        public Task<List<Servico>> GetServicosAsync() => throw new InvalidOperationException("Falha simulada");
        public Task<Servico?> GetServicoAsync(int? id) => throw new NotSupportedException();
        public Task CreateServicoAsync(Servico s) => throw new NotSupportedException();
        public Task UpdateServicoAsync(Servico s) => throw new NotSupportedException();
        public Task DeleteServicoAsync(int id) => throw new NotSupportedException();
    }

    private static async Task<WebApplication> AplicacaoAsync(bool falharCatalogo = false)
    {
        var fixture = new AgendamentoServiceTests.Fixture();
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GestaoAgenda"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AgendamentoController).Assembly.FullName, ContentRootPath = root
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(AgendamentoController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddScoped(_ => fixture.Banco());
        builder.Services.AddScoped<IAgendamentoRepository, AgendamentoRepository>();
        builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
        builder.Services.AddScoped<IServicoRepository, ServicoRepository>();
        if (falharCatalogo) builder.Services.AddScoped<IServicoRepository, CatalogoComFalha>();
        builder.Services.AddScoped<AgendamentoService>();
        builder.Services.AddScoped<ProfissionalServicoService>();
        var app = builder.Build();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        return app;
    }

    private static async Task<Dictionary<string, string>> AbrirAsync(HttpClient client, string url = "/agendamento/novo")
    {
        using var resposta = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        if (resposta.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var jar = new Dictionary<string, string>();
            if (client.DefaultRequestHeaders.TryGetValues("Cookie", out var existentes))
                foreach (var item in existentes.SelectMany(c => c.Split(';')))
                {
                    var partes = item.Trim().Split('=', 2);
                    if (partes.Length == 2) jar[partes[0]] = partes[1];
                }
            foreach (var cookie in cookies)
            {
                var partes = cookie.Split(';')[0].Split('=', 2);
                if (partes.Length == 2) jar[partes[0]] = partes[1];
            }
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", jar.Select(c => $"{c.Key}={c.Value}")));
        }
        var html = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("agendamento-ofertas.js", html);
        Assert.Contains("data-profissional", html);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return new()
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value),
            ["ClienteId"] = "1", ["ServicoId"] = "1", ["ProfissionalServicoId"] = "1",
            ["Data"] = "2030-01-07", ["Hora"] = "09:00:00", ["Status"] = "Agendado"
        };
    }
}