using System.ComponentModel.DataAnnotations;
using GestaoAgenda.Data;
using GestaoAgenda.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoAgenda.Services;

// Entrada contém apenas os dados configuráveis de cada oferta, sem grafos de entidades.
public sealed record ServicoOferecido(int ServicoId, decimal Preco, int DuracaoMinutos, bool Ativo);
public sealed record OfertaProfissional(int ProfissionalServicoId, int ProfissionalId, string Nome,
    decimal Preco, int DuracaoMinutos);
public sealed record ResultadoConfiguracaoProfissional(int? ProfissionalId, IReadOnlyList<string> Erros)
{
    public bool Sucesso => Erros.Count == 0;
}

public class ProfissionalServicoService(
    GestaoAgendaContext db,
    ILogger<ProfissionalServicoService> logger)
{
    public Task<List<ProfissionalServico>> GetAssociacoesAsync(int profissionalId) =>
        db.ProfissionalServicos.AsNoTracking()
            .Where(ps => ps.ProfissionalId == profissionalId)
            .Include(ps => ps.Servico)
            .OrderBy(ps => ps.Servico.Nome).ThenBy(ps => ps.Id)
            .ToListAsync();

    public Task<List<OfertaProfissional>> GetProfissionaisElegiveisAsync(int servicoId) =>
        db.ProfissionalServicos.AsNoTracking()
            .Where(ps => ps.ServicoId == servicoId && ps.Ativo && ps.Profissional.Ativo)
            .OrderBy(ps => ps.Profissional.Nome).ThenBy(ps => ps.ProfissionalId)
            .Select(ps => new OfertaProfissional(ps.Id, ps.ProfissionalId, ps.Profissional.Nome,
                ps.Preco, ps.DuracaoMinutos))
            .ToListAsync();

    // A lista representa a configuração completa. Omitir uma oferta existente a inativa.
    // Chamadas repetidas atualizam a mesma linha, inclusive ao reativar uma oferta.
    public async Task<ResultadoConfiguracaoProfissional> SalvarAsync(
        Profissional profissional, IReadOnlyCollection<ServicoOferecido> ofertas)
    {
        var erros = new List<string>();
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(profissional, new ValidationContext(profissional), resultados, true);
        erros.AddRange(resultados.Select(r => r.ErrorMessage ?? "Dados do profissional inválidos"));
        if (profissional.Id < 0) erros.Add("Identificador do profissional inválido.");
        if (ofertas.Select(o => o.ServicoId).Distinct().Count() != ofertas.Count)
            erros.Add("Um serviço não pode ser associado mais de uma vez ao profissional.");

        foreach (var oferta in ofertas)
        {
            var associacao = new ProfissionalServico
            {
                ServicoId = oferta.ServicoId, Preco = oferta.Preco,
                DuracaoMinutos = oferta.DuracaoMinutos
            };
            resultados.Clear();
            Validator.TryValidateObject(associacao, new ValidationContext(associacao), resultados, true);
            erros.AddRange(resultados.Select(r => $"Serviço {oferta.ServicoId}: {r.ErrorMessage}"));
        }
        if (erros.Count > 0) return new(null, erros);

        Profissional? atual = null;
        try
        {
            var ids = ofertas.Select(o => o.ServicoId).ToArray();
            if (await db.Servicos.CountAsync(s => ids.Contains(s.Id)) != ids.Length)
                return new(null, ["Um ou mais serviços selecionados não existem."]);

            if (profissional.Id != 0)
            {
                atual = await db.Profissionais
                    .Include(p => p.ServicosOferecidos)
                    .SingleOrDefaultAsync(p => p.Id == profissional.Id);
                if (atual is null) return new(null, ["Profissional não encontrado."]);
            }
            else
            {
                atual = new Profissional();
                db.Profissionais.Add(atual);
            }

            // Copiar apenas campos permitidos; ignorar navegações fornecidas pelo chamador.
            atual.Nome = profissional.Nome;
            atual.Telefone = profissional.Telefone;
            atual.Email = profissional.Email;
            atual.Especialidade = profissional.Especialidade;
            atual.Ativo = profissional.Ativo;

            var existentes = atual.ServicosOferecidos.ToDictionary(ps => ps.ServicoId);
            foreach (var existente in existentes.Values.Where(ps => !ids.Contains(ps.ServicoId)))
                existente.Ativo = false;

            foreach (var oferta in ofertas)
            {
                if (!existentes.TryGetValue(oferta.ServicoId, out var associacao))
                {
                    associacao = new ProfissionalServico { ServicoId = oferta.ServicoId };
                    atual.ServicosOferecidos.Add(associacao);
                }
                associacao.Preco = oferta.Preco;
                associacao.DuracaoMinutos = oferta.DuracaoMinutos;
                associacao.Ativo = oferta.Ativo;
            }

            // Um SaveChanges: no SQL Server, profissional e ofertas são gravados atomicamente.
            // Nenhum agendamento ou valor global de Servico é alterado.
            await db.SaveChangesAsync();
            return new(atual.Id, []);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Erro ao salvar serviços do profissional {Id}", profissional.Id);
            RestaurarAlteracoes(atual);
            return new(null, ["Não foi possível salvar. A configuração pode ter sido alterada por outra operação. Recarregue e tente novamente."]);
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or TimeoutException)
        {
            logger.LogError(ex, "Erro ao consultar ou salvar serviços do profissional {Id}", profissional.Id);
            RestaurarAlteracoes(atual);
            return new(null, ["Não foi possível acessar o banco. Tente novamente em instantes."]);
        }
    }

    private void RestaurarAlteracoes(Profissional? profissional)
    {
        if (profissional is null) return;
        var entradas = db.ChangeTracker.Entries()
            .Where(e => ReferenceEquals(e.Entity, profissional) ||
                e.Entity is ProfissionalServico ps && ReferenceEquals(ps.Profissional, profissional))
            .OrderBy(e => e.Entity is Profissional ? 1 : 0)
            .ToList();
        foreach (var entrada in entradas)
        {
            if (entrada.State == EntityState.Added)
            {
                entrada.State = EntityState.Detached;
                if (entrada.Entity is ProfissionalServico nova)
                    profissional.ServicosOferecidos.Remove(nova);
            }
            else
            {
                entrada.CurrentValues.SetValues(entrada.OriginalValues);
                entrada.State = EntityState.Unchanged;
            }
        }
    }
}