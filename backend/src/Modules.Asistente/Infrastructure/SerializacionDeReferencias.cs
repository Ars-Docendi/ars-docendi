using System.Text.Json;
using System.Text.Json.Serialization;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// (De)serializa <c>asistente.turno_historico.referencias</c>: marcador →
/// tipo e id (design.md D11 de asistente-rediseno-v3).
/// </summary>
/// <remarks>
/// Un solo lugar para el formato del JSON, usado por
/// <see cref="RegistroDeHistorial"/> (escribe) y <see cref="ConsultasDeHistorial"/>
/// (lee) — para que los dos lados de la misma columna no puedan divergir en
/// silencio.
/// </remarks>
internal static class SerializacionDeReferencias
{
    private sealed class Entrada
    {
        [JsonPropertyName("tipo")]
        public string Tipo { get; init; } = string.Empty;

        [JsonPropertyName("id")]
        public Guid Id { get; init; }
    }

    /// <summary><c>null</c> si no hay ninguna referencia: la columna se deja <c>NULL</c>.</summary>
    public static string? Serializar(
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referencias)
    {
        if (referencias is null || referencias.Count == 0)
        {
            return null;
        }

        var mapa = referencias.ToDictionary(
            par => par.Key,
            par => new Entrada { Tipo = NombreDe(par.Value.Tipo), Id = par.Value.Id },
            StringComparer.Ordinal);

        return JsonSerializer.Serialize(mapa);
    }

    /// <summary><c>null</c> para una columna <c>NULL</c> o vacía.</summary>
    public static IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? Deserializar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var mapa = JsonSerializer.Deserialize<Dictionary<string, Entrada>>(json);
        if (mapa is null || mapa.Count == 0)
        {
            return null;
        }

        return mapa.ToDictionary(
            par => par.Key,
            par => (TipoDe(par.Value.Tipo), par.Value.Id),
            StringComparer.Ordinal);
    }

    private static string NombreDe(TipoDeMencion tipo) => tipo switch
    {
        TipoDeMencion.Materia => "materia",
        _ => "docente",
    };

    private static TipoDeMencion TipoDe(string nombre) => nombre switch
    {
        "materia" => TipoDeMencion.Materia,
        _ => TipoDeMencion.Docente,
    };
}
