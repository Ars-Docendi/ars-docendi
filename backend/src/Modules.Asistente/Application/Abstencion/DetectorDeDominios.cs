namespace Modules.Asistente.Application;

/// <summary>Los dominios de datos cuyo alcance cambia qué significa un resultado vacío.</summary>
[Flags]
internal enum DominioConsultado
{
    /// <summary>Sólo catálogos de identity: no agrega ninguna exigencia propia.</summary>
    Ninguno = 0,
    Designaciones = 1,
    Portal = 2,
    Comisiones = 4,
    Aulas = 8,

    /// <summary>Un schema habilitado que este detector no sabe evaluar: nunca se asume alcance total.</summary>
    Desconocido = 16,
}

/// <summary>Las tablas que una consulta nombra y los dominios a los que pertenecen.</summary>
internal sealed record TablasDeLaConsulta(
    IReadOnlyList<string> Calificadas, DominioConsultado Dominios);

/// <summary>
/// Detecta qué tablas y dominios toca una consulta generada. Es la ÚNICA detección del turno: de
/// acá salen la declaración de cobertura de portal y la decisión de alcance, para que no puedan
/// discrepar.
/// </summary>
/// <remarks>
/// Trabaja sobre tokens y no sobre el texto: un alias o un comentario que nombra un dominio
/// («AS comisiones», «-- designaciones.pedidos») no lo toca, y una búsqueda de subcadena lo
/// confundiría.
/// </remarks>
internal static class DetectorDeDominios
{
    private static readonly IReadOnlyDictionary<string, DominioConsultado> DominioDeCadaSchema =
        new Dictionary<string, DominioConsultado>(StringComparer.Ordinal)
        {
            ["identity"] = DominioConsultado.Ninguno,
            ["designaciones"] = DominioConsultado.Designaciones,
            ["portal"] = DominioConsultado.Portal,
            ["comisiones"] = DominioConsultado.Comisiones,
            ["aulas"] = DominioConsultado.Aulas,
        };

    public static readonly TablasDeLaConsulta Ninguna = new([], DominioConsultado.Ninguno);

    public static TablasDeLaConsulta Detectar(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return Ninguna;
        }

        IReadOnlyList<TokenSql> tokens;
        try
        {
            tokens = TokenizadorSql.Tokenizar(sql);
        }
        catch (SqlSinCerrar)
        {
            // Una consulta validada no llega acá sin cerrar. Si pasara, no se puede saber qué toca:
            // el dominio queda desconocido y por lo tanto sin alcance total.
            return new TablasDeLaConsulta([], DominioConsultado.Desconocido);
        }

        var dominios = DominioConsultado.Ninguno;
        var tablas = new SortedSet<string>(StringComparer.Ordinal);

        for (var indice = 0; indice < tokens.Count; indice++)
        {
            var token = tokens[indice];
            if (!EsNombre(token) || !EsSchemaHabilitado(token.Texto))
            {
                continue;
            }

            var siguiente = indice + 1 < tokens.Count ? tokens[indice + 1] : default;
            if (!SeCalificaConUnPunto(sql, token, siguiente))
            {
                continue;
            }

            dominios |= DominioDeCadaSchema.TryGetValue(token.Texto, out var dominio)
                ? dominio
                : DominioConsultado.Desconocido;
            tablas.Add($"{token.Texto}.{siguiente.Texto}");
        }

        return new TablasDeLaConsulta([.. tablas], dominios);
    }

    private static bool EsNombre(TokenSql token) =>
        token.Clase is ClaseDeToken.Palabra or ClaseDeToken.IdentificadorEntrecomillado;

    private static bool EsSchemaHabilitado(string texto) =>
        DominioDeCadaSchema.ContainsKey(texto) || EtiquetasDeAreas.SchemasHabilitados.Contains(texto);

    /// <summary>
    /// Si el token es un schema calificando una tabla: <c>schema.tabla</c>. Una palabra sin comillas
    /// tiene que llevar el punto pegado. Entre comillas el tokenizador no guarda la posición, así que
    /// se pide que lo siga el nombre de la tabla.
    /// </summary>
    private static bool SeCalificaConUnPunto(string sql, TokenSql token, TokenSql siguiente)
    {
        if (!EsNombre(siguiente))
        {
            return false;
        }

        if (token.Clase == ClaseDeToken.IdentificadorEntrecomillado)
        {
            return true;
        }

        var fin = token.Inicio + token.Texto.Length;
        return token.Inicio >= 0 && fin < sql.Length && sql[fin] == '.';
    }
}
