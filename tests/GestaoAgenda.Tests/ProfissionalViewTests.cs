using System.Net;
using GestaoAgenda.Controllers;
using GestaoAgenda.Models;
using GestaoAgenda.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalViewTests
{
    [Theory]
    [InlineData("Index", "dados")]
    [InlineData("Index", "vazio")]
    [InlineData("Index", "erro")]
    [InlineData("Create", "dados")]
    [InlineData("Edit", "dados")]
    [InlineData("Details", "dados")]
    [InlineData("Create", "sem-servicos")]
    [InlineData("Edit", "erro-servicos")]
    [InlineData("Details", "sem-servicos")]
    public async Task ViewsRenderizamComLayoutEEstadosEsperados(string view, string estado)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GestaoAgenda"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProfissionalController).Assembly.FullName, ContentRootPath = root
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ProfissionalController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        await using var app = builder.Build();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        using var scope = app.Services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "view-test"));
        var routes = new RouteData();
        routes.Values["controller"] = "Profissional";
        routes.Values["action"] = view;
        var action = new ActionContext(http, routes, new ActionDescriptor());
        var profissional = new Profissional { Id = 1, Nome = "Maria teste", Telefone = "11987654321",
            Email = "maria@example.com", Especialidade = "Corte", Ativo = false };
        var formulario = ProfissionalFormViewModel.FromEntity(profissional);
        formulario.TokenEnvio = "token-teste";
        formulario.PodeSalvar = estado != "erro-servicos";
        if (estado == "dados")
            formulario.Servicos.Add(new() { ServicoId = 2, NomeServico = "Escova personalizada",
                Selecionado = true, Ativo = false, Preco = "1.234,56", DuracaoMinutos = 45 });
        var associacoes = estado == "dados" ? new List<ProfissionalServico>
        {
            new() { Id = 3, ProfissionalId = 1, ServicoId = 2, Preco = 1234.56m, DuracaoMinutos = 45,
                Ativo = false, Servico = new Servico { Id = 2, Nome = "Escova personalizada" } }
        } : [];
        object model = view switch
        {
            "Index" => estado == "dados" ? new List<Profissional> { profissional } : [],
            "Details" => new ProfissionalDetalhesViewModel(profissional, associacoes),
            _ => formulario
        };
        var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        if (estado == "erro") data["Erro"] = "Falha simulada";
        if (estado == "erro-servicos") data["ErroServicos"] = "Erro ao carregar ofertas";
        var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
        var result = engine.GetView(null, $"/Views/Profissional/{view}.cshtml", true);
        Assert.True(result.Success);
        using var writer = new StringWriter();
        await result.View.RenderAsync(new ViewContext(action, result.View, data,
            new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions()));
        var html = WebUtility.HtmlDecode(writer.ToString());
        Assert.Contains("Profissionais", html);
        Assert.Contains("name=\"viewport\"", html);
        if (estado == "vazio") Assert.Contains("Nenhum profissional cadastrado", html);
        else if (estado == "erro")
        {
            Assert.Contains("Falha simulada", html);
            Assert.DoesNotContain("Nenhum profissional cadastrado", html);
        }
        else
        {
            Assert.Contains("Maria teste", html);
            Assert.Contains("Corte", html);
            if (view != "Index") Assert.Contains("Serviços oferecidos", html);
            if (view is "Create" or "Edit")
            {
                Assert.Contains("__RequestVerificationToken", html);
                Assert.Contains("messages_pt_BR.min.js", html);
                Assert.Contains("profissional-servicos.js", html);
                Assert.Contains("name=\"Ativo\"", html);
                Assert.Contains("name=\"TokenEnvio\"", html);
                if (estado == "dados")
                {
                    Assert.Contains("name=\"Servicos[0].Selecionado\"", html);
                    Assert.Contains("1.234,56", html);
                    Assert.Contains("max=\"1440\"", html);
                    Assert.Contains("col-12 col-md-4", html);
                }
                if (estado == "erro-servicos")
                {
                    Assert.Contains("Erro ao carregar ofertas", html);
                    Assert.Contains("disabled=\"disabled\"", html);
                }
            }
            if (view == "Details" && estado == "dados")
            {
                Assert.Contains("R$ 1.234,56", html);
                Assert.Contains("45 min", html);
                Assert.Contains("Inativo", html);
                Assert.Contains("/profissional/editar/1#servico-2", html);
            }
        }
    }
}