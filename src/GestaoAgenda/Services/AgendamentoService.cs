using System.Data;
using GestaoAgenda.Data;
using GestaoAgenda.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GestaoAgenda.Services;

public sealed record ErroAgendamento(string Campo, string Mensagem);
public sealed record ResultadoAgendamento(int? Id, IReadOnlyList<ErroAgendamento> Erros,
    bool NaoEncontrado = false, bool Indisponivel = false)
{
    public bool Sucesso => Id.HasValue && Erros.Count == 0;
}

public class AgendamentoService(GestaoAgendaContext db, ILogger<AgendamentoService> logger)
{
    // O salão tem agenda global. Entre instâncias, a transação SQL serializável
    // protege a leitura de intervalos e a gravação contra alterações/fantasmas.
    private static readonly SemaphoreSlim Escrita = new(1, 1);
    private static readonly HashSet<string> StatusPermitidos = ["Agendado", "Concluído", "Cancelado"];

    public async Task<ResultadoAgendamento> SalvarAsync(Agendamento dados)
    {
        if (dados.Id < 0) return Falha("", "Identificador inválido.");
        if (dados.ClienteId <= 0) return Falha("ClienteId", "Selecione um cliente válido.");
        if (dados.ServicoId <= 0) return Falha("ServicoId", "Selecione um serviço válido.");
        if (!StatusPermitidos.Contains(dados.Status)) return Falha("Status", "Selecione um status válido.");
        if (dados.Observacao?.Length > 500) return Falha("Observacao", "A observação deve ter no máximo 500 caracteres.");
        if (dados.Hora < TimeSpan.Zero || dados.Hora >= TimeSpan.FromDays(1))
            return Falha("Hora", "Informe um horário válido.");

        await Escrita.WaitAsync();
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
                var original = dados.Id == 0 ? null :
                    await db.Agendamentos.AsNoTracking().SingleOrDefaultAsync(a => a.Id == dados.Id);
                if (dados.Id != 0 && original is null)
                    return new ResultadoAgendamento(null, [], NaoEncontrado: true);

                var mudouReserva = original is null || original.ClienteId != dados.ClienteId ||
                    original.ServicoId != dados.ServicoId || original.ProfissionalServicoId != dados.ProfissionalServicoId ||
                    original.Data.Date != dados.Data.Date || original.Hora != dados.Hora;
                var reativando = original?.Status == "Cancelado" && dados.Status != "Cancelado";
                var exigeElegibilidade = mudouReserva || reativando;
                var mesmaOferta = original is not null && original.ServicoId == dados.ServicoId &&
                    original.ProfissionalServicoId == dados.ProfissionalServicoId;
                var preco = original?.PrecoAplicado;
                var duracao = original?.DuracaoMinutosAplicada;

                if (exigeElegibilidade)
                {
                    if (!await db.Clientes.AnyAsync(c => c.Id == dados.ClienteId))
                        return Falha("ClienteId", "O cliente selecionado não existe.");
                    if (dados.ProfissionalServicoId is null or <= 0)
                        return Falha("ProfissionalServicoId", "Selecione um profissional habilitado para este serviço.");
                    var oferta = await db.ProfissionalServicos.AsNoTracking()
                        .Where(ps => ps.Id == dados.ProfissionalServicoId && ps.ServicoId == dados.ServicoId &&
                            ps.Ativo && ps.Profissional.Ativo)
                        .Select(ps => new { ps.Preco, ps.DuracaoMinutos })
                        .SingleOrDefaultAsync();
                    if (oferta is null)
                        return Falha("ProfissionalServicoId", "O profissional ou a oferta não está ativo para este serviço.");
                    if (!mesmaOferta)
                    {
                        preco = oferta.Preco;
                        duracao = oferta.DuracaoMinutos;
                    }
                    // Mesmo em reagendamento, não substituir snapshots históricos pelo preço atual.
                    if (preco is null or <= 0 || duracao is null or < 1 or > 1440)
                        return Falha("ProfissionalServicoId", "A reserva não possui preço/duração históricos completos. Revise a oferta antes de reagendar.");
                    if (oferta.Preco <= 0 || oferta.DuracaoMinutos is < 1 or > 1440)
                        return Falha("ProfissionalServicoId", "A oferta possui preço ou duração inválidos. Revise o cadastro.");
                }

                if (dados.Status != "Cancelado" && exigeElegibilidade)
                {
                    var fim = IntervaloAgendamento.Termino(dados.Data, dados.Hora, duracao);
                    if (fim is null) return Falha("Hora", "O horário de término está fora do intervalo válido.");
                    var erro = await VerificarConflitoAsync(dados, fim.Value);
                    if (erro is not null) return Falha("Hora", erro);
                }
                else if (exigeElegibilidade && IntervaloAgendamento.Termino(dados.Data, dados.Hora, duracao) is null)
                    return Falha("Hora", "O horário de término está fora do intervalo válido.");

                var reserva = new Agendamento
                {
                    Id = dados.Id, ClienteId = dados.ClienteId, ServicoId = dados.ServicoId,
                    ProfissionalServicoId = dados.ProfissionalServicoId,
                    Data = dados.Data.Date, Hora = dados.Hora, Status = dados.Status, Observacao = dados.Observacao,
                    PrecoAplicado = preco, DuracaoMinutosAplicada = duracao
                };
                if (reserva.Id == 0) db.Agendamentos.Add(reserva);
                else db.Agendamentos.Update(reserva);
                try
                {
                    await db.SaveChangesAsync();
                    if (transaction is not null) await transaction.CommitAsync();
                    return new ResultadoAgendamento(reserva.Id, []);
                }
                finally { db.Entry(reserva).State = EntityState.Detached; }
            });
        }
        catch (Exception ex) when (ex is DbUpdateException or System.Data.Common.DbException
            or TimeoutException or RetryLimitExceededException)
        {
            logger.LogError(ex, "Erro ao salvar agendamento {Id}", dados.Id);
            return new(null, [new("", "Não foi possível salvar o agendamento. A agenda pode ter sido alterada por outra operação. Recarregue e tente novamente.")],
                Indisponivel: true);
        }
        finally { Escrita.Release(); }
    }

    private async Task<string?> VerificarConflitoAsync(Agendamento dados, DateTime fim)
    {
        var inicio = dados.Data.Date.Add(dados.Hora);
        var primeiroDia = inicio.Date == DateTime.MinValue ? inicio.Date : inicio.Date.AddDays(-1);
        var ultimoDia = fim.AddTicks(-1).Date;
        // Duração máxima de 1440 min: o dia anterior basta para intervalos conhecidos.
        var candidatos = await db.Agendamentos.AsNoTracking()
            .Where(a => a.Id != dados.Id && a.Status != "Cancelado" &&
                a.Data >= primeiroDia && a.Data <= ultimoDia)
            .Select(a => new { a.Data, a.Hora, a.DuracaoMinutosAplicada }).ToListAsync();
        foreach (var candidato in candidatos)
        {
            var fimExistente = IntervaloAgendamento.Termino(candidato.Data, candidato.Hora, candidato.DuracaoMinutosAplicada);
            if (fimExistente is null)
            {
                if (candidato.Data.Date >= inicio.Date && candidato.Data.Date <= ultimoDia)
                    return "Há agendamentos sem duração histórica neste dia. Revise-os antes de incluir ou reagendar atendimentos.";
            }
            else if (inicio < fimExistente && candidato.Data.Date.Add(candidato.Hora) < fim)
                return "Este intervalo já está ocupado na agenda do salão.";
        }
        return null;
    }

    private static ResultadoAgendamento Falha(string campo, string mensagem) =>
        new(null, [new(campo, mensagem)]);
}