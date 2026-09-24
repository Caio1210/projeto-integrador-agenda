using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace GestaoAgenda.Repositories;
public class ClienteRepository(GestaoAgendaContext context) : IClienteRepository
{
    private readonly GestaoAgendaContext _db = context;
    public Task<List<Cliente>> GetClientesAsync() => _db.Clientes.OrderBy(c => c.Nome).ToListAsync();
    public Task<Cliente?> GetClienteAsync(int? id) => _db.Clientes.FirstOrDefaultAsync(c => c.Id == id);
    public async Task CreateClienteAsync(Cliente cliente) { _db.Clientes.Add(cliente); await _db.SaveChangesAsync(); }
    public async Task UpdateClienteAsync(Cliente cliente) { _db.Clientes.Update(cliente); await _db.SaveChangesAsync(); }
    public async Task DeleteClienteAsync(int id) { var item = await _db.Clientes.FindAsync(id); if (item != null) _db.Clientes.Remove(item); await _db.SaveChangesAsync(); }
}
