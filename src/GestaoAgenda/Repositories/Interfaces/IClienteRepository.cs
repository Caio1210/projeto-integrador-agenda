using GestaoAgenda.Models;
namespace GestaoAgenda.Repositories.Interfaces;
public interface IClienteRepository
{
    Task<List<Cliente>> GetClientesAsync();
    Task<Cliente?> GetClienteAsync(int? id);
    Task CreateClienteAsync(Cliente cliente);
    Task UpdateClienteAsync(Cliente cliente);
    Task DeleteClienteAsync(int id);
}
