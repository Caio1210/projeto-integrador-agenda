using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GestaoAgenda.Controllers;

public class ProfissionalController(
    IProfissionalRepository repository,
    ILogger<ProfissionalController> logger) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        TempData["link"] = "profissional";
        base.OnActionExecuting(context);
    }

    [HttpGet("profissionais")]
    public async Task<IActionResult> Index()
    {
        try
        {
            return View(await repository.GetProfissionaisAsync());
        }
        catch (Exception ex)
        {
            return FalhaCarregamento(ex);
        }
    }

    [HttpGet("profissional/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var profissional = await repository.GetProfissionalAsync(id);
            return profissional is null ? NotFound() : View(profissional);
        }
        catch (Exception ex)
        {
            return FalhaCarregamento(ex);
        }
    }

    [HttpGet("profissional/novo")]
    public IActionResult Create() => View(new Profissional());

    [HttpPost("profissional/novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Nome,Telefone,Email,Especialidade,Ativo")] Profissional profissional)
    {
        if (!ModelState.IsValid) return View(profissional);
        try
        {
            await repository.CreateProfissionalAsync(profissional);
            TempData["Sucesso"] = "Profissional cadastrado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao cadastrar profissional");
            ModelState.AddModelError(string.Empty, "Não foi possível salvar o profissional. Tente novamente.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(profissional);
        }
    }

    [HttpGet("profissional/editar/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var profissional = await repository.GetProfissionalAsync(id);
            return profissional is null ? NotFound() : View(profissional);
        }
        catch (Exception ex)
        {
            return FalhaCarregamento(ex);
        }
    }

    [HttpPost("profissional/editar/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,Nome,Telefone,Email,Especialidade,Ativo")] Profissional profissional)
    {
        if (id != profissional.Id) return NotFound();
        if (!ModelState.IsValid) return View(profissional);
        try
        {
            if (!await repository.UpdateProfissionalAsync(profissional)) return NotFound();
            TempData["Sucesso"] = "Profissional atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao atualizar profissional {Id}", id);
            ModelState.AddModelError(string.Empty, "Não foi possível salvar as alterações. Tente novamente.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(profissional);
        }
    }

    private IActionResult FalhaCarregamento(Exception ex)
    {
        logger.LogError(ex, "Erro ao carregar profissionais");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        ViewData["Erro"] = "Não foi possível carregar os profissionais. Tente novamente. Se o cadastro ainda não foi habilitado, verifique com o responsável a atualização do banco.";
        return View("Index", new List<Profissional>());
    }
}