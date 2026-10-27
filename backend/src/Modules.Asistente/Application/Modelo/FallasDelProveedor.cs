namespace Modules.Asistente.Application;

/// <summary>El breaker está abierto: no se llamó al proveedor.</summary>
internal sealed class ProveedorNoDisponible()
    : Exception("El proveedor del modelo está fuera de servicio y no se lo llamó.");

/// <summary>El proveedor no respondió dentro del tiempo de una llamada.</summary>
internal sealed class TimeoutDelProveedor(TimeSpan cuanto)
    : Exception($"El proveedor del modelo no respondió en {cuanto.TotalSeconds:0.#} s.")
{
    /// <summary>El tiempo que se le dio.</summary>
    public TimeSpan Cuanto { get; } = cuanto;
}

/// <summary>
/// La compuerta de concurrencia no dio lugar a tiempo: no se llamó al proveedor
/// (asistente-proveedor-local, design.md D4).
/// </summary>
internal sealed class ProveedorSaturado(TimeSpan espera, string? motivo = null)
    : Exception(motivo ?? $"No hubo lugar para llamar al modelo en {espera.TotalSeconds:0.#} s: hay demasiadas consultas en curso.")
{
    /// <summary>Lo que se esperó en la cola.</summary>
    public TimeSpan Espera { get; } = espera;
}
