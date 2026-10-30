using System.ComponentModel.DataAnnotations;

namespace GestaoAgenda.Models;

public class ProfissionalServico : IValidatableObject
{
    public int Id { get; set; }
    public int ProfissionalId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione um serviço válido")]
    public int ServicoId { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "O preço deve ser maior que zero e caber em decimal(18,2)")]
    public decimal Preco { get; set; }

    [Range(1, 1440, ErrorMessage = "A duração deve estar entre 1 e 1440 minutos")]
    public int DuracaoMinutos { get; set; }

    public bool Ativo { get; set; } = true;
    public virtual Profissional Profissional { get; set; } = null!;
    public virtual Servico Servico { get; set; } = null!;
    public virtual ICollection<Agendamento> Agendamentos { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (decimal.Round(Preco, 2) != Preco)
            yield return new ValidationResult("Informe o preço com no máximo duas casas decimais", [nameof(Preco)]);
    }
}