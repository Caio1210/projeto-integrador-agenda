using GestaoAgenda.Controllers;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestaoAgenda.Tests;

public class ServicoExclusaoTests
{
    [Fact]
    public async Task FalhaDeExclusaoInformaTambemVinculosComProfissionais()
    {
        var repo = new RepositorioComVinculo();
        var http = new DefaultHttpContext();
        var controller = new ServicoController(repo)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new TempDataFalso())
        };
        var resultado = Assert.IsType<RedirectToActionResult>(await controller.DeleteConfirmed(7));
        Assert.Equal("Index", resultado.ActionName);
        Assert.Equal(7, repo.IdSolicitado);
        Assert.Contains("vínculos com profissionais", Assert.IsType<string>(controller.TempData["Erro"]));
    }

    private sealed class RepositorioComVinculo : IServicoRepository
    {
        public int IdSolicitado { get; private set; }
        public Task DeleteServicoAsync(int id)
        {
            IdSolicitado = id;
            throw new DbUpdateException("Exclusão rejeitada por vínculo existente.");
        }
        public Task<List<Servico>> GetServicosAsync() => throw new NotSupportedException();
        public Task<Servico?> GetServicoAsync(int? id) => throw new NotSupportedException();
        public Task CreateServicoAsync(Servico s) => throw new NotSupportedException();
        public Task UpdateServicoAsync(Servico s) => throw new NotSupportedException();
    }

    private sealed class TempDataFalso : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}