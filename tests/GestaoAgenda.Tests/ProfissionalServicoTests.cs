using System.ComponentModel.DataAnnotations;
using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalServicoTests
{
    private static Profissional ProfissionalValido(string nome = "Maria") => new()
    {
        Nome = nome, Telefone = "11987654321", Especialidade = "Corte", Ativo = true
    };

    private static GestaoAgendaContext Banco(SaveChangesInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<GestaoAgendaContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new GestaoAgendaContext(options.Options);
    }

    private static ProfissionalServicoService Service(GestaoAgendaContext db) =>
        new(db, NullLogger<ProfissionalServicoService>.Instance);

    private static async Task<Servico> ServicoAsync(GestaoAgendaContext db, string nome = "Corte")
    {
        var servico = new Servico { Nome = nome, Valor = 50m, Duracao = 60 };
        db.Servicos.Add(servico);
        await db.SaveChangesAsync();
        return servico;
    }

    [Fact]
    public async Task CadastroSalvaProfissionalComVariasOfertasSemAlterarPadroes()
    {
        using var db = Banco();
        var corte = await ServicoAsync(db);
        var escova = await ServicoAsync(db, "Escova");
        var resultado = await Service(db).SalvarAsync(ProfissionalValido(),
            [new(corte.Id, 40.50m, 30, true), new(escova.Id, 75m, 45, false)]);
        Assert.True(resultado.Sucesso);
        db.ChangeTracker.Clear();
        var profissional = await db.Profissionais.Include(p => p.ServicosOferecidos).SingleAsync();
        Assert.Equal(resultado.ProfissionalId, profissional.Id);
        Assert.Equal(2, profissional.ServicosOferecidos.Count);
        Assert.Equal(40.50m, profissional.ServicosOferecidos.Single(p => p.ServicoId == corte.Id).Preco);
        Assert.False(profissional.ServicosOferecidos.Single(p => p.ServicoId == escova.Id).Ativo);
        Assert.All(await db.Servicos.ToListAsync(), s =>
        {
            Assert.Equal(50m, s.Valor);
            Assert.Equal(60, s.Duracao);
        });
    }

    [Fact]
    public async Task MesmoServicoPodeSerOferecidoPorVariosProfissionais()
    {
        using var db = Banco();
        var servico = await ServicoAsync(db);
        var service = Service(db);
        Assert.True((await service.SalvarAsync(ProfissionalValido("Maria"), [new(servico.Id, 40m, 30, true)])).Sucesso);
        Assert.True((await service.SalvarAsync(ProfissionalValido("Ana"), [new(servico.Id, 70m, 60, true)])).Sucesso);
        var ofertas = await service.GetProfissionaisElegiveisAsync(servico.Id);
        Assert.Equal(2, ofertas.Count);
        Assert.Equal(new[] { "Ana", "Maria" }, ofertas.Select(p => p.Nome));
        Assert.Equal(new[] { 70m, 40m }, ofertas.Select(p => p.Preco));
    }

    [Fact]
    public async Task DuplicidadeNaEntradaNaoGravaNemMesmoProfissional()
    {
        using var db = Banco();
        var servico = await ServicoAsync(db);
        var resultado = await Service(db).SalvarAsync(ProfissionalValido(),
            [new(servico.Id, 40m, 30, true), new(servico.Id, 50m, 60, false)]);
        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.Contains("mais de uma vez"));
        Assert.Empty(await db.Profissionais.ToListAsync());
        Assert.Empty(await db.ProfissionalServicos.ToListAsync());
    }

    [Fact]
    public async Task ReenvioEReativacaoReutilizamAssociacaoSemDuplicar()
    {
        using var db = Banco();
        var servico = await ServicoAsync(db);
        var service = Service(db);
        var profissional = ProfissionalValido();
        profissional.Id = (await service.SalvarAsync(profissional, [new(servico.Id, 40m, 30, true)])).ProfissionalId!.Value;
        var id = (await db.ProfissionalServicos.SingleAsync()).Id;
        Assert.True((await service.SalvarAsync(profissional, [new(servico.Id, 40m, 30, true)])).Sucesso);
        Assert.True((await service.SalvarAsync(profissional, [])).Sucesso);
        Assert.False((await db.ProfissionalServicos.SingleAsync()).Ativo);
        Assert.True((await service.SalvarAsync(profissional, [new(servico.Id, 45m, 35, true)])).Sucesso);
        var associacao = await db.ProfissionalServicos.SingleAsync();
        Assert.Equal(id, associacao.Id);
        Assert.True(associacao.Ativo);
        Assert.Equal(45m, associacao.Preco);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10.123")]
    [InlineData("10000000000000000")]
    public async Task PrecoInvalidoNaoGrava(string valor)
    {
        using var db = Banco();
        var servico = await ServicoAsync(db);
        var preco = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);
        var resultado = await Service(db).SalvarAsync(ProfissionalValido(), [new(servico.Id, preco, 30, true)]);
        Assert.False(resultado.Sucesso);
        Assert.Empty(await db.Profissionais.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public async Task DuracaoInvalidaNaoGrava(int duracao)
    {
        using var db = Banco();
        var servico = await ServicoAsync(db);
        Assert.False((await Service(db).SalvarAsync(ProfissionalValido(), [new(servico.Id, 40m, duracao, true)])).Sucesso);
        Assert.Empty(await db.Profissionais.ToListAsync());
    }

    [Theory]
    [InlineData("0.01", 1)]
    [InlineData("9999999999999999.99", 1440)]
    public void LimitesValidosDoModelo(string valor, int duracao)
    {
        var modelo = new ProfissionalServico
        {
            ServicoId = 1, Preco = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture),
            DuracaoMinutos = duracao
        };
        Assert.True(Validator.TryValidateObject(modelo, new ValidationContext(modelo), [], true));
    }

    [Fact]
    public async Task ReferenciasInexistentesECamposObrigatoriosNaoGravam()
    {
        using var db = Banco();
        var service = Service(db);
        var servico = await ServicoAsync(db);
        Assert.False((await service.SalvarAsync(ProfissionalValido(), [new(999, 40m, 30, true)])).Sucesso);
        var profissional = ProfissionalValido();
        profissional.Id = 999;
        Assert.False((await service.SalvarAsync(profissional, [new(servico.Id, 40m, 30, true)])).Sucesso);
        profissional.Id = 0;
        profissional.Nome = " ";
        Assert.False((await service.SalvarAsync(profissional, [new(servico.Id, 40m, 30, true)])).Sucesso);
        Assert.Empty(await db.ProfissionalServicos.ToListAsync());
        Assert.Empty(await db.Profissionais.ToListAsync());
    }

    [Fact]
    public async Task ConsultaElegivelFiltraServicoProfissionalEAssociacaoAtivos()
    {
        using var db = Banco();
        var corte = await ServicoAsync(db);
        var escova = await ServicoAsync(db, "Escova");
        var service = Service(db);
        var ativo = ProfissionalValido("Ativo");
        var inativo = ProfissionalValido("Inativo");
        inativo.Ativo = false;
        await service.SalvarAsync(ativo, [new(corte.Id, 40m, 30, true), new(escova.Id, 80m, 60, false)]);
        await service.SalvarAsync(inativo, [new(corte.Id, 50m, 45, true)]);
        Assert.Equal("Ativo", Assert.Single(await service.GetProfissionaisElegiveisAsync(corte.Id)).Nome);
        Assert.Empty(await service.GetProfissionaisElegiveisAsync(escova.Id));
        Assert.Empty(await service.GetProfissionaisElegiveisAsync(999));
    }

    [Fact]
    public async Task ConfiguracaoPreservaSnapshotsENulosLegadosAposAlterarOuDesativarOferta()
    {
        using var db = Banco();
        var servico = await ServicoAsync(db);
        var service = Service(db);
        var profissional = ProfissionalValido();
        profissional.Id = (await service.SalvarAsync(profissional, [new(servico.Id, 40m, 30, true)])).ProfissionalId!.Value;
        var associacao = await db.ProfissionalServicos.SingleAsync();
        var cliente = new Cliente { Nome = "Cliente", Telefone = "11987654321" };
        db.Clientes.Add(cliente);
        db.Agendamentos.AddRange(
            new Agendamento { Cliente = cliente, ServicoId = servico.Id, ProfissionalServicoId = associacao.Id,
                Data = new DateTime(2026, 10, 29), Hora = TimeSpan.FromHours(9), PrecoAplicado = 40m, DuracaoMinutosAplicada = 30 },
            new Agendamento { Cliente = cliente, ServicoId = servico.Id, Data = new DateTime(2026, 10, 28), Hora = TimeSpan.FromHours(9) });
        await db.SaveChangesAsync();
        Assert.True((await service.SalvarAsync(profissional, [new(servico.Id, 90m, 90, false)])).Sucesso);
        db.ChangeTracker.Clear();
        var reserva = await db.Agendamentos.SingleAsync(a => a.ProfissionalServicoId != null);
        Assert.Equal(40m, reserva.PrecoAplicado);
        Assert.Equal(30, reserva.DuracaoMinutosAplicada);
        var legado = await db.Agendamentos.SingleAsync(a => a.ProfissionalServicoId == null);
        Assert.Null(legado.PrecoAplicado);
        Assert.Null(legado.DuracaoMinutosAplicada);
        Assert.False((await db.ProfissionalServicos.SingleAsync()).Ativo);
        Assert.Equal(2, await db.Agendamentos.CountAsync());
    }

    [Fact]
    public void MetadadosSqlServerImpedemDuplicidadeECascataEPreservamIndiceGlobal()
    {
        using var db = new GestaoAgendaContext(new DbContextOptionsBuilder<GestaoAgendaContext>()
            .UseSqlServer("Server=localhost;Database=SomenteMetadados;Integrated Security=True").Options);
        var modelo = db.GetService<IDesignTimeModel>().Model;
        var associacao = modelo.FindEntityType(typeof(ProfissionalServico))!;
        Assert.Contains(associacao.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ProfissionalId", "ServicoId" }));
        Assert.All(associacao.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Equal(18, associacao.FindProperty("Preco")!.GetPrecision());
        Assert.Equal(2, associacao.FindProperty("Preco")!.GetScale());
        Assert.Equal(2, associacao.GetCheckConstraints().Count());
        var reserva = modelo.FindEntityType(typeof(Agendamento))!;
        Assert.All(new[] { "ProfissionalServicoId", "PrecoAplicado", "DuracaoMinutosAplicada" },
            campo => Assert.True(reserva.FindProperty(campo)!.IsNullable));
        Assert.Equal(DeleteBehavior.Restrict, reserva.GetForeignKeys().Single(fk => fk.PrincipalEntityType == associacao).DeleteBehavior);
        Assert.Contains(reserva.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Data", "Hora" }));
    }

    [Fact]
    public async Task FalhaDeGravacaoRetornaErroSemDeixarNovoCadastroPendente()
    {
        var interceptor = new FalhaSaveChanges();
        using var db = Banco(interceptor);
        var servico = await ServicoAsync(db);
        interceptor.Falhar = true;
        var resultado = await Service(db).SalvarAsync(ProfissionalValido(), [new(servico.Id, 40m, 30, true)]);
        Assert.False(resultado.Sucesso);
        Assert.NotEmpty(resultado.Erros);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(await db.Profissionais.ToListAsync());
        Assert.Empty(await db.ProfissionalServicos.ToListAsync());
    }

    [Fact]
    public async Task FalhaAoEditarRestauraCamposEPermiteTentarNovamente()
    {
        var interceptor = new FalhaSaveChanges();
        using var db = Banco(interceptor);
        var corte = await ServicoAsync(db);
        var escova = await ServicoAsync(db, "Escova");
        var service = Service(db);
        var profissional = ProfissionalValido();
        profissional.Id = (await service.SalvarAsync(profissional, [new(corte.Id, 40m, 30, true)])).ProfissionalId!.Value;
        var idOriginal = (await db.ProfissionalServicos.SingleAsync()).Id;

        interceptor.Falhar = true;
        profissional.Nome = "Nome alterado";
        var resultado = await service.SalvarAsync(profissional,
            [new(corte.Id, 90m, 90, false), new(escova.Id, 80m, 40, true)]);
        Assert.False(resultado.Sucesso);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal("Maria", (await db.Profissionais.SingleAsync()).Nome);
        var original = await db.ProfissionalServicos.SingleAsync();
        Assert.Equal(idOriginal, original.Id);
        Assert.Equal(40m, original.Preco);
        Assert.Equal(30, original.DuracaoMinutos);
        Assert.True(original.Ativo);

        interceptor.Falhar = false;
        resultado = await service.SalvarAsync(profissional,
            [new(corte.Id, 90m, 90, false), new(escova.Id, 80m, 40, true)]);
        Assert.True(resultado.Sucesso);
        Assert.Equal(2, await db.ProfissionalServicos.CountAsync());
        Assert.Equal("Nome alterado", (await db.Profissionais.SingleAsync()).Nome);
    }

    private sealed class FalhaSaveChanges : SaveChangesInterceptor
    {
        public bool Falhar { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Falhar) throw new DbUpdateException("Falha simulada antes da gravação");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}