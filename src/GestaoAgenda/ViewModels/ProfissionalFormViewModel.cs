using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using GestaoAgenda.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestaoAgenda.ViewModels;

public class ProfissionalFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do profissional")]
    [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o telefone")]
    [StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres")]
    [Phone(ErrorMessage = "Informe um telefone válido")]
    public string Telefone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Informe um e-mail válido")]
    [StringLength(120, ErrorMessage = "O e-mail deve ter no máximo 120 caracteres")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Informe a especialidade")]
    [StringLength(120, ErrorMessage = "A especialidade deve ter no máximo 120 caracteres")]
    public string Especialidade { get; set; } = string.Empty;

    [Display(Name = "Profissional ativo")]
    public bool Ativo { get; set; } = true;

    [Required(ErrorMessage = "Recarregue o formulário antes de salvar")]
    public string TokenEnvio { get; set; } = string.Empty;

    public List<ServicoProfissionalFormViewModel> Servicos { get; set; } = [];

    [BindNever, ValidateNever]
    public bool PodeSalvar { get; set; } = true;

    public Profissional ToEntity() => new()
    {
        Id = Id, Nome = Nome, Telefone = Telefone, Email = Email,
        Especialidade = Especialidade, Ativo = Ativo
    };

    public static ProfissionalFormViewModel FromEntity(Profissional profissional) => new()
    {
        Id = profissional.Id, Nome = profissional.Nome, Telefone = profissional.Telefone,
        Email = profissional.Email, Especialidade = profissional.Especialidade, Ativo = profissional.Ativo
    };
}

public class ServicoProfissionalFormViewModel : IValidatableObject
{
    public int ServicoId { get; set; }

    [BindNever, ValidateNever]
    public string NomeServico { get; set; } = string.Empty;

    public bool Selecionado { get; set; }
    public bool Ativo { get; set; } = true;
    public string? Preco { get; set; }
    public int? DuracaoMinutos { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Selecionado) yield break;
        if (ServicoId <= 0)
            yield return new ValidationResult("Selecione um serviço válido", [nameof(ServicoId)]);
        if (!PrecoPtBr.TryParse(Preco, out _))
            yield return new ValidationResult("Informe um preço maior que zero, com até duas casas decimais. Exemplo: 40,00", [nameof(Preco)]);
        if (DuracaoMinutos is null or < 1 or > 1440)
            yield return new ValidationResult("A duração deve estar entre 1 e 1440 minutos", [nameof(DuracaoMinutos)]);
    }
}

public static class PrecoPtBr
{
    // Sem ponto decimal; agrupamento de milhar exige centavos para evitar ambiguidade.
    private static readonly Regex Formato = new(
        @"^(?:[0-9]+(?:,[0-9]{1,2})?|[1-9][0-9]{0,2}(?:[.][0-9]{3})+,[0-9]{2})$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static bool TryParse(string? texto, out decimal preco)
    {
        preco = 0;
        var valor = texto?.Trim();
        return valor is { Length: > 0 and <= 24 } && Formato.IsMatch(valor) &&
            decimal.TryParse(valor, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.GetCultureInfo("pt-BR"), out preco) &&
            preco >= 0.01m && preco <= 9999999999999999.99m;
    }

    public static string Formatar(decimal preco) => preco.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
}

public sealed record ProfissionalDetalhesViewModel(Profissional Profissional,
    IReadOnlyList<ProfissionalServico> Servicos);