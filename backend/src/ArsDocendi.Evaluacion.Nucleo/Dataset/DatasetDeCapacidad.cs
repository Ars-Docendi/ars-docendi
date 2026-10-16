using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArsDocendi.Evaluacion.Nucleo.Dataset;

/// <summary>Dificultad técnica que ilustra un ítem.</summary>
public static class CategoriaDeItem
{
    public const string ConsultaSimple = "consulta_simple";
    public const string FiltroTemporal = "filtro_temporal";
    public const string CruceDeTablas = "cruce_de_tablas";
    public const string Agregacion = "agregacion";
    public const string NoContestable = "no_contestable";
    public const string Ambigua = "ambigua";

    /// <summary>Lista cerrada. Un ítem con otra categoría es un error del dataset.</summary>
    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        ConsultaSimple, FiltroTemporal, CruceDeTablas, Agregacion, NoContestable, Ambigua,
    };

    /// <summary>Las categorías en las que el asistente <b>debe</b> abstenerse.</summary>
    public static bool EsInfactible(string categoria) =>
        categoria is NoContestable or Ambigua;
}

/// <summary>
/// Quién hace la pregunta. Se nombra por alcance y no por identificador, para que
/// el dataset se lea sin tener el fixture al lado.
/// </summary>
public static class ActorDeItem
{
    public const string Global = "global";
    public const string Carrera = "carrera";
    public const string Materia = "materia";
    public const string SinPermiso = "sin_permiso";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Global, Carrera, Materia, SinPermiso,
    };
}

/// <summary>
/// Una mención declarada por un ítem, tal como el fixture la conoce (design.md
/// D10/D11 de asistente-rediseno-v3, ARS-148).
/// </summary>
/// <param name="Marcador">El <c>$refN</c> que <c>SqlReferencia</c> tiene que usar.</param>
/// <param name="Tipo"><c>"materia"</c> o <c>"docente"</c>.</param>
/// <param name="Indice">
/// El índice del fixture —<c>GeneradorDeFixture.IdDeMateria</c> o
/// <c>IdDePersona</c>, según <see cref="Tipo"/>—, para no depender de un GUID
/// escrito a mano que dejaría de significar algo si el fixture cambia de forma.
/// </param>
/// <param name="Nombre">
/// El nombre de la entidad, tal como <c>GeneradorDeFixture</c> la nombra. Un test
/// verifica que coincide con <c>MateriasCompartidas</c>/<c>ApellidosCompartidos</c>,
/// así que un fixture que cambiara esos nombres rompería el dataset en vez de
/// dejarlo describir una entidad que ya no existe.
/// </param>
/// <param name="Carrera">La carrera de la materia. <c>null</c> para un docente.</param>
public sealed record ReferenciaDeItem(string Marcador, string Tipo, int Indice, string Nombre, string? Carrera = null);

/// <summary>Un ítem del dataset de capacidad.</summary>
/// <param name="Id">Identificador estable. El gate de regresión lo usa como lock.</param>
/// <param name="Pregunta">La pregunta, tal como la escribiría alguien.</param>
/// <param name="Categoria">Dificultad técnica que ilustra.</param>
/// <param name="Actor">Con qué alcance se ejecuta.</param>
/// <param name="SqlReferencia">
/// La consulta que responde bien, o nulo si el ítem es infactible. Se guarda la
/// <b>consulta</b> y no su resultado: con resultados guardados, cualquier cambio
/// del fixture desincroniza el dataset en silencio y la métrica pasa a medir esa
/// diferencia en vez de medir al asistente. Si <see cref="Referencias"/> declara
/// marcadores, esta consulta los usa tal cual —<c>$ref1</c>, nunca el id— y el
/// runner los liga de la misma forma que <c>EjecutorDeConsulta</c>.
/// </param>
/// <param name="OrdenImporta">
/// Si el orden de las filas es parte de la pregunta. Por omisión no lo es: dos
/// consultas que devuelven las mismas filas en distinto orden responden lo mismo.
/// </param>
/// <param name="Referencias">
/// Las menciones «@materia»/«#docente» de este ítem, o nulo si no tiene ninguna
/// (design.md D11). El runner las manda a <c>GeneradorDeSql</c> igual que un
/// turno real, así que el eje de capacidad también mide esta traducción.
/// </param>
/// <param name="MotivosAceptables">
/// Los motivos de rechazo que este ítem acepta como correctos
/// (asistente-rechazos-dinamicos, design.md D10), sólo en un ítem
/// <c>no_contestable</c>. Informativo: no cambia el desenlace del ítem —una
/// abstención con un motivo distinto sigue siendo <c>AbstencionCorrecta</c>—,
/// sólo alimenta la sección de acuerdo del reporte.
/// </param>
public sealed record ItemDeCapacidad(
    string Id,
    string Pregunta,
    string Categoria,
    string Actor,
    string? SqlReferencia,
    bool OrdenImporta,
    IReadOnlyList<ReferenciaDeItem>? Referencias = null,
    IReadOnlyList<string>? MotivosAceptables = null)
{
    /// <summary>Si el asistente tiene que abstenerse en este ítem.</summary>
    public bool EsInfactible => CategoriaDeItem.EsInfactible(Categoria);
}

