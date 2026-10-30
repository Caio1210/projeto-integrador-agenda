using Microsoft.Extensions.Caching.Memory;

namespace GestaoAgenda.Services;

public enum EstadoInicioEnvio { Adquirido, EmAndamento, Concluido, Invalido }

// Proteção por instância, com tamanho e validade limitados. Não usa banco nem sessão.
public sealed class EnviosFormularioProfissional : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 2048 });

    private sealed class Envio(string finalidade)
    {
        public string Finalidade { get; } = finalidade;
        public EstadoInicioEnvio Estado { get; set; } = EstadoInicioEnvio.Adquirido;
        public bool EmUso { get; set; }
    }

    public string Novo(string finalidade)
    {
        var token = Guid.NewGuid().ToString("N");
        cache.Set(token, new Envio(finalidade),
            new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });
        return token;
    }

    public EstadoInicioEnvio Iniciar(string token, string finalidade)
    {
        if (!Guid.TryParseExact(token, "N", out _) || !cache.TryGetValue<Envio>(token, out var envio) || envio is null)
            return EstadoInicioEnvio.Invalido;
        lock (envio)
        {
            if (envio.Finalidade != finalidade) return EstadoInicioEnvio.Invalido;
            if (envio.Estado == EstadoInicioEnvio.Concluido) return EstadoInicioEnvio.Concluido;
            if (envio.EmUso) return EstadoInicioEnvio.EmAndamento;
            envio.EmUso = true;
            return EstadoInicioEnvio.Adquirido;
        }
    }

    public void Finalizar(string token, string finalidade, bool sucesso)
    {
        if (!cache.TryGetValue<Envio>(token, out var envio) || envio is null) return;
        lock (envio)
        {
            if (envio.Finalidade != finalidade) return;
            envio.Estado = sucesso ? EstadoInicioEnvio.Concluido : EstadoInicioEnvio.Adquirido;
            envio.EmUso = false;
        }
    }

    public void Dispose() => cache.Dispose();
}