using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Services.Integridad;

/// <summary>
/// Serialización canónica de los sellos [BR-designaciones-019]. Es propia y explícita a
/// propósito: cambiar un nombre, el orden o el formato de un campo invalida toda cadena
/// existente, así que un cambio de formato exige una versión de payload nueva.
/// </summary>
internal static class CanonicoSellos
{
    private const string VersionPayload = "v1";

    /// <summary>Antecesor del primer sello del período. Coincide con el trigger <c>encadenar_sello</c>.</summary>
    public static byte[] Genesis(Guid periodoId) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(periodoId.ToString()));

    /// <summary>PostgreSQL guarda microsegundos; lo hasheado tiene que sobrevivir el ida y vuelta.</summary>
    public static DateTimeOffset AMicrosegundos(DateTimeOffset instante) =>
        new(instante.UtcTicks - instante.UtcTicks % 10, TimeSpan.Zero);

    /// <summary>Estado del pedido que el sello fotografía. Excluye <c>Version</c> (xmin cambia con cada UPDATE).</summary>
    public static byte[] HashEstado(Pedido pedido) => Sha256(escritor =>
    {
        escritor.WriteString("v", VersionPayload);
        escritor.WriteString("periodo_id", pedido.PeriodoId);
        escritor.WriteString("persona_id", pedido.PersonaId);
        escritor.WriteString("materia_id", pedido.MateriaId);
        escritor.WriteString("carrera_id", pedido.CarreraId);
        escritor.WriteString("novedad", pedido.Novedad);
        escritor.WriteString("estado", pedido.Estado);
        escritor.WriteBoolean("prioritario", pedido.Prioritario);
        EscribirGuid(escritor, "cargo_solicitado_id", pedido.CargoSolicitadoId);
        escritor.WriteString("dedicacion_solicitada", pedido.DedicacionSolicitada);
        EscribirGuid(escritor, "dedicacion_solicitada_id", pedido.DedicacionSolicitadaId);
        EscribirEntero(escritor, "horas", pedido.Horas);
        EscribirEntero(escritor, "horas_investigacion", pedido.HorasInvestigacion);
        EscribirEntero(escritor, "horas_externas", pedido.HorasExternas);
        escritor.WriteString("justificacion", pedido.Justificacion);
        escritor.WriteString("tipo_baja", pedido.TipoBaja);
        escritor.WriteString("tipo_baja_detalle", pedido.TipoBajaDetalle);
        escritor.WriteString("etapa_retorno", pedido.EtapaRetorno);
        escritor.WriteString("propietario_actual", pedido.PropietarioActual);
        if (pedido.Snapshot is { } snapshot)
        {
            escritor.WriteStartObject("snapshot");
            escritor.WriteString("cargo", snapshot.Cargo);
            escritor.WriteString("dedicacion", snapshot.Dedicacion);
            EscribirEntero(escritor, "horas", snapshot.Horas);
            escritor.WriteString("materia", snapshot.Materia);
            EscribirEntero(escritor, "horas_investigacion", snapshot.HorasInvestigacion);
            EscribirEntero(escritor, "horas_externas", snapshot.HorasExternas);
            escritor.WriteEndObject();
        }
        else
        {
            escritor.WriteNull("snapshot");
        }
        escritor.WriteStartArray("adjuntos");
        foreach (var adjunto in pedido.Adjuntos.OrderBy(a => a.Id.ToString(), StringComparer.Ordinal))
        {
            escritor.WriteStartObject();
            escritor.WriteString("id", adjunto.Id);
            escritor.WriteString("tipo", adjunto.Tipo);
            escritor.WriteString("nombre", adjunto.Nombre);
            escritor.WriteString("uri", adjunto.Uri);
            EscribirGuid(escritor, "archivo_id", adjunto.ArchivoId);
            escritor.WriteEndObject();
        }
        escritor.WriteEndArray();
    });

    /// <summary>SHA-256(antecesor ‖ payload del evento).</summary>
    public static byte[] Hash(
        byte[] hashAnterior, Guid periodoId, long secuencia, PedidoHistorial evento, byte[] hashEstado)
    {
        var payload = Serializar(escritor =>
        {
            escritor.WriteString("v", VersionPayload);
            escritor.WriteString("periodo_id", periodoId);
            escritor.WriteNumber("secuencia", secuencia);
            escritor.WriteString("historial_id", evento.Id);
            escritor.WriteString("pedido_id", evento.PedidoId);
            escritor.WriteString("accion", evento.Accion);
            escritor.WriteString("etapa", evento.Etapa);
            escritor.WriteString("rol_id", evento.RolId);
            EscribirGuid(escritor, "actor_id", evento.ActorId);
            escritor.WriteString("comentario", evento.Comentario);
            escritor.WriteString("creado_en", evento.CreadoEn.UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
            escritor.WriteString("hash_estado", Convert.ToHexStringLower(hashEstado));
        });
        return SHA256.HashData([.. hashAnterior, .. payload]);
    }

    private static byte[] Sha256(Action<Utf8JsonWriter> escribir) => SHA256.HashData(Serializar(escribir));

    private static byte[] Serializar(Action<Utf8JsonWriter> escribir)
    {
        using var buffer = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(buffer))
        {
            escritor.WriteStartObject();
            escribir(escritor);
            escritor.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static void EscribirGuid(Utf8JsonWriter escritor, string nombre, Guid? valor)
    {
        if (valor is { } guid) escritor.WriteString(nombre, guid);
        else escritor.WriteNull(nombre);
    }

    private static void EscribirEntero(Utf8JsonWriter escritor, string nombre, int? valor)
    {
        if (valor is { } entero) escritor.WriteNumber(nombre, entero);
        else escritor.WriteNull(nombre);
    }
}
