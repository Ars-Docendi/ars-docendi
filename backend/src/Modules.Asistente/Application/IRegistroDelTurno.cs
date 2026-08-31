namespace Modules.Asistente.Application;

/// <summary>Por qué camino se resolvió el turno.</summary>
public enum CarrilDelTurno
{
    /// <summary>Saludo, agradecimiento o meta-pregunta: cero tokens.</summary>
    SinDatos,

    /// <summary>Terminó pidiendo una aclaración.</summary>
    Aclaracion,

    /// <summary>Generó y ejecutó una consulta.</summary>
    Sql,

    /// <summary>No había modelo disponible.</summary>
    Degradado,
}

/// <summary>
/// Todo lo que un turno deja para registrar, antes de separarlo en dos.
/// </summary>
/// <remarks>
/// Entra completo y sale partido en dos filas que no comparten ninguna columna.
/// Tenerlo en un solo tipo hace que la separación ocurra en <b>un solo lugar</b>,
/// que es donde se la puede leer y verificar; repartida entre los llamadores, cada
/// uno decidiría por su cuenta qué mandar a dónde.
///
/// <b>No incluye las filas devueltas ni la consulta generada.</b> No es un olvido:
/// no se persisten en ningún registro, y no tenerlas acá hace que no se puedan
/// persistir por accidente.
/// </remarks>
/// <param name="Actor">Quién consultó. Va solo al registro operativo.</param>
/// <param name="Cuando">
/// Cuándo. Al operativo va completo; al analítico, redondeado al día.
/// </param>
/// <param name="Pregunta">
/// Qué se preguntó. Va solo al registro analítico.
/// </param>
/// <param name="Proveedor">
/// Quién respondió: la identidad que expone <see cref="IProveedorDeModelo.Nombre"/>,
/// nunca su credencial. Va solo al registro operativo, porque es una propiedad del
/// costo del turno y no de la pregunta.
/// </param>
public sealed record TurnoParaRegistrar(
    Guid Actor,
    DateTimeOffset Cuando,
    CarrilDelTurno Carril,
    EstadoDelTurno Estado,
    int LlamadasAlModelo,
    int TokensDeEntrada,
    int TokensDeSalida,
    int LatenciaMs,
    bool HuboReintento,
    bool Truncado,
    string Pregunta,
    string Categoria,
    string Proveedor);

/// <summary>
/// Escribe los dos registros desvinculados (RF-16).
/// </summary>
/// <remarks>
/// <b>Nunca hace fallar el turno.</b> Un registro que rompe el turno que estaba
/// registrando convierte la observabilidad en una fuente de indisponibilidad.
///
/// Es la decisión inversa a la del enmascarador, y a propósito: ahí un fallo
/// silencioso filtra datos, acá un fallo ruidoso niega un servicio que funciona.
/// </remarks>
public interface IRegistroDelTurno
{
    /// <summary>Registra un turno. No propaga errores.</summary>
    Task RegistrarAsync(TurnoParaRegistrar turno, CancellationToken ct);
}
