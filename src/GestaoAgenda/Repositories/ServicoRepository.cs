using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace GestaoAgenda.Repositories;
public class ServicoRepository(GestaoAgendaContext context) : IServicoRepository
{
    private readonly GestaoAgendaContext _db = context;
    public Task<List<Servico>> GetServicosAsync() => _db.Servicos.OrderBy(s => s.Nome).ToListAsync();
    public Task<Servico?> GetServicoAsync(int? id) => _db.Servicos.FirstOrDefaultAsync(s => s.Id == id);
    public async Task CreateServicoAsync(Servico servico) { _db.Servicos.Add(servico); await _db.SaveChangesAsync(); }
    public async Task UpdateServicoAsync(Servico servico) { _db.Servicos.Update(servico); await _db.SaveChangesAsync(); }
    public async Task DeleteServicoAsync(int id) { var item = await _db.Servicos.FindAsync(id); if (item != null) _db.Servicos.Remove(item); await _db.SaveChangesAsync(); }
}
