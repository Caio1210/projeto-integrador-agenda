using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories;
using GestaoAgenda.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestaoAgenda.Tests;

public class AgendamentoServiceTests
{
    [Fact]
    public async Task ReservaUsaOfertaEIgnoraValoresEnviadosPeloChamador()
    {
        var f = new Fixture();
        var entrada = f.Nova(9);
        entrada.PrecoAplicado = 1; entrada.DuracaoMinutosAplicada = 1;
        var resultado = await f.Salvar(entrada);
        Assert.True(resultado.Sucesso);
        using var db = f.Banco();
        var reserva = await db.Agendamentos.SingleAsync();
        Assert.Equal(40m, reserva.PrecoAplicado);
        Assert.Equal(60, reserva.DuracaoMinutosAplicada);
        Assert.Equal(Fixture.Dia.AddHours(10), reserva.Termino);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(0, 1)]
    [InlineData(999, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    [InlineData(1, 2)]
    public async Task RejeitaProfissionalOfertaInativosOuServicoIncompativel(int? oferta, int servico)
    {
        var f = new Fixture();
        var entrada = f.Nova(9); entrada.ProfissionalServicoId = oferta; entrada.ServicoId = servico;
        Assert.False((await f.Salvar(entrada)).Sucesso);
        using var db = f.Banco();
        Assert.Empty(await db.Agendamentos.ToListAsync());
    }

    [Theory]
    [InlineData(8, 0, true)]
    [InlineData(8, 30, false)]
    [InlineData(9, 0, false)]
    [InlineData(9, 30, false)]
    [InlineData(10, 0, true)]
    public async Task DetectaSobreposicoesEAceitaIntervalosAdjacentes(int hora, int minuto, bool esperado)
    {
        var f = new Fixture();
        Assert.True((await f.Salvar(f.Nova(9))).Sucesso);
        var nova = f.Nova(hora); nova.Hora = new TimeSpan(hora, minuto, 0);
        Assert.Equal(esperado, (await f.Salvar(nova)).Sucesso);
    }

    [Fact]
    public async Task ConflitoContinuaGlobalMesmoComOutroProfissional()
    {
        var f = new Fixture();
        await f.Salvar(f.Nova(9));
        var nova = f.Nova(9); nova.Hora = new TimeSpan(9, 30, 0); nova.ProfissionalServicoId = 2;
        Assert.False((await f.Salvar(nova)).Sucesso);
    }

    [Fact]
    public async Task DetectaIntervaloDoDiaAnteriorEAceitaFimExatoAposMeiaNoite()
    {
        var f = new Fixture();
        var primeira = f.Nova(23); primeira.Hora = new TimeSpan(23, 30, 0);
        Assert.True((await f.Salvar(primeira)).Sucesso);
        var segunda = f.Nova(0); segunda.Data = Fixture.Dia.AddDays(1); segunda.Hora = TimeSpan.FromMinutes(15);
        Assert.False((await f.Salvar(segunda)).Sucesso);
        segunda.Hora = TimeSpan.FromMinutes(30);
        Assert.True((await f.Salvar(segunda)).Sucesso);
    }

    [Fact]
    public async Task CanceladoNaoBloqueiaHorarioMasExigeOfertaElegivelNaCriacao()
    {
        var f = new Fixture();
        var cancelada = f.Nova(9); cancelada.Status = "Cancelado";
        Assert.True((await f.Salvar(cancelada)).Sucesso);
        Assert.True((await f.Salvar(f.Nova(9))).Sucesso);
        var invalida = f.Nova(9); invalida.Status = "Cancelado"; invalida.ProfissionalServicoId = 4;
        Assert.False((await f.Salvar(invalida)).Sucesso);
    }

    [Fact]
    public async Task LegadoSemDuracaoBloqueiaDiaESomenteCancelamentoLiberaDia()
    {
        var f = new Fixture();
        int id;
        using (var db = f.Banco())
        {
            var legado = new Agendamento { ClienteId = 1, ServicoId = 1, Data = Fixture.Dia, Hora = TimeSpan.FromHours(9) };
            db.Agendamentos.Add(legado); await db.SaveChangesAsync(); id = legado.Id;
        }
        var erro = await f.Salvar(f.Nova(15));
        Assert.Contains(erro.Erros, e => e.Mensagem.Contains("sem duração histórica"));
        var atravessa = f.Nova(23); atravessa.Data = Fixture.Dia.AddDays(-1); atravessa.Hora = new TimeSpan(23, 30, 0);
        Assert.False((await f.Salvar(atravessa)).Sucesso);
        var depois = f.Nova(9); depois.Data = Fixture.Dia.AddDays(1);
        Assert.True((await f.Salvar(depois)).Sucesso);
        var cancelar = f.Nova(9); cancelar.Id = id; cancelar.ProfissionalServicoId = null; cancelar.Status = "Cancelado";
        Assert.True((await f.Salvar(cancelar)).Sucesso);
        Assert.True((await f.Salvar(f.Nova(15))).Sucesso);
        using var verificacao = f.Banco();
        var antigo = await verificacao.Agendamentos.SingleAsync(a => a.Id == id);
        Assert.Null(antigo.PrecoAplicado); Assert.Null(antigo.DuracaoMinutosAplicada);
    }

    [Fact]
    public async Task LegadoPodeEditarObservacaoSemInventarHistoricoMasNaoReagendarSemOferta()
    {
        var f = new Fixture();
        int id;
        using (var db = f.Banco())
        {
            var a = new Agendamento { ClienteId = 1, ServicoId = 1, Data = Fixture.Dia, Hora = TimeSpan.FromHours(9) };
            db.Agendamentos.Add(a); await db.SaveChangesAsync(); id = a.Id;
        }
        var editar = f.Nova(9); editar.Id = id; editar.ProfissionalServicoId = null; editar.Observacao = "Revisão";
        Assert.True((await f.Salvar(editar)).Sucesso);
        editar.Hora = TimeSpan.FromHours(10);
        Assert.False((await f.Salvar(editar)).Sucesso);
        using var dbFinal = f.Banco();
        var antigo = await dbFinal.Agendamentos.SingleAsync();
        Assert.Equal("Revisão", antigo.Observacao); Assert.Null(antigo.PrecoAplicado);
        Assert.Null(antigo.DuracaoMinutosAplicada);
    }

    [Fact]
    public async Task MetadadosPreservamOfertaInativaEMudancasDeReservaSaoRejeitadas()
    {
        var f = new Fixture();
        var id = (await f.Salvar(f.Nova(9))).Id!.Value;
        using (var db = f.Banco())
        {
            var oferta = await db.ProfissionalServicos.SingleAsync(o => o.Id == 1);
            oferta.Ativo = false; oferta.Preco = 999m; oferta.DuracaoMinutos = 120;
            (await db.Profissionais.SingleAsync(p => p.Id == 1)).Ativo = false;
            await db.SaveChangesAsync();
        }
        var editar = f.Nova(9); editar.Id = id; editar.Observacao = "Atendido"; editar.Status = "Concluído";
        Assert.True((await f.Salvar(editar)).Sucesso);
        editar.Hora = TimeSpan.FromHours(11);
        Assert.False((await f.Salvar(editar)).Sucesso);
        using var final = f.Banco();
        var antigo = await final.Agendamentos.SingleAsync();
        Assert.Equal(40m, antigo.PrecoAplicado); Assert.Equal(60, antigo.DuracaoMinutosAplicada);
        Assert.Equal("Atendido", antigo.Observacao);
    }

    [Fact]
    public async Task MesmaOfertaPreservaSnapshotsETrocaExplicitaAdotaNovosValores()
    {
        var f = new Fixture();
        var id = (await f.Salvar(f.Nova(9))).Id!.Value;
        using (var db = f.Banco())
        {
            var oferta = await db.ProfissionalServicos.SingleAsync(o => o.Id == 1);
            oferta.Preco = 150m; oferta.DuracaoMinutos = 120;
            (await db.Servicos.SingleAsync(s => s.Id == 1)).Valor = 500m;
            await db.SaveChangesAsync();
        }
        var editar = f.Nova(11); editar.Id = id;
        Assert.True((await f.Salvar(editar)).Sucesso);
        using (var db = f.Banco())
        {
            var a = await db.Agendamentos.SingleAsync();
            Assert.Equal(40m, a.PrecoAplicado); Assert.Equal(60, a.DuracaoMinutosAplicada);
        }
        editar.ProfissionalServicoId = 2;
        Assert.True((await f.Salvar(editar)).Sucesso);
        using var final = f.Banco();
        var atual = await final.Agendamentos.SingleAsync();
        Assert.Equal(80m, atual.PrecoAplicado); Assert.Equal(30, atual.DuracaoMinutosAplicada);
        var nova = f.Nova(14);
        Assert.True((await f.Salvar(nova)).Sucesso);
        using var conferencia = f.Banco();
        Assert.Equal(150m, (await conferencia.Agendamentos.SingleAsync(a => a.Id != id)).PrecoAplicado);
    }

    [Fact]
    public async Task ReativacaoRefazConflitoENaoUsaOfertaInativa()
    {
        var f = new Fixture();
        var cancelada = f.Nova(9); cancelada.Status = "Cancelado";
        cancelada.Id = (await f.Salvar(cancelada)).Id!.Value;
        await f.Salvar(f.Nova(9));
        cancelada.Status = "Agendado";
        Assert.False((await f.Salvar(cancelada)).Sucesso);
        cancelada.Hora = TimeSpan.FromHours(11);
        using (var db = f.Banco())
        {
            (await db.ProfissionalServicos.SingleAsync(o => o.Id == 1)).Ativo = false;
            await db.SaveChangesAsync();
        }
        Assert.False((await f.Salvar(cancelada)).Sucesso);
    }

    [Fact]
    public async Task AtualizacaoExcluiProprioIdMasDetectaOutrasReservas()
    {
        var f = new Fixture();
        var id = (await f.Salvar(f.Nova(9))).Id!.Value;
        await f.Salvar(f.Nova(11));
        var editar = f.Nova(9); editar.Id = id;
        Assert.True((await f.Salvar(editar)).Sucesso);
        editar.Hora = new TimeSpan(10, 30, 0);
        Assert.False((await f.Salvar(editar)).Sucesso);
    }

    [Fact]
    public async Task DuasSolicitacoesConcorrentesNaoReservamMesmoIntervaloNaInstancia()
    {
        var f = new Fixture();
        var resultados = await Task.WhenAll(Task.Run(() => f.Salvar(f.Nova(9))),
            Task.Run(() => f.Salvar(f.Nova(9))));
        Assert.Single(resultados, r => r.Sucesso);
        using var db = f.Banco();
        Assert.Equal(1, await db.Agendamentos.CountAsync());
    }

    [Fact]
    public async Task RejeitaStatusClienteETerminoInvalidos()
    {
        var f = new Fixture();
        var a = f.Nova(9); a.Status = "Inventado";
        Assert.False((await f.Salvar(a)).Sucesso);
        a = f.Nova(9); a.ClienteId = 999;
        Assert.False((await f.Salvar(a)).Sucesso);
        a = f.Nova(9); a.Data = DateTime.MaxValue.Date; a.Hora = new TimeSpan(23, 59, 0);
        Assert.False((await f.Salvar(a)).Sucesso);
        a = f.Nova(9); a.Hora = TimeSpan.FromHours(24);
        Assert.False((await f.Salvar(a)).Sucesso);
    }

    [Fact]
    public async Task ErroDeGravacaoNaoDeixaReservaPendenteENovaTentativaFunciona()
    {
        var f = new Fixture();
        using var db = new GestaoAgendaContext(new DbContextOptionsBuilder<GestaoAgendaContext>(f.Options)
            .AddInterceptors(new Falha()).Options);
        var resultado = await new AgendamentoService(db, NullLogger<AgendamentoService>.Instance).SalvarAsync(f.Nova(9));
        Assert.True(resultado.Indisponivel); Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(await db.Agendamentos.ToListAsync());
        Assert.True((await f.Salvar(f.Nova(9))).Sucesso);
    }

    [Fact]
    public async Task RepositorioCarregaProfissionalESemanaCorretaSemRastreamento()
    {
        var f = new Fixture();
        await f.Salvar(f.Nova(9));
        var outra = f.Nova(9); outra.Data = Fixture.Dia.AddDays(7);
        await f.Salvar(outra);
        using var db = f.Banco();
        var repo = new AgendamentoRepository(db);
        var semana = await repo.GetAgendamentosDaSemanaAsync(Fixture.Dia, Fixture.Dia.AddDays(7));
        Assert.Equal("Ana", Assert.Single(semana).ProfissionalServico!.Profissional.Nome);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private sealed class Falha : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Falha simulada");
    }

    internal sealed class Fixture
    {
        public static readonly DateTime Dia = new(2030, 1, 7);
        public DbContextOptions<GestaoAgendaContext> Options { get; } =
            new DbContextOptionsBuilder<GestaoAgendaContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public Fixture()
        {
            using var db = Banco();
            db.Clientes.Add(new Cliente { Id = 1, Nome = "Cliente", Telefone = "11987654321" });
            db.Servicos.AddRange(new Servico { Id = 1, Nome = "Corte", Duracao = 90, Valor = 999 },
                new Servico { Id = 2, Nome = "Barba", Duracao = 20, Valor = 25 });
            db.Profissionais.AddRange(new Profissional { Id = 1, Nome = "Ana", Telefone = "11987654321", Especialidade = "Corte" },
                new Profissional { Id = 2, Nome = "Bia", Telefone = "11987654321", Especialidade = "Corte" },
                new Profissional { Id = 3, Nome = "Carla", Telefone = "11987654321", Especialidade = "Corte", Ativo = false });
            db.ProfissionalServicos.AddRange(
                new ProfissionalServico { Id = 1, ProfissionalId = 1, ServicoId = 1, Preco = 40, DuracaoMinutos = 60 },
                new ProfissionalServico { Id = 2, ProfissionalId = 2, ServicoId = 1, Preco = 80, DuracaoMinutos = 30 },
                new ProfissionalServico { Id = 3, ProfissionalId = 1, ServicoId = 2, Preco = 25, DuracaoMinutos = 20, Ativo = false },
                new ProfissionalServico { Id = 4, ProfissionalId = 3, ServicoId = 1, Preco = 20, DuracaoMinutos = 30 });
            db.SaveChanges();
        }
        public GestaoAgendaContext Banco() => new(Options);
        public Agendamento Nova(int hora) => new()
        {
            ClienteId = 1, ServicoId = 1, ProfissionalServicoId = 1,
            Data = Dia, Hora = TimeSpan.FromHours(hora), Status = "Agendado"
        };
        public async Task<ResultadoAgendamento> Salvar(Agendamento entrada)
        {
            using var db = Banco();
            return await new AgendamentoService(db, NullLogger<AgendamentoService>.Instance).SalvarAsync(entrada);
        }
    }
}