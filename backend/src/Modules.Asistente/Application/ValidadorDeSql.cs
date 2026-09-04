namespace Modules.Asistente.Application;

/// <summary>
/// Veredicto del validador.
/// </summary>
/// <param name="EsValida">Si la consulta puede ejecutarse.</param>
/// <param name="Motivo">
/// Por qué se rechazó, para el registro de diagnóstico. <b>No se le muestra al
/// usuario</b>: nombra construcciones de SQL, y el texto que sale del turno no
/// puede hablar de esquema ni de consultas.
/// </param>
public sealed record ResultadoDeValidacion(bool EsValida, string? Motivo)
{
    /// <summary>Veredicto favorable.</summary>
    public static readonly ResultadoDeValidacion Valida = new(true, null);

    /// <summary>Arma un veredicto de rechazo.</summary>
    public static ResultadoDeValidacion Rechazada(string motivo) => new(false, motivo);
}

/// <summary>
/// Segunda capa de defensa sobre la consulta generada (RNF-08, RNF-23).
/// </summary>
/// <remarks>
/// <b>Segunda</b>, no primera. La primera es el motor: el rol de lectura no tiene
/// privilegios de mutación y la transacción se declara de solo lectura. Cada vez
/// que las dos capas dicen cosas distintas, gana la del motor.
///
/// Este validador sube el costo de un ataque y convierte un turno peligroso en
/// una abstención barata. No es lo que hace que el sistema sea seguro; es lo que
/// hace que el sistema no dependa de que el modelo colabore.
/// </remarks>
public static class ValidadorDeSql
{
    /// <summary>
    /// Funciones prohibidas. Se chequean tanto en palabras sueltas como en
    /// identificadores entrecomillados.
    /// </summary>
    /// <remarks>
    /// Las de reloj están acá porque la fecha del turno entra por parámetro
    /// (RF-18): si la consulta nunca necesita saber la hora, prohibir el reloj no
    /// rompe ningún caso legítimo, y una regla que hasta ahora solo vivía en el
    /// prompt pasa a estar impuesta.
    ///
    /// Las de configuración de sesión están acá porque son la vía por la que una
    /// consulta podría fijarse un actor distinto del suyo.
    /// </remarks>
    private static readonly HashSet<string> FuncionesProhibidas = new(StringComparer.Ordinal)
    {
        // Reloj — las ocho.
        "now", "current_date", "current_timestamp", "localtime", "localtimestamp",
        "statement_timestamp", "clock_timestamp", "transaction_timestamp",

        // Configuración de sesión: la vía al ajuste del actor.
        "set_config", "current_setting",

        // Lectura del sistema de archivos y del entorno del servidor.
        "pg_read_file", "pg_read_binary_file", "pg_ls_dir", "pg_stat_file",
        "lo_import", "lo_export", "pg_file_write", "pg_file_read",

        // Salida hacia afuera del motor.
        "dblink", "dblink_connect", "dblink_exec", "pg_logical_emit_message",

        // Consumo de recursos y señales a otras sesiones.
        "pg_sleep", "pg_sleep_for", "pg_sleep_until",
        "pg_terminate_backend", "pg_cancel_backend",
    };

