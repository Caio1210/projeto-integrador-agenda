using GestaoAgenda.Models;
using GestaoAgenda.Repositories.Interfaces;
using GestaoAgenda.Services;
using GestaoAgenda.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GestaoAgenda.Controllers;

public class ProfissionalController(
    IProfissionalRepository repository,
    IServicoRepository servicoRepository,
    ProfissionalServicoService ofertasService,
    EnviosFormularioProfissional envios,
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
        try { return View(await repository.GetProfissionaisAsync()); }
        catch (Exception ex) { return FalhaCarregamento(ex); }
    }

    [HttpGet("profissional/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var profissional = await repository.GetProfissionalAsync(id);
            if (profissional is null) return NotFound();
            var associacoes = await ofertasService.GetAssociacoesAsync(id);
            return View(new ProfissionalDetalhesViewModel(profissional, associacoes));
        }
        catch (Exception ex) { return FalhaCarregamento(ex); }
    }

    [HttpGet("profissional/novo")]
    public async Task<IActionResult> Create()
    {
        var model = new ProfissionalFormViewModel { TokenEnvio = envios.Novo("criar") };
        await CarregarFormularioAsync(model, preservarEntrada: false);
        return View(model);
    }

    [HttpPost("profissional/novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProfissionalFormViewModel model)
    {
        if (model.Id != 0) return BadRequest();
        return await SalvarAsync(model, "criar", nameof(Create));
    }

    [HttpGet("profissional/editar/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var profissional = await repository.GetProfissionalAsync(id);
            if (profissional is null) return NotFound();
            var model = ProfissionalFormViewModel.FromEntity(profissional);
            model.TokenEnvio = envios.Novo($"editar:{id}");
            await CarregarFormularioAsync(model, preservarEntrada: false);
            return View(model);
        }
        catch (Exception ex) { return FalhaCarregamento(ex); }
    }

    [HttpPost("profissional/editar/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProfissionalFormViewModel model)
    {
        if (id != model.Id || id <= 0) return NotFound();
        return await SalvarAsync(model, $"editar:{id}", nameof(Edit));
    }

    private async Task<IActionResult> SalvarAsync(ProfissionalFormViewModel model, string finalidade, string view)
    {
        if (model.Id != 0)
        {
            try
            {
                if (await repository.GetProfissionalAsync(model.Id) is null) return NotFound();
            }
            catch (Exception ex)
            {
                FalhaFormulario(ex, model);
                return View(view, model);
            }
        }
        await CarregarFormularioAsync(model, preservarEntrada: true);

        // Validar novamente no servidor, sem confiar em valores ou nomes do navegador.
        if (model.Servicos.Select(s => s.ServicoId).Distinct().Count() != model.Servicos.Count)
            ModelState.AddModelError(nameof(model.Servicos), "Um serviço não pode aparecer mais de uma vez.");
        var ofertas = new List<ServicoOferecido>();
        for (var i = 0; i < model.Servicos.Count; i++)
        {
            var item = model.Servicos[i];
            if (!item.Selecionado) continue;
            if (!PrecoPtBr.TryParse(item.Preco, out var preco))
                ModelState.AddModelError($"Servicos[{i}].Preco", "Informe um preço válido em reais. Exemplo: 40,00");
            if (item.DuracaoMinutos is null or < 1 or > 1440)
                ModelState.AddModelError($"Servicos[{i}].DuracaoMinutos", "A duração deve estar entre 1 e 1440 minutos.");
            ofertas.Add(new(item.ServicoId, preco, item.DuracaoMinutos ?? 0, item.Ativo));
        }
        if (!ModelState.IsValid || !model.PodeSalvar) return View(view, model);

        var inicio = envios.Iniciar(model.TokenEnvio, finalidade);
        if (inicio == EstadoInicioEnvio.Concluido) return RedirectToAction(nameof(Index));
        if (inicio != EstadoInicioEnvio.Adquirido)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(string.Empty, inicio == EstadoInicioEnvio.EmAndamento
                ? "Este formulário já está sendo salvo. Aguarde e consulte a listagem."
                : "O formulário expirou ou é inválido. Recarregue a página antes de salvar.");
            model.PodeSalvar = false;
            return View(view, model);
        }

        var sucesso = false;
        try
        {
            var resultado = await ofertasService.SalvarAsync(model.ToEntity(), ofertas);
            if (!resultado.Sucesso)
            {
                foreach (var erro in resultado.Erros) ModelState.AddModelError(string.Empty, erro);
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return View(view, model);
            }
            sucesso = true;
            TempData["Sucesso"] = model.Id == 0
                ? "Profissional e serviços cadastrados com sucesso."
                : "Profissional e serviços atualizados com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao salvar profissional e serviços {Id}", model.Id);
            ModelState.AddModelError(string.Empty, "Não foi possível salvar. Os dados foram mantidos para uma nova tentativa.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(view, model);
        }
        finally { envios.Finalizar(model.TokenEnvio, finalidade, sucesso); }
    }

    private async Task CarregarFormularioAsync(ProfissionalFormViewModel model, bool preservarEntrada)
    {
        try
        {
            var servicos = await servicoRepository.GetServicosAsync();
            var catalogo = servicos.ToDictionary(s => s.Id);
            var associacoes = !preservarEntrada && model.Id != 0
                ? (await ofertasService.GetAssociacoesAsync(model.Id)).ToDictionary(s => s.ServicoId)
                : new Dictionary<int, ProfissionalServico>();

            if (!preservarEntrada)
            {
                model.Servicos = servicos.Select(s =>
                {
                    associacoes.TryGetValue(s.Id, out var oferta);
                    return new ServicoProfissionalFormViewModel
                    {
                        ServicoId = s.Id, NomeServico = s.Nome, Selecionado = oferta is not null,
                        Ativo = oferta?.Ativo ?? true,
                        Preco = PrecoPtBr.Formatar(oferta?.Preco ?? s.Valor),
                        DuracaoMinutos = oferta?.DuracaoMinutos ?? s.Duracao
                    };
                }).ToList();
            }
            else
            {
                foreach (var item in model.Servicos)
                {
                    if (catalogo.TryGetValue(item.ServicoId, out var servico)) item.NomeServico = servico.Nome;
                    else
                    {
                        item.NomeServico = "Serviço indisponível";
                        ModelState.AddModelError(nameof(model.Servicos), "Um dos serviços não está mais disponível. Recarregue a página.");
                    }
                }
                // Formulários incompletos não devem inativar ofertas silenciosamente.
                var recebidos = model.Servicos.Select(s => s.ServicoId).ToHashSet();
                if (catalogo.Keys.Any(id => !recebidos.Contains(id)))
                {
                    ModelState.AddModelError(nameof(model.Servicos), "A lista de serviços mudou ou está incompleta. Recarregue a página antes de salvar.");
                    model.PodeSalvar = false;
                }
            }
        }
        catch (Exception ex) { FalhaFormulario(ex, model); }
    }

    private void FalhaFormulario(Exception ex, ProfissionalFormViewModel model)
    {
        logger.LogError(ex, "Erro ao carregar formulário do profissional {Id}", model.Id);
        model.PodeSalvar = false;
        ViewData["ErroServicos"] = "Não foi possível carregar os serviços oferecidos. Recarregue a página para tentar novamente.";
        ModelState.AddModelError(string.Empty, "Não foi possível carregar os dados necessários. Nenhuma alteração foi salva.");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    }

    private IActionResult FalhaCarregamento(Exception ex)
    {
        logger.LogError(ex, "Erro ao carregar profissionais");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        ViewData["Erro"] = "Não foi possível carregar os profissionais. Tente novamente. Se o cadastro ainda não foi habilitado, verifique com o responsável a atualização do banco.";
        return View("Index", new List<Profissional>());
    }
}