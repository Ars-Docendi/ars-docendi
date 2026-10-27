namespace Modules.Asistente.Application;

/// <summary>
/// El motor rechazó la consulta generada. Las dos formas que importan son hermanas de esto para que
/// quien no necesita distinguirlas pueda atraparlas juntas.
/// </summary>
internal abstract class FallaDeLaConsulta(string mensaje, Exception causa)
    : Exception(mensaje, causa);

/// <summary>
/// El motor rechazó la lectura porque el rol del asistente no tiene el privilegio que la consulta
/// pide.
/// </summary>
internal sealed class ConsultaSinPrivilegio(Exception causa)
    : FallaDeLaConsulta(
        "El rol de lectura del asistente no tiene privilegio sobre lo que la consulta pide.",
        causa);

/// <summary>
/// El motor rechazó la consulta por cualquier otro motivo: SQL que el validador dejó pasar y no
/// ejecuta, un tipo incompatible, un techo de sentencia agotado.
/// </summary>
internal sealed class ConsultaRechazadaPorElMotor(
    string? estado, Exception causa, string? detalleDelMotor = null)
    : FallaDeLaConsulta($"El motor rechazó la consulta ({estado ?? "sin estado"}).", causa)
{
    /// <summary>El SQLSTATE que devolvió PostgreSQL.</summary>
    public string? Estado { get; } = estado;

    /// <summary>
    /// El mensaje y la pista del motor, CRUDOS (asistente-optimizaciones-modelo- local, D4).
    /// </summary>
    public string? DetalleDelMotor { get; } = detalleDelMotor;
}
