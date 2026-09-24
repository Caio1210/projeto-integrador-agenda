using GestaoAgenda.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace GestaoAgenda.Controllers;
public class HomeController(IClienteRepository clienteRepository, IServicoRepository servicoRepository, IAgendamentoRepository agendamentoRepository) : Controller
{
    public async Task<IActionResult> Index()
    {
        var clientes = await clienteRepository.GetClientesAsync();
        var servicos = await servicoRepository.GetServicosAsync();
        var agendamentos = await agendamentoRepository.GetAgendamentosAsync();
        var hoje = DateTime.Today;
        ViewBag.TotalClientes = clientes.Count;
        ViewBag.TotalServicos = servicos.Count;
        ViewBag.TotalAgendamentos = agendamentos.Count;
        ViewBag.AgendamentosHoje = agendamentos.Where(a => a.Data.Date == hoje && a.Status != "Cancelado").OrderBy(a => a.Hora).ToList();
        ViewBag.Proximos = agendamentos.Where(a => a.Data.Date >= hoje && a.Status == "Agendado").OrderBy(a => a.Data).ThenBy(a => a.Hora).Take(8).ToList();
        return View();
    }
    public IActionResult Error() => View();
}
