namespace Modules.Asistente.Application;

/// <summary>Un área que el actor puede consultar, con su tamaño.</summary>
public sealed record AreaCubierta(string Nombre, string? Descripcion, int Columnas);

/// <summary>Qué puede hacer el asistente para un actor concreto (RF-04).</summary>
public sealed record CapacidadesDelActor(
    IReadOnlyList<AreaCubierta> Cubre,
    IReadOnlyList<string> Ejemplos,
    IReadOnlyList<string> NoPuede,
    string Alcance,
    string Presentacion,
    EstadoDeMantenimiento Mantenimiento,
    EstadoDelCupoDelActor Cupo)
{
    /// <summary>Cuántas tablas puede consultar.</summary>
    public int Tablas => Cubre.Count;

    /// <summary>Cuántas columnas puede leer en total.</summary>
    public int Columnas => Cubre.Sum(area => area.Columnas);
}

/// <summary>Arma el catálogo de capacidades a partir de los privilegios efectivos.</summary>
public interface ICatalogoDeCapacidades
{
    /// <summary>Resuelve el catálogo del actor. Cuesta cero tokens.</summary>
    Task<CapacidadesDelActor> ObtenerAsync(Guid actor, CancellationToken ct);
}
