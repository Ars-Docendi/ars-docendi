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
/// Una entidad que el buscador de menciones encontró, con los datos que se le
/// muestran al usuario y —para materias— la carrera que la distingue de un
/// homónimo.
/// </summary>
/// <param name="Id">
/// <c>identity.materias.id</c> o <c>identity.personas.id</c>, según
/// <see cref="TipoDeMencion"/>. Nunca llega al modelo (D11): sólo lo ve el
/// navegador del propio actor, y el ejecutor lo liga a un marcador `$refN` al
/// ejecutar la consulta.
/// </param>
/// <param name="Nombre">
/// El nombre de la materia, o el nombre completo («Nombre Apellido») del
/// docente.
/// </param>
/// <param name="Carrera">
/// La carrera de la materia. <c>null</c> para un docente: es lo que distingue
/// dos materias homónimas de carreras distintas (design.md, decisión 13).
/// </param>
/// <param name="Codigo">El código de la materia. <c>null</c> para un docente.</param>
/// <param name="Cargo">
/// El cargo de la designación vigente más reciente y visible del docente.
/// <c>null</c> para una materia.
/// </param>
public sealed record ResultadoDeMencion(
    Guid Id,
    string Nombre,
    string? Carrera = null,
    string? Codigo = null,
    string? Cargo = null);

/// <summary>El resultado de una búsqueda por término (RF de <c>GET /menciones</c>).</summary>
/// <param name="Resultados">A lo sumo el tope configurado, nunca más.</param>
/// <param name="HayMas">
/// Si había más coincidencias que las devueltas. <b>Nunca un conteo</b>: cuántas
/// quedaron afuera es un canal de inferencia sobre materias o docentes que el
/// actor no puede ver — el mismo motivo por el que <c>ResultadoDeConsulta</c>
/// nunca informa un total exacto sobre un recorte.
/// </param>
public sealed record BusquedaDeMenciones(IReadOnlyList<ResultadoDeMencion> Resultados, bool HayMas)
{
    /// <summary>Sin resultados, ninguno oculto.</summary>
    public static readonly BusquedaDeMenciones Vacia = new([], false);
}

/// <summary>
/// Busca materias y docentes dentro del alcance del actor, y resuelve una
/// mención por su identificador para revalidarla antes de un turno
/// (design.md D10/D11 de asistente-rediseno-v3).
/// </summary>
/// <remarks>
/// Corre sobre el rol básico de sólo lectura con <see cref="PreambuloDelActor"/>
/// (transacción de sólo lectura, actor fijado): el alcance no lo decide ningún
/// filtro de C#, lo decide el motor —<c>identity.asistente_materias_visibles()</c>
/// para materias, RLS de <c>designaciones.designaciones</c> para docentes—. Un
/// actor sin <c>designaciones.ver</c> no encuentra ningún docente porque la
/// policy le deja cero filas, no porque este puerto lo haya filtrado.
/// </remarks>
public interface IBuscadorDeMenciones
{
    /// <summary>
    /// Busca materias o docentes cuyo nombre (o código, para materias) empiece
    /// con <paramref name="termino"/> en alguna de sus palabras.
    /// </summary>
    /// <param name="termino">
    /// Al menos 2 caracteres. Es responsabilidad de quien llama —el
    /// controller— rechazar con <c>400</c> uno más corto antes de llegar acá.
    /// </param>
    Task<BusquedaDeMenciones> BuscarAsync(
        Guid actor, TipoDeMencion tipo, string termino, CancellationToken ct);

    /// <summary>
    /// Resuelve una mención por identificador, con el mismo alcance que
    /// <see cref="BuscarAsync"/>. <c>null</c> si no existe o el actor no la
    /// alcanza — las dos cosas se ven exactamente igual a propósito: no hay
    /// oráculo de existencia (design.md D11).
    /// </summary>
    Task<ResultadoDeMencion?> ResolverAsync(
        Guid actor, TipoDeMencion tipo, Guid id, CancellationToken ct);
}
