namespace GestaoAgenda.Models;

public class AgendaSemanalViewModel
{
    public AgendaSemanalViewModel(DateTime data)
    {
        Inicio = data.Date.AddDays(-((int)data.DayOfWeek + 6) % 7);
    }

    public DateTime Inicio { get; }
    public DateTime FimExclusivo => Inicio.AddDays(7);
    public DateTime UltimoDia => Inicio.AddDays(6);
    public DateTime? SemanaAnterior => Inicio >= DateTime.MinValue.AddDays(7) ? Inicio.AddDays(-7) : null;
    public DateTime? ProximaSemana => Inicio <= DateTime.MaxValue.Date.AddDays(-14) ? Inicio.AddDays(7) : null;
    public IReadOnlyList<Agendamento> Agendamentos { get; set; } = [];
    public string? Erro { get; set; }
}