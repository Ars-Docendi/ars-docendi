using System.Text.Json;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Contracts;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IConsultasDeAuditoriaDeAdministracion"/> sobre
/// <c>asistente.auditoria_administracion</c> (sistema-seccion-unificada,
/// design.md D1, ARS-157).
/// </summary>
/// <remarks>
/// Lee con la conexión del dueño: el schema <c>asistente</c> está revocado por
/// completo a los dos roles de solo lectura (definición §3.4 de
/// asistente-administracion-de-uso), así que incluso leer su propia auditoría
/// de administración exige la conexión dueña — mismo motivo que
/// <c>ConsultasDeUso</c> y <c>PresupuestosAdministrablesReal</c>.
///
/// Parsea acá el JSON de <c>antes</c>/<c>despues</c>: ese formato lo escribe
/// <c>AdministracionAsistenteController</c> y es interno del módulo — nunca
/// cruza a Contracts sin normalizar (design.md D1, alternativa (d) rechazada:
/// devolver el string crudo filtraría el formato privado al Host).
/// </remarks>
internal sealed class ConsultasDeAuditoriaDeAdministracion(CadenaDuena cadena)
    : IConsultasDeAuditoriaDeAdministracion
{
    /// <summary>Cupo de filas por consulta (design.md D1/D3).</summary>
    private const int Cupo = 2000;

    public async Task<LoteDeAuditoriaDeAdministracion> ListarAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        // El rango es INCLUSIVO en las dos puntas (design.md tarea 1.2): >= y <=,
        // no un lado abierto.
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, actor_id, ocurrido_en, accion, antes, despues
              FROM asistente.auditoria_administracion
             WHERE (@desde::timestamptz IS NULL OR ocurrido_en >= @desde)
               AND (@hasta::timestamptz IS NULL OR ocurrido_en <= @hasta)
             ORDER BY ocurrido_en DESC, id DESC
             LIMIT @limite
            """, conexion);
        comando.Parameters.AddWithValue("desde", (object?)desde ?? DBNull.Value);
        comando.Parameters.AddWithValue("hasta", (object?)hasta ?? DBNull.Value);
        // Se pide uno de más para distinguir "hay exactamente el cupo" de "hay
        // más que el cupo" sin un COUNT(*) aparte.
        comando.Parameters.AddWithValue("limite", Cupo + 1);

        var eventos = new List<EventoDeAdministracion>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            eventos.Add(Mapear(
                lector.GetInt64(0),
                lector.GetGuid(1),
                lector.GetFieldValue<DateTimeOffset>(2),
                lector.GetString(3),
                lector.IsDBNull(4) ? null : lector.GetString(4),
                lector.IsDBNull(5) ? null : lector.GetString(5)));
        }

        var truncado = eventos.Count > Cupo;
        if (truncado)
        {
            eventos.RemoveRange(Cupo, eventos.Count - Cupo);
        }

        return new LoteDeAuditoriaDeAdministracion(eventos, truncado);
    }

    /// <summary>
    /// Normaliza el JSON de cada acción a los campos de design.md D1/D5. Un
    /// código de acción que este método no reconoce mapea a sí mismo sin
    /// campos — el Host lo redacta como «Cambio» genérico (design.md D5).
    /// </summary>
    private static EventoDeAdministracion Mapear(
        long id, Guid actorId, DateTimeOffset ocurridoEn, string tipo, string? antes, string? despues)
    {
        using var docAntes = Parsear(antes);
        using var docDespues = Parsear(despues);

        return tipo switch
        {
            "presupuesto.rol" => new EventoDeAdministracion(
                id, actorId, ocurridoEn, tipo,
                Clave: Texto(docDespues, "rol") ?? Texto(docAntes, "rol"),
                UsuarioAfectado: null,
                Campos: [CampoDe("cupo", docAntes, docDespues)]),

            "presupuesto.usuario" => new EventoDeAdministracion(
                id, actorId, ocurridoEn, tipo,
                Clave: null,
                UsuarioAfectado: LeerGuid(docDespues, "actorId") ?? LeerGuid(docAntes, "actorId"),
                Campos: [CampoDe("cupo", docAntes, docDespues)]),

            "tope_organizacional" => new EventoDeAdministracion(
                id, actorId, ocurridoEn, tipo,
                Clave: null,
                UsuarioAfectado: null,
                Campos: [CampoDe("tope_mensual_usd", docAntes, docDespues, "topeMensualUsd")]),

            "mantenimiento.activar" or "mantenimiento.desactivar" => new EventoDeAdministracion(
                id, actorId, ocurridoEn, tipo,
                Clave: null,
                UsuarioAfectado: null,
                Campos:
                [
                    CampoDe("activo", docAntes, docDespues, "Activo"),
                    CampoDe("razon", docAntes, docDespues, "Razon"),
                ]),

            _ => new EventoDeAdministracion(id, actorId, ocurridoEn, tipo, null, null, []),
        };
    }

    private static CampoDeAdministracion CampoDe(
        string campoNormalizado, JsonDocument? antes, JsonDocument? despues, string? claveEnJson = null)
    {
        var clave = claveEnJson ?? campoNormalizado;
        return new CampoDeAdministracion(campoNormalizado, ValorCrudo(antes, clave), ValorCrudo(despues, clave));
    }

    /// <summary>
    /// El valor de una propiedad, como texto, sea cual sea su tipo JSON. Nulo
    /// si el documento no existe, la propiedad falta o vale JSON null.
    /// </summary>
    private static string? ValorCrudo(JsonDocument? documento, string propiedad)
    {
        if (documento is null || !documento.RootElement.TryGetProperty(propiedad, out var valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => valor.GetString(),
            _ => valor.GetRawText(),
        };
    }

    private static string? Texto(JsonDocument? documento, string propiedad) =>
        documento is not null
        && documento.RootElement.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static Guid? LeerGuid(JsonDocument? documento, string propiedad) =>
        Texto(documento, propiedad) is { } texto && Guid.TryParse(texto, out var guid) ? guid : null;

    private static JsonDocument? Parsear(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);
}
