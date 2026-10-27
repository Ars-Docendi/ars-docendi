namespace Modules.Asistente.Application;

/// <summary>Veredicto del validador.</summary>
internal sealed record ResultadoDeValidacion(bool EsValida, string? Motivo)
{
    /// <summary>Veredicto favorable.</summary>
    public static readonly ResultadoDeValidacion Valida = new(true, null);

    /// <summary>Arma un veredicto de rechazo.</summary>
    public static ResultadoDeValidacion Rechazada(string motivo) => new(false, motivo);
}

/// <summary>
/// Segunda capa de defensa sobre la consulta generada (RNF-08, RNF-23). La primera es el motor: el
/// rol de lectura no tiene privilegios de mutación y la transacción se declara de solo lectura.
/// </summary>
internal static class ValidadorDeSql
{
    /// <summary>
    /// Funciones prohibidas. Se chequean tanto en palabras sueltas como en identificadores
    /// entrecomillados.
    /// </summary>
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

    /// <summary>Palabras clave prohibidas. Se chequean <b>solo</b> en palabras sueltas.</summary>
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
    /// Con qué empiezan los nombres del catálogo del sistema. Es un conjunto cerrado por el motor:
    /// toda relación de <c>pg_catalog</c> —y toda función del catálogo— empieza con <c>pg_</c>.
    /// </summary>
    private const string PrefijoDelCatalogo = "pg_";

    /// <summary>El catálogo del estándar, que no lleva prefijo: se nombra entero.</summary>
    private const string CatalogoDelEstandar = "information_schema";

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
    public static ResultadoDeValidacion Validar(
        string sql,
        IReadOnlySet<string>? marcadoresDeclarados = null,
        IReadOnlySet<string>? marcadoresQueDebenUsarse = null)
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

        return Verificar(tokens, marcadoresDeclarados, marcadoresQueDebenUsarse);
    }

    private static ResultadoDeValidacion Verificar(
        IReadOnlyList<TokenSql> tokens,
        IReadOnlySet<string>? marcadoresDeclarados,
        IReadOnlySet<string>? marcadoresQueDebenUsarse)
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

        return VerificarMarcadores(tokens, marcadoresDeclarados, marcadoresQueDebenUsarse);
    }

    /// <summary>
    /// Los dos chequeos de D11: ningún marcador sin declarar, y todo marcador
    /// nuevo declarado tiene que aparecer.
    /// </summary>
    private static ResultadoDeValidacion VerificarMarcadores(
        IReadOnlyList<TokenSql> tokens,
        IReadOnlySet<string>? marcadoresDeclarados,
        IReadOnlySet<string>? marcadoresQueDebenUsarse)
    {
        var usados = tokens
            .Where(t => t.Clase == ClaseDeToken.Marcador)
            .Select(t => t.Texto)
            .ToHashSet(StringComparer.Ordinal);

        if (usados.Count > 0)
        {
            var declarados = marcadoresDeclarados ?? new HashSet<string>(StringComparer.Ordinal);
            var noDeclarado = usados.FirstOrDefault(m => !declarados.Contains(m));
            if (noDeclarado is not null)
            {
                return ResultadoDeValidacion.Rechazada(
                    $"La consulta usa el marcador '{noDeclarado}', que no está declarado para este turno.");
            }
        }

        var requeridos = marcadoresQueDebenUsarse ?? new HashSet<string>(StringComparer.Ordinal);
        var sinUsar = requeridos.FirstOrDefault(m => !usados.Contains(m));
        if (sinUsar is not null)
        {
            return ResultadoDeValidacion.Rechazada(
                $"La consulta no usa el marcador '{sinUsar}', declarado para una mención de este turno.");
        }

        return ResultadoDeValidacion.Valida;
    }

    private static string? MotivoDeRechazo(TokenSql token) => token.Clase switch
    {
        ClaseDeToken.Palabra when FuncionesProhibidas.Contains(token.Texto) =>
            $"La consulta usa la función prohibida '{token.Texto}'.",

        ClaseDeToken.Palabra when PalabrasClaveProhibidas.Contains(token.Texto) =>
            $"La consulta usa la palabra clave prohibida '{token.Texto}'.",

        // El catálogo del motor se deniega en las dos clases de token: entre
        // comillas dobles sigue nombrando la misma relación.
        ClaseDeToken.Palabra or ClaseDeToken.IdentificadorEntrecomillado
            when EsDelCatalogoDelMotor(token.Texto) =>
            $"La consulta nombra el catálogo del motor ('{token.Texto}').",

        // Un identificador entrecomillado se chequea contra funciones y nada más.
        ClaseDeToken.IdentificadorEntrecomillado when FuncionesProhibidas.Contains(token.Texto) =>
            $"La consulta invoca la función prohibida '{token.Texto}' entre comillas dobles.",

        _ => null,
    };

    /// <summary>
    /// Si el nombre pertenece al catálogo del motor. El catálogo describe el esquema entero, los
    /// roles y los privilegios: es el mapa que hace falta para escribir el próximo intento, y
    /// material que la política de redacción prohíbe que salga del turno.
    /// </summary>
    private static bool EsDelCatalogoDelMotor(string texto) =>
        texto.StartsWith(PrefijoDelCatalogo, StringComparison.Ordinal)
        || string.Equals(texto, CatalogoDelEstandar, StringComparison.Ordinal);
}
