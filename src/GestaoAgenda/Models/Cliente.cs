using System.ComponentModel.DataAnnotations;

namespace GestaoAgenda.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do cliente")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = null!;

    [Required(ErrorMessage = "Informe o telefone")]
    [Display(Name = "Telefone")]
    public string Telefone { get; set; } = null!;

    [EmailAddress(ErrorMessage = "Informe um e-mail válido")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    public virtual ICollection<Agendamento> Agendamentos { get; set; } = [];
}
