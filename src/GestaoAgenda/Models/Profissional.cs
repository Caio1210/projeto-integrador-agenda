using System.ComponentModel.DataAnnotations;

namespace GestaoAgenda.Models;

public class Profissional
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do profissional")]
    [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o telefone")]
    [StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres")]
    [Phone(ErrorMessage = "Informe um telefone válido")]
    [Display(Name = "Telefone")]
    public string Telefone { get; set; } = string.Empty;

    [StringLength(120, ErrorMessage = "O e-mail deve ter no máximo 120 caracteres")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Informe a especialidade")]
    [StringLength(120, ErrorMessage = "A especialidade deve ter no máximo 120 caracteres")]
    [Display(Name = "Especialidade")]
    public string Especialidade { get; set; } = string.Empty;

    [Display(Name = "Profissional ativo")]
    public bool Ativo { get; set; } = true;

    public virtual ICollection<ProfissionalServico> ServicosOferecidos { get; set; } = [];
}