using System.ComponentModel.DataAnnotations;

namespace GestaoAgenda.Models;

public class Servico
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do serviço")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = null!;

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Required(ErrorMessage = "Informe a duração")]
    [Range(1, 1440, ErrorMessage = "A duração deve estar entre 1 e 1440 minutos")]
    [Display(Name = "Duração (minutos)")]
    public int Duracao { get; set; }

    [Required(ErrorMessage = "Informe o valor")]
    [Range(0, 999999, ErrorMessage = "Informe um valor válido")]
    [DataType(DataType.Currency)]
    [Display(Name = "Valor")]
    public decimal Valor { get; set; }

    public virtual ICollection<Agendamento> Agendamentos { get; set; } = [];
}
