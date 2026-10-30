using GestaoAgenda.Models;
using GestaoAgenda.Services;
using GestaoAgenda.ViewModels;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace GestaoAgenda.Controllers;
public class AgendamentoController(IAgendamentoRepository repository, IClienteRepository clienteRepository, IServicoRepository servicoRepository, ILogger<AgendamentoController> logger, AgendamentoService agendamentoService, ProfissionalServicoService ofertasService) : Controller
{
    [HttpGet("agenda/semana")]
    public async Task<IActionResult> Semana(string? data)
    {
        TempData["link"] = "agendamento";
        var selecionada = DateTime.Today;
        if (data is not null &&
            (!DateTime.TryParseExact(data, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out selecionada) ||
             selecionada > DateTime.MaxValue.Date.AddDays(-7)))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new AgendaSemanalViewModel(DateTime.Today)
            {
                Erro = "Data inválida. Selecione uma data válida para consultar a semana."
            });
        }

        var model = new AgendaSemanalViewModel(selecionada);
        try
        {
            model.Agendamentos = await repository.GetAgendamentosDaSemanaAsync(model.Inicio, model.FimExclusivo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao carregar a agenda semanal iniciada em {Inicio}", model.Inicio);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            model.Erro = "Não foi possível carregar os agendamentos. Tente novamente em instantes.";
        }
        return View(model);
    }

    [HttpGet("agendamento/profissionais")]
    public async Task<IActionResult> Profissionais(int servicoId)
    {
        if (servicoId <= 0) return BadRequest();
        try
        {
            var ofertas = await ofertasService.GetProfissionaisElegiveisAsync(servicoId);
            return Json(ofertas.Select(o => new
            {
                id = o.ProfissionalServicoId, profissionalId = o.ProfissionalId, nome = o.Nome,
                preco = PrecoPtBr.Formatar(o.Preco), duracaoMinutos = o.DuracaoMinutos
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao carregar profissionais do serviço {Id}", servicoId);
            return StatusCode(503, new { erro = "Não foi possível carregar os profissionais. Tente novamente." });
        }
    }

    [HttpGet("agenda")]
    public async Task<IActionResult> Index()
    {
        TempData["link"] = "agendamento";
        try { return View(await repository.GetAgendamentosAsync()); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao carregar agenda");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ViewData["Erro"] = "Não foi possível carregar a agenda. Recarregue e tente novamente.";
            return View(new List<Agendamento>());
        }
    }

    [HttpGet("agendamento/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        TempData["link"] = "agendamento";
        var item = await repository.GetAgendamentoAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpGet("agendamento/novo")]
    public async Task<IActionResult> Create()
    {
        TempData["link"] = "agendamento";
        var model = new AgendamentoFormViewModel { Data = DateTime.Today, Hora = TimeSpan.FromHours(9) };
        await CarregarFormularioAsync(model, null);
        return View(model);
    }

    [HttpPost("agendamento/novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AgendamentoFormViewModel model, string? acao)
    {
        if (model.Id != 0) return BadRequest();
        return await ProcessarAsync(model, null, nameof(Create), acao);
    }

    [HttpGet("agendamento/editar/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        TempData["link"] = "agendamento";
        try
        {
            var original = await repository.GetAgendamentoAsync(id);
            if (original is null) return NotFound();
            var model = AgendamentoFormViewModel.FromEntity(original);
            await CarregarFormularioAsync(model, original);
            return View(model);
        }
        catch (Exception ex)
        {
            var model = new AgendamentoFormViewModel { Id = id };
            FalhaFormulario(ex, model);
            return View(model);
        }
    }

    [HttpPost("agendamento/editar/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AgendamentoFormViewModel model, string? acao)
    {
        if (id != model.Id || id <= 0) return NotFound();
        Agendamento? original;
        try { original = await repository.GetAgendamentoAsync(id); }
        catch (Exception ex)
        {
            FalhaFormulario(ex, model);
            return View(model);
        }
        if (original is null) return NotFound();
        return await ProcessarAsync(model, original, nameof(Edit), acao);
    }
    private async Task<IActionResult> ProcessarAsync(AgendamentoFormViewModel model,
        Agendamento? original, string view, string? acao)
    {
        TempData["link"] = "agendamento";
        await CarregarFormularioAsync(model, original);
        if (acao == "carregar-profissionais" || !ModelState.IsValid || !model.PodeSalvar)
            return View(view, model);
        var resultado = await agendamentoService.SalvarAsync(model.ToEntity());
        if (resultado.NaoEncontrado) return NotFound();
        if (resultado.Sucesso)
        {
            TempData["Sucesso"] = "Agendamento salvo com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        foreach (var erro in resultado.Erros) ModelState.AddModelError(erro.Campo, erro.Mensagem);
        if (resultado.Indisponivel) Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View(view, model);
    }

    private async Task CarregarFormularioAsync(AgendamentoFormViewModel model, Agendamento? original)
    {
        try
        {
            model.Clientes = (await clienteRepository.GetClientesAsync())
                .Select(c => new SelectListItem(c.Nome, c.Id.ToString())).ToList();
            model.Servicos = (await servicoRepository.GetServicosAsync())
                .Select(s => new SelectListItem(s.Nome, s.Id.ToString())).ToList();
            if (original is not null)
            {
                model.AssociacaoOriginalId = original.ProfissionalServicoId;
                model.ServicoOriginalId = original.ServicoId;
                model.PrecoOriginal = original.PrecoAplicado;
                model.DuracaoOriginal = original.DuracaoMinutosAplicada;
                model.Legado = original.ProfissionalServicoId is null ||
                    original.PrecoAplicado is null || original.DuracaoMinutosAplicada is null;
            }
            var elegiveis = model.ServicoId <= 0 ? [] :
                await ofertasService.GetProfissionaisElegiveisAsync(model.ServicoId);
            model.Ofertas = elegiveis.Select(o =>
            {
                var manter = original?.ProfissionalServicoId == o.ProfissionalServicoId &&
                    original.ServicoId == model.ServicoId;
                return new OfertaAgendamentoViewModel(o.ProfissionalServicoId, o.Nome,
                    manter ? (original!.PrecoAplicado is decimal preco ? PrecoPtBr.Formatar(preco) : null) : PrecoPtBr.Formatar(o.Preco),
                    manter ? original!.DuracaoMinutosAplicada : o.DuracaoMinutos);
            }).ToList();
            // A oferta histórica permite somente preservar a reserva; não é uma opção elegível nova.
            if (original?.ProfissionalServicoId is int atual && original.ServicoId == model.ServicoId &&
                model.ProfissionalServicoId == atual && model.Ofertas.All(o => o.Id != atual))
            {
                model.Ofertas.Add(new(atual,
                    (original.ProfissionalServico?.Profissional.Nome ?? "Profissional da reserva") + " (oferta histórica/inativa)",
                    original.PrecoAplicado is decimal preco ? PrecoPtBr.Formatar(preco) : null,
                    original.DuracaoMinutosAplicada, Historica: true));
            }
        }
        catch (Exception ex) { FalhaFormulario(ex, model); }
    }

    private void FalhaFormulario(Exception ex, AgendamentoFormViewModel model)
    {
        logger.LogError(ex, "Erro ao carregar formulário de agendamento");
        model.PodeSalvar = false;
        ModelState.AddModelError(string.Empty, "Não foi possível carregar os dados do agendamento. Recarregue o formulário e tente novamente.");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    }

    [HttpGet("agendamento/apagar/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        TempData["link"] = "agendamento";
        var item = await repository.GetAgendamentoAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost("agendamento/apagar/{id:int}"), ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await repository.DeleteAgendamentoAsync(id);
        return RedirectToAction(nameof(Index));
    }
}