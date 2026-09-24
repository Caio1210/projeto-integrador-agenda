using GestaoAgenda.Models;
namespace GestaoAgenda.Repositories.Interfaces;
public interface IAgendamentoRepository
{
    Task<List<Agendamento>> GetAgendamentosAsync();
    Task<Agendamento?> GetAgendamentoAsync(int? id);
    Task<bool> HorarioDisponivelAsync(DateTime data, TimeSpan hora, int? id = null);
    Task CreateAgendamentoAsync(Agendamento agendamento);
    Task UpdateAgendamentoAsync(Agendamento agendamento);
    Task DeleteAgendamentoAsync(int id);
}
