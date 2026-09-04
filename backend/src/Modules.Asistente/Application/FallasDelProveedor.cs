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
public sealed class ProveedorNoDisponible()
    : Exception("El proveedor del modelo está fuera de servicio y no se lo llamó.");

/// <summary>El proveedor no respondió dentro del tiempo de una llamada.</summary>
public sealed class TimeoutDelProveedor(TimeSpan cuanto)
    : Exception($"El proveedor del modelo no respondió en {cuanto.TotalSeconds:0.#} s.")
{
    /// <summary>El tiempo que se le dio.</summary>
    public TimeSpan Cuanto { get; } = cuanto;
}
