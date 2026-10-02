namespace Modules.Asistente.Application;

/// <summary>
/// El breaker está abierto: no se llamó al proveedor.
/// </summary>
/// <remarks>
/// Tiene tipo propio porque significa algo distinto de un fallo de red. Nadie la
/// reintenta y nadie la loguea como error del proveedor: es el sistema haciendo
/// exactamente lo que se le pidió.
///
/// Vive en <c>Application</c> y no junto al decorador que la lanza porque quien la
/// atrapa —el carril y la capa conversacional— no puede depender de
/// infraestructura.
/// </remarks>
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
/// <remarks>
/// Tipo propio y no <see cref="TimeoutDelProveedor"/> a propósito: el proveedor
/// no falló, está OCUPADO atendiendo a otros. Contarlo como fallo abriría el
/// breaker y apagaría el asistente para todos justo cuando más se lo usa — el
/// breaker no la ve porque la compuerta va por fuera de él.
/// </remarks>
internal sealed class ProveedorSaturado(TimeSpan espera)
    : Exception($"No hubo lugar para llamar al modelo en {espera.TotalSeconds:0.#} s: hay demasiadas consultas en curso.")
{
    /// <summary>Lo que se esperó en la cola.</summary>
    public TimeSpan Espera { get; } = espera;
}
