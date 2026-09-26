namespace Modules.Asistente.Application;

/// <summary>
/// El motor rechazó la consulta generada. Las dos formas que importan son
/// hermanas de esto para que quien no necesita distinguirlas pueda atraparlas
/// juntas.
/// </summary>
internal abstract class FallaDeLaConsulta(string mensaje, Exception causa)
    : Exception(mensaje, causa);

/// <summary>
/// El motor rechazó la lectura porque el rol del asistente no tiene el
/// privilegio que la consulta pide.
/// </summary>
/// <remarks>
/// Tiene tipo propio porque significa algo distinto de un rechazo cualquiera: es
/// la frontera de motor funcionando. Para quien pregunta, «no tenés acceso a eso»
/// es una respuesta, no una falla, y el carril la resuelve como abstención.
///
/// <b>El mensaje no lleva nada del rechazo original</b>: el texto crudo de
/// PostgreSQL nombra la tabla y la columna, y eso no puede salir del turno. El
/// detalle viaja en la excepción interna, que va al registro.
/// </remarks>
internal sealed class ConsultaSinPrivilegio(Exception causa)
    : FallaDeLaConsulta(
        "El rol de lectura del asistente no tiene privilegio sobre lo que la consulta pide.",
        causa);

/// <summary>
/// El motor rechazó la consulta por cualquier otro motivo: SQL que el validador
/// dejó pasar y no ejecuta, un tipo incompatible, un techo de sentencia agotado.
/// </summary>
/// <remarks>
/// Igual que su hermana, sólo expone el SQLSTATE. Es lo único del rechazo que se
/// puede loguear sin arrastrar nombres del esquema.
/// </remarks>
internal sealed class ConsultaRechazadaPorElMotor(string? estado, Exception causa)
    : FallaDeLaConsulta($"El motor rechazó la consulta ({estado ?? "sin estado"}).", causa)
{
    /// <summary>El SQLSTATE que devolvió PostgreSQL.</summary>
    public string? Estado { get; } = estado;
}