    /// <summary>
    /// Palabras clave prohibidas. Se chequean <b>solo</b> en palabras sueltas.
    /// </summary>
    /// <remarks>
    /// No se chequean en identificadores entrecomillados a propósito: ahí no son
    /// palabras clave sino nombres, y un alias legítimo como <c>AS "cantidad"</c>
    /// —o incluso <c>AS "select"</c>— no tiene nada de malo. Chequearlas también
    /// ahí rompería consultas correctas sin cerrar ningún agujero.
    ///
    /// <c>end</c> queda deliberadamente afuera: <c>CASE ... END</c> es legítimo y
    /// frecuente. <c>close</c>, <c>fetch</c> y <c>move</c> también, porque son
    /// nombres plausibles de columna y su forma peligrosa exige una sentencia
    /// propia, que ya está prohibida por otro lado.
    /// </remarks>
    private static readonly HashSet<string> PalabrasClaveProhibidas = new(StringComparer.Ordinal)
    {
        // Mutación de datos.
        "insert", "update", "delete", "truncate", "merge", "upsert",

        // Definición y permisos.
        "create", "alter", "drop", "grant", "revoke", "comment", "security",
        "owner", "rename",

        // Movimiento masivo de datos y materialización.
        "copy", "into", "refresh",

        // Ejecución de código.
        "do", "call", "execute", "prepare", "deallocate", "explain", "analyze", "analyse",

        // Control de transacción y de sesión.
        "begin", "start", "commit", "rollback", "savepoint", "set", "reset",
        "discard", "lock", "listen", "notify", "unlisten",

        // Mantenimiento.
        "vacuum", "reindex", "cluster", "checkpoint",
    };

    /// <summary>
    /// Con qué palabra puede empezar una consulta. Es una lista blanca a
    /// propósito: las listas de prohibiciones se quedan cortas, y acá alcanza con
    /// dos entradas para cubrir todo lo que el carril necesita.
    /// </summary>
    private static readonly HashSet<string> ComienzosAdmitidos = new(StringComparer.Ordinal)
    {
        "select", "with",
    };

    /// <summary>Decide si la consulta generada puede ejecutarse.</summary>
    public static ResultadoDeValidacion Validar(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return ResultadoDeValidacion.Rechazada("La consulta está vacía.");
        }

        IReadOnlyList<TokenSql> tokens;
        try
        {
            tokens = TokenizadorSql.Tokenizar(sql);
        }
        catch (SqlSinCerrar excepcion)
        {
            // Una construcción sin cerrar se rechaza en lugar de intentar
            // interpretarla: si el tokenizador no sabe dónde termina un literal,
            // tampoco sabe qué parte del texto es código.
            return ResultadoDeValidacion.Rechazada(excepcion.Message);
        }

        return Verificar(tokens);
    }

    private static ResultadoDeValidacion Verificar(IReadOnlyList<TokenSql> tokens)
    {
        var primeraPalabra = tokens.FirstOrDefault(t => t.Clase == ClaseDeToken.Palabra);
        if (primeraPalabra.Texto is null || !ComienzosAdmitidos.Contains(primeraPalabra.Texto))
        {
            return ResultadoDeValidacion.Rechazada(
                "La consulta no empieza con SELECT ni con WITH.");
        }

        var separadores = tokens.Count(t => t.Clase == ClaseDeToken.FinDeSentencia);
        if (separadores > 1 || (separadores == 1 && tokens[^1].Clase != ClaseDeToken.FinDeSentencia))
        {
            return ResultadoDeValidacion.Rechazada(
                "La consulta contiene más de una sentencia.");
        }

        foreach (var token in tokens)
        {
            var motivo = MotivoDeRechazo(token);
            if (motivo is not null)
            {
                return ResultadoDeValidacion.Rechazada(motivo);
            }
        }

        return ResultadoDeValidacion.Valida;
    }

    private static string? MotivoDeRechazo(TokenSql token) => token.Clase switch
    {
        ClaseDeToken.Palabra when FuncionesProhibidas.Contains(token.Texto) =>
            $"La consulta usa la función prohibida '{token.Texto}'.",

        ClaseDeToken.Palabra when PalabrasClaveProhibidas.Contains(token.Texto) =>
            $"La consulta usa la palabra clave prohibida '{token.Texto}'.",

        // Un identificador entrecomillado se chequea contra funciones y nada más.
        // En PostgreSQL las comillas dobles no hacen del nombre una cadena: siguen
        // nombrando al mismo objeto.
        ClaseDeToken.IdentificadorEntrecomillado when FuncionesProhibidas.Contains(token.Texto) =>
            $"La consulta invoca la función prohibida '{token.Texto}' entre comillas dobles.",

        _ => null,
    };
}
