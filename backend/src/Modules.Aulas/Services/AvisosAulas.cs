using System.Globalization;
using ArsDocendi.Shared.Identity;
using Modules.Aulas.Domain;
using Modules.Notificaciones.Contracts;

namespace Modules.Aulas.Services;

/// <summary>
/// Text snapshots of the notifications the Aulas module emits. The wording is part of the product
/// (RF-022: the notice of an assigned room carries the room), so it lives in one place.
/// </summary>
internal static class AvisosAulas
{
    public static AvisoNuevo AulaAsignada(SolicitudReservaAula s, string materia, string aula) => Armar(
        TiposEventoNotificacion.AulaAsignada, s, $"Aula asignada: {aula}",
        $"Se te asignó {aula} para tu solicitud del {FechaCorta(s.Dia)} ({materia}, {s.Comision}).",
        new() { ["dia"] = Iso(s.Dia), ["materia"] = materia, ["comision"] = s.Comision, ["aula"] = aula });

    public static AvisoNuevo AulaActualizada(SolicitudReservaAula s, string materia, string anterior, string nueva) => Armar(
        TiposEventoNotificacion.AulaActualizada, s, $"Aula actualizada: {nueva}",
        $"El aula de tu solicitud del {FechaCorta(s.Dia)} ({materia}, {s.Comision}) cambió de {anterior} a {nueva}.",
        new()
        {
            ["dia"] = Iso(s.Dia), ["materia"] = materia, ["comision"] = s.Comision,
            ["aulaAnterior"] = anterior, ["aula"] = nueva,
        });

    public static AvisoNuevo SolicitudRechazada(SolicitudReservaAula s, string materia, string motivo) => Armar(
        TiposEventoNotificacion.SolicitudRechazada, s, "Solicitud de aula rechazada",
        $"Se rechazó tu solicitud del {FechaCorta(s.Dia)} ({materia}, {s.Comision}). Motivo: {motivo}",
        new() { ["dia"] = Iso(s.Dia), ["materia"] = materia, ["comision"] = s.Comision, ["motivo"] = motivo });

    public static AvisoNuevo ReservaCancelada(SolicitudReservaAula s, string materia, string? aula, string motivo) => Armar(
        TiposEventoNotificacion.ReservaCancelada, s, "Reserva de aula cancelada",
        $"Se canceló tu reserva del {FechaCorta(s.Dia)} ({materia}, {s.Comision})"
            + (aula is null ? "" : $"; {aula} quedó liberada") + $". Motivo: {motivo}",
        new()
        {
            ["dia"] = Iso(s.Dia), ["materia"] = materia, ["comision"] = s.Comision,
            ["aulaLiberada"] = aula, ["motivo"] = motivo,
        });

    /// <summary>Notice to Administrativo and Secretaría: <paramref name="rolDestino"/> is the recipient role.</summary>
    public static AvisoNuevo SolicitudCanceladaPorDocente(
        SolicitudReservaAula s, string materia, Persona? docente, string rolDestino) => new(
        TiposEventoNotificacion.SolicitudCanceladaDocente, TiposEntidadNotificacion.SolicitudAula, s.Id, rolDestino,
        "Solicitud de aula cancelada",
        (docente is null ? "El docente" : $"{docente.Apellido}, {docente.Nombre}")
            + $" canceló su solicitud del {FechaCorta(s.Dia)} ({materia}, {s.Comision}). "
            + "Si ya pediste un aula a Secretaría Académica, hay que liberarla.",
        new Dictionary<string, object?> { ["dia"] = Iso(s.Dia), ["materia"] = materia, ["comision"] = s.Comision });

    /// <summary>Notice addressed to the owner teacher, under the profile she created the request with.</summary>
    private static AvisoNuevo Armar(
        string tipoEvento, SolicitudReservaAula s, string titulo, string mensaje, Dictionary<string, object?> contexto) =>
        new(tipoEvento, TiposEntidadNotificacion.SolicitudAula, s.Id, s.RolSolicitante, titulo, mensaje, contexto);

    private static string FechaCorta(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
