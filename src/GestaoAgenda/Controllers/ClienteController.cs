using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace GestaoAgenda.Controllers;
public class ClienteController(IClienteRepository repository) : Controller
{
    [Route("clientes")]
    public async Task<IActionResult> Index() { TempData["link"] = "cliente"; return View(await repository.GetClientesAsync()); }
    [Route("cliente/{id}")]
    public async Task<IActionResult> Details(int? id) { TempData["link"] = "cliente"; if (id == null) return NotFound(); var item = await repository.GetClienteAsync(id); return item == null ? NotFound() : View(item); }
    [Route("cliente/novo")]
    public IActionResult Create() { TempData["link"] = "cliente"; return View(); }
    [HttpPost, ValidateAntiForgeryToken, Route("cliente/novo")]
    public async Task<IActionResult> Create(Cliente cliente) { if (ModelState.IsValid) { await repository.CreateClienteAsync(cliente); return RedirectToAction(nameof(Index)); } return View(cliente); }
    [Route("cliente/editar/{id}")]
    public async Task<IActionResult> Edit(int? id) { TempData["link"] = "cliente"; if (id == null) return NotFound(); var item = await repository.GetClienteAsync(id); return item == null ? NotFound() : View(item); }
    [HttpPost, ValidateAntiForgeryToken, Route("cliente/editar/{id}")]
    public async Task<IActionResult> Edit(int id, Cliente cliente) { if (id != cliente.Id) return NotFound(); if (ModelState.IsValid) { await repository.UpdateClienteAsync(cliente); return RedirectToAction(nameof(Index)); } return View(cliente); }
    [Route("cliente/apagar/{id}")]
    public async Task<IActionResult> Delete(int? id) { TempData["link"] = "cliente"; if (id == null) return NotFound(); var item = await repository.GetClienteAsync(id); return item == null ? NotFound() : View(item); }
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Route("cliente/apagar/{id}")]
    public async Task<IActionResult> DeleteConfirmed(int id) { try { await repository.DeleteClienteAsync(id); } catch { TempData["Erro"] = "Não é possível excluir um cliente que possui agendamentos."; } return RedirectToAction(nameof(Index)); }
}
