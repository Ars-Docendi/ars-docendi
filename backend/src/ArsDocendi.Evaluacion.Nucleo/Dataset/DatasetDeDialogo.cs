using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArsDocendi.Evaluacion.Nucleo.Dataset;

/// <summary>
/// Un turno de una conversación de prueba.
/// </summary>
/// <param name="Pregunta">Lo que escribe el usuario en este turno.</param>
/// <param name="SqlReferencia">
/// La consulta que responde bien, o nulo si el turno debe abstenerse o pedir
/// aclaración.
/// </param>
/// <param name="TerminosProhibidos">
/// Términos que <b>no deben aparecer</b> en la pregunta interpretada de este turno.
/// </param>
/// <remarks>
/// <b>Los términos prohibidos son lo que hace que este eje mida algo.</b> Un diálogo
/// puede dar 100% mientras el sistema arrastra silenciosamente el filtro del turno
/// anterior: si el turno de prueba es autocontenido, el arrastre no cambia el
/// resultado y no se ve.
///
/// Se buscan en la <b>pregunta interpretada</b> y no en la respuesta, porque es la
/// única superficie donde el arrastre es visible antes de convertirse en filas.
/// </remarks>
public sealed record TurnoDeDialogo(
    string Pregunta,
    string? SqlReferencia,
    IReadOnlyList<string> TerminosProhibidos,
    bool EsperaAclaracion);

/// <summary>
/// Una conversación de prueba.
/// </summary>
/// <param name="EsPivoteDuro">
/// Si el diálogo cambia de tema sin ninguna referencia anafórica. Es el caso donde
/// el chequeo negativo muerde: turno uno sobre una entidad, turno dos sobre otra,
/// con los términos del primero prohibidos.
/// </param>
public sealed record DialogoDePrueba(
    string Id,
    string Actor,
    IReadOnlyList<TurnoDeDialogo> Turnos,
    bool EsPivoteDuro);

/// <summary>
/// El eje de diálogo.
/// </summary>
/// <remarks>
/// Es el único eje que ejercita la capa conversacional: el de capacidad manda turnos
/// autocontenidos, así que el reescritor, el reconocedor de aclaraciones y el
/// detector de cambio de tema no están medidos por ningún número.
///
/// <b>Se espera que empiece rojo.</b> Ése es el punto: es la línea de base honesta
/// contra la cual medir las mejoras del reescritor, y registrarla es parte del
/// entregable.
/// </remarks>
public sealed class DatasetDeDialogo
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private DatasetDeDialogo(IReadOnlyList<DialogoDePrueba> dialogos, string huella)
    {
        Dialogos = dialogos;
        Huella = huella;
    }

    /// <summary>Las conversaciones, en el orden del archivo.</summary>
    public IReadOnlyList<DialogoDePrueba> Dialogos { get; }

    /// <summary>Huella estable del archivo, para el sellado.</summary>
    public string Huella { get; }

    /// <summary>Cuántos turnos hay en total.</summary>
    public int Turnos => Dialogos.Sum(dialogo => dialogo.Turnos.Count);

    /// <summary>Carga el dataset desde un archivo.</summary>
    public static DatasetDeDialogo Cargar(string ruta) => Interpretar(File.ReadAllText(ruta));

    /// <summary>Interpreta el dataset desde su texto.</summary>
    public static DatasetDeDialogo Interpretar(string crudo)
    {
        var archivo = JsonSerializer.Deserialize<ArchivoDeDialogo>(crudo, Opciones)
            ?? throw new InvalidOperationException("El dataset de diálogo no se pudo interpretar.");

        if (archivo.Dialogos.Count == 0)
        {
            throw new InvalidOperationException("El dataset de diálogo no tiene ninguna conversación.");
        }

        var dialogos = archivo.Dialogos
            .Select(dialogo => new DialogoDePrueba(
                dialogo.Id,
                dialogo.Actor,
                [.. dialogo.Turnos.Select(turno => new TurnoDeDialogo(
                    turno.Pregunta,
                    turno.SqlReferencia,
                    turno.TerminosProhibidos ?? [],
                    turno.EsperaAclaracion))],
                dialogo.EsPivoteDuro))
            .ToArray();

        foreach (var dialogo in dialogos)
        {
            if (dialogo.Turnos.Count < 2)
            {
                // Un diálogo de un turno es un ítem de capacidad con otro nombre: no
                // ejercita nada de la capa conversacional.
                throw new InvalidOperationException(
                    $"El diálogo '{dialogo.Id}' tiene menos de dos turnos.");
            }

            if (dialogo.EsPivoteDuro && dialogo.Turnos[1].TerminosProhibidos.Count == 0)
            {
                // Un pivote sin términos prohibidos no comprueba el pivote: comprueba
                // que el segundo turno se responde, que es otra cosa.
                throw new InvalidOperationException(
                    $"El diálogo '{dialogo.Id}' está marcado como pivote duro y su segundo turno "
                    + "no declara términos prohibidos, así que no verificaría el pivote.");
            }
        }

        if (!dialogos.Any(dialogo => dialogo.EsPivoteDuro))
        {
            throw new InvalidOperationException(
                "El dataset de diálogo no tiene ningún pivote duro. Sin él, el eje no verifica "
                + "el caso para el que el chequeo negativo existe.");
        }

        var huella = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(crudo)));
        return new DatasetDeDialogo(dialogos, huella);
    }

    private sealed class ArchivoDeDialogo
    {
        [JsonPropertyName("dialogos")]
        public IReadOnlyList<DialogoDeArchivo> Dialogos { get; init; } = [];
    }

    private sealed class DialogoDeArchivo
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("actor")]
        public string Actor { get; init; } = string.Empty;

        [JsonPropertyName("es_pivote_duro")]
        public bool EsPivoteDuro { get; init; }

        [JsonPropertyName("turnos")]
        public IReadOnlyList<TurnoDeArchivo> Turnos { get; init; } = [];
    }

    private sealed class TurnoDeArchivo
    {
        [JsonPropertyName("pregunta")]
        public string Pregunta { get; init; } = string.Empty;

        [JsonPropertyName("sql_referencia")]
        public string? SqlReferencia { get; init; }

        [JsonPropertyName("terminos_prohibidos")]
        public IReadOnlyList<string>? TerminosProhibidos { get; init; }

        [JsonPropertyName("espera_aclaracion")]
        public bool EsperaAclaracion { get; init; }
    }
}
