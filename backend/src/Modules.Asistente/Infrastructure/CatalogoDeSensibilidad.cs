using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Resuelve el manifiesto de sensibilidad a identificadores del motor y cachea el
/// resultado.
/// </summary>
/// <remarks>
/// <b>Una sola resolución para los dos roles.</b> El identificador de tabla y el
/// número de atributo no dependen de quién consulta: son del catálogo, y
/// <c>pg_attribute</c> es legible por cualquiera con independencia de los
/// <c>GRANT</c>. Una columna que solo el rol con datos personales puede leer
/// aparece igual en el catálogo leído con el rol básico, así que alcanza con
/// resolver una vez.
///
/// <b>Perezoso, por el mismo motivo que el prefijo del prompt.</b> Resolver al
/// arrancar exigiría una conexión durante el arranque del Host, y el invariante #3
/// pide que el <c>ping</c> responda con la base detenida.
/// </remarks>
internal sealed class CatalogoDeSensibilidad(
    CadenaSoloLectura cadena,
    ManifiestoDeSensibilidad manifiesto) : IClasificadorDeSensibilidad
{
    /// <summary>
    /// Los esquemas se cualifican siempre: los roles del asistente corren con
    /// <c>search_path</c> vacío, así que un nombre sin cualificar no resuelve.
    /// </summary>
    private const string Sql = """
        -- El OID se castea a bigint a propósito: el tipo `oid` de PostgreSQL no
        -- tiene lectura directa a un entero del lado del cliente, y castearlo acá
        -- deja la conversión escrita en un solo lugar.
        SELECT c.oid::bigint, a.attnum, n.nspname, c.relname, a.attname
          FROM pg_catalog.pg_class c
          JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
          JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid
         WHERE c.relkind = 'r'
           AND a.attnum > 0
           AND NOT a.attisdropped
           AND n.nspname = ANY(@esquemas)
        """;

    private readonly SemaphoreSlim _turnoDeCalculo = new(1, 1);
    private IReadOnlyDictionary<(uint Oid, short Atributo), SensibilidadDeColumna>? _resuelto;

    /// <summary>Veces que se consultó el catálogo. Existe para los tests del caché.</summary>
    internal int Lecturas { get; private set; }

    public SensibilidadDeColumna Clasificar(uint oidDeTabla, short numeroDeAtributo)
    {
        if (_resuelto is null)
        {
            throw new InvalidOperationException(
                $"Se pidió clasificar una columna sin haber llamado antes a " +
                $"{nameof(PrepararAsync)}. Sin la resolución no se puede distinguir una " +
                "columna sensible de una pública, y devolver 'pública' por omisión " +
                "mandaría datos personales al proveedor.");
        }

        // Origen sin reportar: el motor manda cero cuando la columna no es una
        // referencia directa a una columna de tabla.
        if (oidDeTabla == 0 || numeroDeAtributo <= 0)
        {
            return SensibilidadDeColumna.Desconocida;
        }

        return _resuelto.TryGetValue((oidDeTabla, numeroDeAtributo), out var sensibilidad)
            ? sensibilidad
            : SensibilidadDeColumna.Desconocida;
    }

    public async Task PrepararAsync(CancellationToken ct)
    {
        if (_resuelto is not null)
        {
            return;
        }

        await _turnoDeCalculo.WaitAsync(ct);
        try
        {
            if (_resuelto is not null)
            {
                return;
            }

            _resuelto = await ResolverAsync(ct);
        }
        finally
        {
            _turnoDeCalculo.Release();
        }
    }

    private async Task<IReadOnlyDictionary<(uint, short), SensibilidadDeColumna>> ResolverAsync(
        CancellationToken ct)
    {
        var esquemas = manifiesto.Tablas
            .Select(tabla => tabla.Schema)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Clave del catálogo real -> par de identificadores del motor.
        var enLaBase = new Dictionary<string, (uint Oid, short Atributo)>(StringComparer.Ordinal);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using (var comando = new NpgsqlCommand(Sql, conexion))
        {
            comando.Parameters.AddWithValue("esquemas", esquemas);

            await using var lector = await comando.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
            {
                var clave = $"{lector.GetString(2)}.{lector.GetString(3)}.{lector.GetString(4)}";
                enLaBase[clave] = ((uint)lector.GetInt64(0), lector.GetInt16(1));
            }
        }

        Lecturas++;

        var resuelto = new Dictionary<(uint, short), SensibilidadDeColumna>();
        var faltantes = new List<string>();

        foreach (var (schema, tabla, entrada) in manifiesto.Entradas())
        {
            var clave = $"{schema}.{tabla}.{entrada.Columna}";
            if (!enLaBase.TryGetValue(clave, out var identificadores))
            {
                faltantes.Add(clave);
                continue;
            }

            resuelto[identificadores] =
                new SensibilidadDeColumna(entrada.Clasificacion, entrada.Etiqueta);
        }

        if (faltantes.Count > 0)
        {
            // Falla en lugar de ignorarlas. Una columna que el manifiesto nombra y
            // la base no tiene significa que el manifiesto quedó viejo, y un
            // manifiesto viejo es exactamente el que deja de clasificar la columna
            // nueva que ocupó su lugar.
            throw new InvalidOperationException(
                "El manifiesto de sensibilidad nombra columnas que no existen en la base: " +
                string.Join(", ", faltantes.Order(StringComparer.Ordinal)));
        }

        return resuelto;
    }
}
