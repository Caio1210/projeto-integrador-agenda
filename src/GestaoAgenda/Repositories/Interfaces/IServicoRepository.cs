using GestaoAgenda.Models;
namespace GestaoAgenda.Repositories.Interfaces;
public interface IServicoRepository
{
    Task<List<Servico>> GetServicosAsync();
    Task<Servico?> GetServicoAsync(int? id);
    Task CreateServicoAsync(Servico servico);
    Task UpdateServicoAsync(Servico servico);
    Task DeleteServicoAsync(int id);
}
