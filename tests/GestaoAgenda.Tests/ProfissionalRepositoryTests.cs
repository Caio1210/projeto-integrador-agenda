using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalRepositoryTests
{
    [Fact]
    public async Task CadastroEdicaoEInativacaoPersistemEntreContextos()
    {
        var options = new DbContextOptionsBuilder<GestaoAgendaContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        int id;
        using (var db = new GestaoAgendaContext(options))
        {
            var profissional = new Profissional { Nome = "Maria", Telefone = "11987654321", Especialidade = "Corte" };
            await new ProfissionalRepository(db).CreateProfissionalAsync(profissional);
            id = profissional.Id;
            Assert.True(id > 0);
        }
        using (var db = new GestaoAgendaContext(options))
        {
            var repo = new ProfissionalRepository(db);
            var profissional = await repo.GetProfissionalAsync(id);
            Assert.NotNull(profissional);
            Assert.True(profissional.Ativo);
            profissional.Nome = "Maria Souza";
            profissional.Email = "maria@example.com";
            profissional.Especialidade = "Coloração";
            profissional.Ativo = false;
            Assert.True(await repo.UpdateProfissionalAsync(profissional));
        }
        using (var db = new GestaoAgendaContext(options))
        {
            var itens = await new ProfissionalRepository(db).GetProfissionaisAsync();
            var profissional = Assert.Single(itens);
            Assert.Equal("Maria Souza", profissional.Nome);
            Assert.Equal("maria@example.com", profissional.Email);
            Assert.Equal("Coloração", profissional.Especialidade);
            Assert.False(profissional.Ativo);
        }
    }

    [Fact]
    public async Task ListagemOrdenaENaoOcultaInativos()
    {
        using var db = new GestaoAgendaContext(new DbContextOptionsBuilder<GestaoAgendaContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var repo = new ProfissionalRepository(db);
        await repo.CreateProfissionalAsync(new Profissional { Nome = "Zelia", Ativo = false });
        await repo.CreateProfissionalAsync(new Profissional { Nome = "Ana" });
        var itens = await repo.GetProfissionaisAsync();
        Assert.Equal(new[] { "Ana", "Zelia" }, itens.Select(p => p.Nome));
        Assert.False(itens[1].Ativo);
        Assert.Null(await repo.GetProfissionalAsync(999));
        Assert.False(await repo.UpdateProfissionalAsync(new Profissional { Id = 999 }));
        Assert.Equal(2, await db.Profissionais.CountAsync());
    }
}