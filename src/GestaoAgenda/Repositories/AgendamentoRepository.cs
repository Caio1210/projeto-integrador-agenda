using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace GestaoAgenda.Repositories;
public class AgendamentoRepository(GestaoAgendaContext context) : IAgendamentoRepository
{
    private readonly GestaoAgendaContext _db = context;
    public Task<List<Agendamento>> GetAgendamentosAsync() => _db.Agendamentos.Include(a => a.Cliente).Include(a => a.Servico).OrderBy(a => a.Data).ThenBy(a => a.Hora).ToListAsync();
    public Task<Agendamento?> GetAgendamentoAsync(int? id) => _db.Agendamentos.Include(a => a.Cliente).Include(a => a.Servico).FirstOrDefaultAsync(a => a.Id == id);
    public Task<bool> HorarioDisponivelAsync(DateTime data, TimeSpan hora, int? id = null) => !_db.Agendamentos.AnyAsync(a => a.Data.Date == data.Date && a.Hora == hora && a.Status != "Cancelado" && (!id.HasValue || a.Id != id.Value));
    public async Task CreateAgendamentoAsync(Agendamento agendamento) { _db.Agendamentos.Add(agendamento); await _db.SaveChangesAsync(); }
    public async Task UpdateAgendamentoAsync(Agendamento agendamento) { _db.Agendamentos.Update(agendamento); await _db.SaveChangesAsync(); }
    public async Task DeleteAgendamentoAsync(int id) { var item = await _db.Agendamentos.FindAsync(id); if (item != null) _db.Agendamentos.Remove(item); await _db.SaveChangesAsync(); }
}
