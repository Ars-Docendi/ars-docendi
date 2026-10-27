namespace Modules.Asistente.Application;

/// <summary>Qué clase de entidad nombra una mención (design.md D10/D11).</summary>
public enum TipoDeMencion
{
    /// <summary>Una fila de <c>identity.materias</c>.</summary>
    Materia,

    /// <summary>Una fila de <c>identity.personas</c> con al menos una designación visible.</summary>
    Docente,
}

/// <summary>
/// Una entidad que el buscador de menciones encontró, con los datos que se le muestran al usuario y
/// —para materias— la carrera que la distingue de un homónimo.
/// </summary>
public sealed record ResultadoDeMencion(
    Guid Id,
    string Nombre,
    string? Carrera = null,
    string? Codigo = null,
    string? Cargo = null);

/// <summary>El resultado de una búsqueda por término (RF de <c>GET /menciones</c>).</summary>
public sealed record BusquedaDeMenciones(IReadOnlyList<ResultadoDeMencion> Resultados, bool HayMas)
{
    /// <summary>Sin resultados, ninguno oculto.</summary>
    public static readonly BusquedaDeMenciones Vacia = new([], false);
}

/// <summary>
/// Busca materias y docentes dentro del alcance del actor, y resuelve una mención por su
/// identificador para revalidarla antes de un turno (design.md D10/D11 de asistente-rediseno-v3).
/// </summary>
public interface IBuscadorDeMenciones
{
    /// <summary>
    /// Busca materias o docentes cuyo nombre (o código, para materias) empiece con <paramref
    /// name="termino"/> en alguna de sus palabras.
    /// </summary>
    Task<BusquedaDeMenciones> BuscarAsync(
        Guid actor, TipoDeMencion tipo, string termino, CancellationToken ct);

    /// <summary>
    /// Resuelve una mención por identificador, con el mismo alcance que <see cref="BuscarAsync"/>.
    /// </summary>
    Task<ResultadoDeMencion?> ResolverAsync(
        Guid actor, TipoDeMencion tipo, Guid id, CancellationToken ct);
}
