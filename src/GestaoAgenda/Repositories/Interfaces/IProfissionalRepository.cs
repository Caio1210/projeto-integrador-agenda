using GestaoAgenda.Models;

namespace GestaoAgenda.Repositories.Interfaces;

public interface IProfissionalRepository
{
    Task<List<Profissional>> GetProfissionaisAsync();
    Task<Profissional?> GetProfissionalAsync(int id);
    Task CreateProfissionalAsync(Profissional profissional);
    Task<bool> UpdateProfissionalAsync(Profissional profissional);
}