namespace Modules.Asistente.Application;

/// <summary>Un área que el actor puede consultar, con su tamaño.</summary>
/// <param name="Nombre">Nombre calificado de la tabla, tal como está en la base.</param>
/// <param name="Descripcion">
/// El comentario de la tabla en el catálogo de PostgreSQL. Es el mismo texto que el
/// asistente le muestra al modelo en el prefijo del prompt: quien lo escribe lo
/// escribe una vez y sirve para las dos cosas.
/// </param>
/// <param name="Columnas">Cuántas columnas de esa tabla puede leer el actor.</param>
public sealed record AreaCubierta(string Nombre, string? Descripcion, int Columnas);

/// <summary>
/// Qué puede hacer el asistente para un actor concreto (RF-04).
/// </summary>
/// <param name="Cubre">Las áreas, con sus conteos.</param>
/// <param name="Ejemplos">Preguntas ejecutables, verificadas contra sus privilegios.</param>
/// <param name="NoPuede">Los límites del asistente.</param>
/// <param name="Alcance">
/// Qué filas ve. Va aparte de los conteos a propósito: el ámbito cambia
/// <b>qué filas</b> se ven, no <b>qué se puede preguntar</b>, y meterlo en los
/// conteos los haría mentir en las dos direcciones.
/// </param>
public sealed record CapacidadesDelActor(
    IReadOnlyList<AreaCubierta> Cubre,
    IReadOnlyList<string> Ejemplos,
    IReadOnlyList<string> NoPuede,
    string Alcance)
{
    /// <summary>Cuántas tablas puede consultar.</summary>
    public int Tablas => Cubre.Count;

    /// <summary>Cuántas columnas puede leer en total.</summary>
    public int Columnas => Cubre.Sum(area => area.Columnas);
}

/// <summary>
/// Arma el catálogo de capacidades a partir de los privilegios efectivos.
/// </summary>
/// <remarks>
/// <b>Nunca del payload del prompt, y ésta es la restricción entera del ticket.</b>
/// El esquema se inyecta completo en el prefijo, columnas personales incluidas: un
/// catálogo derivado de ahí le ofrecería al usuario preguntas sobre columnas que su
/// rol no puede leer. La consulta terminaría en <c>permission denied</c>, pero el
/// daño ya estaría hecho — el catálogo le habría dicho que esos datos existen y que
/// el asistente los tiene.
/// </remarks>
public interface ICatalogoDeCapacidades
{
    /// <summary>Resuelve el catálogo del actor. Cuesta cero tokens.</summary>
    Task<CapacidadesDelActor> ObtenerAsync(Guid actor, CancellationToken ct);
}
