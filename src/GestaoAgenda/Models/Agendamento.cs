using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    // Nulos identificam registros legados; não inferir valores históricos do cadastro atual.
    public int? ProfissionalServicoId { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true, ErrorMessage = "Informe um preço aplicado válido")]
    public decimal? PrecoAplicado { get; set; }

    [Range(1, 1440, ErrorMessage = "Informe uma duração aplicada entre 1 e 1440 minutos")]
    public int? DuracaoMinutosAplicada { get; set; }

    public virtual ProfissionalServico? ProfissionalServico { get; set; }

    [NotMapped]
    public DateTime? Termino => IntervaloAgendamento.Termino(Data, Hora, DuracaoMinutosAplicada);

    public virtual Cliente Cliente { get; set; } = null!;
    public virtual Servico Servico { get; set; } = null!;
}
