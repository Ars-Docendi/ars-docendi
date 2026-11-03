using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Traduce el rechazo de PostgreSQL al tipo del módulo.</summary>
internal static class FallaDelMotor
{
    /// <summary>SQLSTATE de <c>insufficient_privilege</c>.</summary>
    private const string PrivilegioDenegado = "42501";

    public static Exception Traducir(PostgresException excepcion) =>
        excepcion.SqlState == PrivilegioDenegado
            ? new ConsultaSinPrivilegio(excepcion)
            : new ConsultaRechazadaPorElMotor(
                excepcion.SqlState,
                excepcion,
                string.IsNullOrWhiteSpace(excepcion.Hint)
                    ? excepcion.MessageText
                    : $"{excepcion.MessageText} {excepcion.Hint}");
}
