using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace GestaoAgenda.Controllers;
public class ServicoController(IServicoRepository repository) : Controller
{
    [Route("servicos")]
    public async Task<IActionResult> Index() { TempData["link"] = "servico"; return View(await repository.GetServicosAsync()); }
    [Route("servico/{id}")]
    public async Task<IActionResult> Details(int? id) { TempData["link"] = "servico"; if (id == null) return NotFound(); var item = await repository.GetServicoAsync(id); return item == null ? NotFound() : View(item); }
    [Route("servico/novo")]
    public IActionResult Create() { TempData["link"] = "servico"; return View(); }
    [HttpPost, ValidateAntiForgeryToken, Route("servico/novo")]
    public async Task<IActionResult> Create(Servico servico) { if (ModelState.IsValid) { await repository.CreateServicoAsync(servico); return RedirectToAction(nameof(Index)); } return View(servico); }
    [Route("servico/editar/{id}")]
    public async Task<IActionResult> Edit(int? id) { TempData["link"] = "servico"; if (id == null) return NotFound(); var item = await repository.GetServicoAsync(id); return item == null ? NotFound() : View(item); }
    [HttpPost, ValidateAntiForgeryToken, Route("servico/editar/{id}")]
    public async Task<IActionResult> Edit(int id, Servico servico) { if (id != servico.Id) return NotFound(); if (ModelState.IsValid) { await repository.UpdateServicoAsync(servico); return RedirectToAction(nameof(Index)); } return View(servico); }
    [Route("servico/apagar/{id}")]
    public async Task<IActionResult> Delete(int? id) { TempData["link"] = "servico"; if (id == null) return NotFound(); var item = await repository.GetServicoAsync(id); return item == null ? NotFound() : View(item); }
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Route("servico/apagar/{id}")]
    public async Task<IActionResult> DeleteConfirmed(int id) { try { await repository.DeleteServicoAsync(id); } catch { TempData["Erro"] = "Não é possível excluir um serviço que possui agendamentos."; } return RedirectToAction(nameof(Index)); }
}
