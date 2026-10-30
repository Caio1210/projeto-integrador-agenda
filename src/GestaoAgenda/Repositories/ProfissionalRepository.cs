using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GestaoAgenda.Repositories;

public class ProfissionalRepository(GestaoAgendaContext context) : IProfissionalRepository
{
    public Task<List<Profissional>> GetProfissionaisAsync() =>
        context.Profissionais.AsNoTracking().OrderBy(p => p.Nome).ThenBy(p => p.Id).ToListAsync();

    public Task<Profissional?> GetProfissionalAsync(int id) =>
        context.Profissionais.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public async Task CreateProfissionalAsync(Profissional profissional)
    {
        context.Profissionais.Add(profissional);
        await context.SaveChangesAsync();
    }

    public async Task<bool> UpdateProfissionalAsync(Profissional profissional)
    {
        var atual = await context.Profissionais.FindAsync(profissional.Id);
        if (atual is null) return false;

        atual.Nome = profissional.Nome;
        atual.Telefone = profissional.Telefone;
        atual.Email = profissional.Email;
        atual.Especialidade = profissional.Especialidade;
        atual.Ativo = profissional.Ativo;
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // O registro pode ter sido removido entre a consulta e a gravação.
            return false;
        }
    }
}