/// <summary>
/// El conjunto cerrado de motivos de rechazo, tal como lo declara el modelo
/// (asistente-rechazos-dinamicos). Duplicado deliberado del enum
/// <c>MotivoDeRechazo</c> de <c>Modules.Asistente</c>: éste es un archivo de
/// datos versionado y no puede depender de un tipo <c>internal</c> de otro
/// proyecto.
/// </summary>
public static class MotivosDeRechazoAceptables
{
    public const string FueraDeTema = "fuera_de_tema";
    public const string OtroSistema = "otro_sistema";
    public const string MuyGeneral = "muy_general";
    public const string NoCubierto = "no_cubierto";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        FueraDeTema, OtroSistema, MuyGeneral, NoCubierto,
    };
}

/// <summary>
/// El dataset de capacidad, cargado del archivo versionado.
/// </summary>
/// <remarks>
/// Mide si el asistente traduce la pregunta a la consulta correcta. Estratificado
/// por dificultad técnica para que el número no esconda que acierta lo fácil y
/// falla lo que importa.
/// </remarks>
public sealed class DatasetDeCapacidad
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private DatasetDeCapacidad(IReadOnlyList<ItemDeCapacidad> items, string huella)
    {
        Items = items;
        Huella = huella;
    }

    /// <summary>Los ítems, en el orden del archivo.</summary>
    public IReadOnlyList<ItemDeCapacidad> Items { get; }

    /// <summary>
    /// Huella estable del archivo, para el sellado de reportes.
    /// </summary>
    /// <remarks>
    /// Se calcula sobre los bytes del archivo y no sobre el objeto ya
    /// interpretado: así un cambio de formato que no altere el contenido igual
    /// queda registrado, que es lo que el sellado necesita.
    /// </remarks>
    public string Huella { get; }

    /// <summary>Cuántos ítems hay de cada categoría.</summary>
    public IReadOnlyDictionary<string, int> ConteoPorCategoria() =>
        Items.GroupBy(item => item.Categoria, StringComparer.Ordinal)
            .OrderBy(grupo => grupo.Key, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count(), StringComparer.Ordinal);

    /// <summary>Carga el dataset desde un archivo.</summary>
    public static DatasetDeCapacidad Cargar(string ruta)
    {
        var crudo = File.ReadAllText(ruta);
        return Interpretar(crudo);
    }

    /// <summary>Interpreta el dataset desde su texto.</summary>
    public static DatasetDeCapacidad Interpretar(string crudo)
    {
        var archivo = JsonSerializer.Deserialize<ArchivoDeDataset>(crudo, Opciones)
            ?? throw new InvalidOperationException("El dataset de capacidad no se pudo interpretar.");

        if (archivo.Items.Count == 0)
        {
            throw new InvalidOperationException("El dataset de capacidad no tiene ningún ítem.");
        }

        var items = archivo.Items
            .Select(item => new ItemDeCapacidad(
                item.Id, item.Pregunta, item.Categoria, item.Actor,
                item.SqlReferencia, item.OrdenImporta,
                item.Referencias?.Select(r => new ReferenciaDeItem(
                    r.Marcador, r.Tipo, r.Indice, r.Nombre, r.Carrera)).ToArray(),
                item.MotivosAceptables))
            .ToArray();

        foreach (var item in items)
        {
            ValidarMotivosAceptables(item);
        }

        var repetidos = items.GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key)
            .ToArray();

        if (repetidos.Length > 0)
        {
            // Los identificadores son el lock del gate de regresión: repetidos, dos
            // ítems distintos compartirían su historial.
            throw new InvalidOperationException(
                $"El dataset tiene identificadores repetidos: {string.Join(", ", repetidos)}.");
        }

        var huella = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(crudo)));
        return new DatasetDeCapacidad(items, huella);
    }

    /// <summary>
    /// Valida <see cref="ItemDeCapacidad.MotivosAceptables"/> (design.md D10 de
    /// asistente-rechazos-dinamicos): no vacío, cada valor del conjunto
    /// cerrado, y sólo declarado en un ítem <c>no_contestable</c>.
    /// </summary>
    private static void ValidarMotivosAceptables(ItemDeCapacidad item)
    {
        if (item.Categoria != CategoriaDeItem.NoContestable)
        {
            if (item.MotivosAceptables is not null)
            {
                throw new InvalidOperationException(
                    $"El ítem '{item.Id}' es de categoría '{item.Categoria}' y declara "
                    + "`motivos_aceptables`: sólo un ítem `no_contestable` puede declararlo.");
            }

            return;
        }

        if (item.MotivosAceptables is null)
        {
            // OPCIONAL EN LA UNIDAD A (tarea 8.1): todavía no hay dataset que lo
            // declare en todos los ítems `no_contestable` — eso llega con la
            // tarea 10.2, en la corrida financiada. `capacidad.json` de hoy
            // sigue cargando sin tocarlo.
            return;
        }

        if (item.MotivosAceptables.Count == 0)
        {
            throw new InvalidOperationException(
                $"El ítem '{item.Id}' declara `motivos_aceptables` vacío: tiene que nombrar al "
                + "menos un motivo, o no declarar la clave.");
        }

        var invalidos = item.MotivosAceptables
            .Where(motivo => !MotivosDeRechazoAceptables.Todos.Contains(motivo))
            .ToArray();

        if (invalidos.Length > 0)
        {
            throw new InvalidOperationException(
                $"El ítem '{item.Id}' declara `motivos_aceptables` fuera del conjunto cerrado: "
                + string.Join(", ", invalidos) + ".");
        }
    }

    private sealed class ArchivoDeDataset
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<ItemDeArchivo> Items { get; init; } = [];
    }

    private sealed class ItemDeArchivo
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("pregunta")]
        public string Pregunta { get; init; } = string.Empty;

        [JsonPropertyName("categoria")]
        public string Categoria { get; init; } = string.Empty;

        [JsonPropertyName("actor")]
        public string Actor { get; init; } = string.Empty;

        [JsonPropertyName("sql_referencia")]
        public string? SqlReferencia { get; init; }

        [JsonPropertyName("orden_importa")]
        public bool OrdenImporta { get; init; }

        [JsonPropertyName("referencias")]
        public IReadOnlyList<ReferenciaDeArchivo>? Referencias { get; init; }

        [JsonPropertyName("motivos_aceptables")]
        public IReadOnlyList<string>? MotivosAceptables { get; init; }
    }

    private sealed class ReferenciaDeArchivo
    {
        [JsonPropertyName("marcador")]
        public string Marcador { get; init; } = string.Empty;

        [JsonPropertyName("tipo")]
        public string Tipo { get; init; } = string.Empty;

        [JsonPropertyName("indice")]
        public int Indice { get; init; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; init; } = string.Empty;

        [JsonPropertyName("carrera")]
        public string? Carrera { get; init; }
    }
}
