using System.ComponentModel.DataAnnotations;
using GestaoAgenda.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestaoAgenda.ViewModels;

public class AgendamentoFormViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o cliente")]
    [Display(Name = "Cliente")]
    public int ClienteId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o serviço")]
    [Display(Name = "Serviço")]
    public int ServicoId { get; set; }

    [Display(Name = "Profissional")]
    public int? ProfissionalServicoId { get; set; }

    [Required(ErrorMessage = "Informe a data"), DataType(DataType.Date)]
    public DateTime? Data { get; set; }

    [Required(ErrorMessage = "Informe o horário"), DataType(DataType.Time)]
    [Display(Name = "Horário")]
    public TimeSpan? Hora { get; set; }

    [Required, RegularExpression("^(Agendado|Concluído|Cancelado)$", ErrorMessage = "Selecione um status válido")]
    public string Status { get; set; } = "Agendado";

    [StringLength(500, ErrorMessage = "A observação deve ter no máximo 500 caracteres")]
    [Display(Name = "Observação")]
    public string? Observacao { get; set; }

    [BindNever, ValidateNever] public bool PodeSalvar { get; set; } = true;
    [BindNever, ValidateNever] public bool Legado { get; set; }
    [BindNever, ValidateNever] public int ServicoOriginalId { get; set; }
    [BindNever, ValidateNever] public int? AssociacaoOriginalId { get; set; }
    [BindNever, ValidateNever] public decimal? PrecoOriginal { get; set; }
    [BindNever, ValidateNever] public int? DuracaoOriginal { get; set; }
    [BindNever, ValidateNever] public List<SelectListItem> Clientes { get; set; } = [];
    [BindNever, ValidateNever] public List<SelectListItem> Servicos { get; set; } = [];
    [BindNever, ValidateNever] public List<OfertaAgendamentoViewModel> Ofertas { get; set; } = [];

    public Agendamento ToEntity() => new()
    {
        Id = Id, ClienteId = ClienteId, ServicoId = ServicoId, ProfissionalServicoId = ProfissionalServicoId,
        Data = Data ?? DateTime.MinValue, Hora = Hora ?? TimeSpan.MinValue, Status = Status, Observacao = Observacao
    };

    public static AgendamentoFormViewModel FromEntity(Agendamento a) => new()
    {
        Id = a.Id, ClienteId = a.ClienteId, ServicoId = a.ServicoId,
        ProfissionalServicoId = a.ProfissionalServicoId, Data = a.Data, Hora = a.Hora,
        Status = a.Status, Observacao = a.Observacao
    };
}

public sealed record OfertaAgendamentoViewModel(int Id, string Nome, string? Preco,
    int? DuracaoMinutos, bool Historica = false);