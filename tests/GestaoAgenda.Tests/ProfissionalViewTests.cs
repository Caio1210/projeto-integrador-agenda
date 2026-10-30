using GestaoAgenda.Controllers;
using GestaoAgenda.Models;
using Microsoft.AspNetCore.Builder;
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
    public async Task ViewsRenderizamComLayoutEEstadosEsperados(string view, string estado)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GestaoAgenda"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProfissionalController).Assembly.FullName,
            ContentRootPath = root
        });
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ProfissionalController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        await using var app = builder.Build();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        using var scope = app.Services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "view-test"));
        var routes = new RouteData();
        routes.Values["controller"] = "Profissional";
        routes.Values["action"] = view;
        var action = new ActionContext(http, routes, new ActionDescriptor());
        var profissional = new Profissional { Id = 1, Nome = "Maria teste", Telefone = "11987654321", Email = "maria@example.com", Especialidade = "Corte", Ativo = false };
        object model = view == "Index" ? (estado == "dados" ? new List<Profissional> { profissional } : new List<Profissional>()) : profissional;
        var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        if (estado == "erro") data["Erro"] = "Falha simulada";
        var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
        var result = engine.GetView(null, $"/Views/Profissional/{view}.cshtml", true);
        Assert.True(result.Success);
        using var writer = new StringWriter();
        var context = new ViewContext(action, result.View, data,
            new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions());
        await result.View.RenderAsync(context);
        var html = writer.ToString();
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
            if (view is "Index" or "Details") Assert.Contains("Inativo", html);
            else
            {
                Assert.Contains("__RequestVerificationToken", html);
                Assert.Contains("messages_pt_BR.min.js", html);
                Assert.Contains("name=\"Ativo\"", html);
            }
        }
    }
}