using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Traduce el rechazo de PostgreSQL al tipo del módulo.
/// </summary>
/// <remarks>
/// Existe para que <c>Application</c> no tenga que referenciar Npgsql. Antes el
/// carril atrapaba <c>PostgresException</c> directamente, así que la capa que
/// decide qué contestarle a una persona conocía el driver de la base.
/// </remarks>
internal static class FallaDelMotor
{
    /// <summary>SQLSTATE de <c>insufficient_privilege</c>.</summary>
    private const string PrivilegioDenegado = "42501";

    public static Exception Traducir(PostgresException excepcion) =>
        excepcion.SqlState == PrivilegioDenegado
            ? new ConsultaSinPrivilegio(excepcion)
            : new ConsultaRechazadaPorElMotor(excepcion.SqlState, excepcion);
}
