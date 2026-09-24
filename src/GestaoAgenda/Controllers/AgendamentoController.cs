using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace GestaoAgenda.Controllers;
public class AgendamentoController(IAgendamentoRepository repository, IClienteRepository clienteRepository, IServicoRepository servicoRepository) : Controller
{
    private async Task CarregarListasAsync(int? clienteId = null, int? servicoId = null)
    {
        ViewData["ClienteId"] = new SelectList(await clienteRepository.GetClientesAsync(), "Id", "Nome", clienteId);
        ViewData["ServicoId"] = new SelectList(await servicoRepository.GetServicosAsync(), "Id", "Nome", servicoId);
        ViewData["Status"] = new SelectList(new[] { "Agendado", "Concluído", "Cancelado" });
    }
    [Route("agenda")]
    public async Task<IActionResult> Index() { TempData["link"] = "agendamento"; return View(await repository.GetAgendamentosAsync()); }
    [Route("agendamento/{id}")]
    public async Task<IActionResult> Details(int? id) { TempData["link"] = "agendamento"; if (id == null) return NotFound(); var item = await repository.GetAgendamentoAsync(id); return item == null ? NotFound() : View(item); }
    [Route("agendamento/novo")]
    public async Task<IActionResult> Create() { TempData["link"] = "agendamento"; await CarregarListasAsync(); return View(new Agendamento { Data = DateTime.Today, Hora = new TimeSpan(9, 0, 0), Status = "Agendado" }); }
    [HttpPost, ValidateAntiForgeryToken, Route("agendamento/novo")]
    public async Task<IActionResult> Create(Agendamento agendamento)
    {
        ModelState.Remove("Cliente"); ModelState.Remove("Servico");
        if (ModelState.IsValid && !await repository.HorarioDisponivelAsync(agendamento.Data, agendamento.Hora)) ModelState.AddModelError("Hora", "Este horário já está ocupado.");
        if (ModelState.IsValid) { await repository.CreateAgendamentoAsync(agendamento); return RedirectToAction(nameof(Index)); }
        await CarregarListasAsync(agendamento.ClienteId, agendamento.ServicoId); return View(agendamento);
    }
    [Route("agendamento/editar/{id}")]
    public async Task<IActionResult> Edit(int? id) { TempData["link"] = "agendamento"; if (id == null) return NotFound(); var item = await repository.GetAgendamentoAsync(id); if (item == null) return NotFound(); await CarregarListasAsync(item.ClienteId, item.ServicoId); return View(item); }
    [HttpPost, ValidateAntiForgeryToken, Route("agendamento/editar/{id}")]
    public async Task<IActionResult> Edit(int id, Agendamento agendamento)
    {
        ModelState.Remove("Cliente"); ModelState.Remove("Servico");
        if (id != agendamento.Id) return NotFound();
        if (ModelState.IsValid && !await repository.HorarioDisponivelAsync(agendamento.Data, agendamento.Hora, agendamento.Id)) ModelState.AddModelError("Hora", "Este horário já está ocupado.");
        if (ModelState.IsValid) { await repository.UpdateAgendamentoAsync(agendamento); return RedirectToAction(nameof(Index)); }
        await CarregarListasAsync(agendamento.ClienteId, agendamento.ServicoId); return View(agendamento);
    }
    [Route("agendamento/apagar/{id}")]
    public async Task<IActionResult> Delete(int? id) { TempData["link"] = "agendamento"; if (id == null) return NotFound(); var item = await repository.GetAgendamentoAsync(id); return item == null ? NotFound() : View(item); }
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Route("agendamento/apagar/{id}")]
    public async Task<IActionResult> DeleteConfirmed(int id) { await repository.DeleteAgendamentoAsync(id); return RedirectToAction(nameof(Index)); }
}
