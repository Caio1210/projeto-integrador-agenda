namespace GestaoAgenda.Models;

public static class IntervaloAgendamento
{
    public static DateTime? Termino(DateTime data, TimeSpan hora, int? duracaoMinutos)
    {
        if (hora < TimeSpan.Zero || hora >= TimeSpan.FromDays(1) || duracaoMinutos is null or < 1 or > 1440)
            return null;
        try { return data.Date.Add(hora).AddMinutes(duracaoMinutos.Value); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}