using System.ComponentModel.DataAnnotations;

namespace GestaoAgenda.Models;

public class Agendamento
{
    public int Id { get; set; }

    [Display(Name = "Cliente")]
    [Required(ErrorMessage = "Selecione o cliente")]
    public int ClienteId { get; set; }

    [Display(Name = "Serviço")]
    [Required(ErrorMessage = "Selecione o serviço")]
    public int ServicoId { get; set; }

    [Required(ErrorMessage = "Informe a data")]
    [DataType(DataType.Date)]
    [Display(Name = "Data")]
    public DateTime Data { get; set; }

    [Required(ErrorMessage = "Informe o horário")]
    [DataType(DataType.Time)]
    [Display(Name = "Horário")]
    public TimeSpan Hora { get; set; }

    [Required(ErrorMessage = "Informe o status")]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Agendado";

    [Display(Name = "Observação")]
    public string? Observacao { get; set; }

    public virtual Cliente Cliente { get; set; } = null!;
    public virtual Servico Servico { get; set; } = null!;
}
