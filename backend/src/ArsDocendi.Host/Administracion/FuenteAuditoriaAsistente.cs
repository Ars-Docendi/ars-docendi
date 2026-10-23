using Modules.Asistente.Contracts;

namespace ArsDocendi.Host.Administracion;

/// <summary>Filtros que <see cref="FuenteAuditoriaAsistente"/> aplica EN MEMORIA (design.md D2/D4).</summary>
public sealed record FiltrosAsistente(string? Accion, string? Tabla, string? RowPk, string? QNormalizado);

/// <summary>
/// La fuente «asistente» del feed unificado de auditoría: llama al contrato,
/// resuelve nombres, mapea cada <c>Tipo</c> por la tabla de design.md D5 y
/// aplica la misma máscara y los mismos filtros en memoria que D2/D4 piden
/// (sistema-seccion-unificada, ARS-157, tarea 2.4).
/// </summary>
public sealed class FuenteAuditoriaAsistente(
    IConsultasDeAuditoriaDeAdministracion contrato, IRepositorioAuditoria repositorio)
{
    public async Task<(IReadOnlyList<EventoAuditoriaDto> Eventos, bool Truncado)> ListarAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, FiltrosAsistente filtros, CancellationToken ct)
    {
        var lote = await contrato.ListarAsync(desde, hasta, ct);

        var idsAResolver = lote.Eventos
            .SelectMany(e => new Guid?[] { e.ActorId, e.UsuarioAfectado })
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var nombres = await repositorio.ResolverNombresAsync(idsAResolver, ct);

        var eventos = lote.Eventos
            .Select(e => Mapear(e, nombres))
            .Where(evt => Coincide(evt, filtros))
            .ToArray();

        return (eventos, lote.Truncado);
    }

    /// <summary>Mapeo puro: sin I/O, para poder testearlo sin base (tarea 2.4).</summary>
    public static EventoAuditoriaDto Mapear(
        EventoDeAdministracion evento, IReadOnlyDictionary<Guid, NombreResuelto> nombres)
    {
        var campos = evento.Campos.Select(MapeadorEventoAuditoria.MapearCampoDeAdministracion).ToArray();
        var nombreAfectado = evento.UsuarioAfectado is { } afectado && nombres.TryGetValue(afectado, out var n)
            ? MapeadorEventoAuditoria.NombreNaturalDePersona(n)
            : null;
        var (objeto, tabla, rowPk, accionEtiqueta, resumen) = DatosDelTipo(evento, nombreAfectado, campos);

        var (actor, tipoActor) = nombres.TryGetValue(evento.ActorId, out var nombreActor)
            && MapeadorEventoAuditoria.NombreNaturalDePersona(nombreActor) is { } nombreResuelto
                ? (nombreResuelto, MapeadorEventoAuditoria.TipoActorPersona)
                : ("Actor no identificado", MapeadorEventoAuditoria.TipoActorNoIdentificado);

        return new EventoAuditoriaDto(
            $"{FusionDeFuentesAuditoria.OrigenAsistente}-{evento.Id}",
            "asistente",
            tabla,
            rowPk,
            accionEtiqueta == "Alta" ? "INSERT" : "UPDATE",
            evento.OcurridoEn,
            evento.ActorId,
            null,
            [.. campos.Select(c => c.Campo)],
            campos,
            actor,
            accionEtiqueta,
            "Asistente",
            objeto,
            resumen,
            tipoActor,
            FusionDeFuentesAuditoria.OrigenAsistente);
    }

    /// <summary>La tabla de mapeo de design.md D5, por <c>Tipo</c>.</summary>
    private static (string Objeto, string Tabla, string RowPk, string AccionEtiqueta, string Resumen) DatosDelTipo(
        EventoDeAdministracion evento, string? nombreAfectado, IReadOnlyList<CambioAuditoriaDto> campos)
    {
        string Antes(string campo) => campos.FirstOrDefault(c => c.Campo == campo)?.ValorAnterior ?? "—";
        string Despues(string campo) => campos.FirstOrDefault(c => c.Campo == campo)?.ValorNuevo ?? "—";
        bool AntesEsNulo(string campo) => campos.FirstOrDefault(c => c.Campo == campo)?.ValorAnterior is null;

        switch (evento.Tipo)
        {
            case "presupuesto.rol":
                {
                    var objeto = $"Cupo diario del rol {evento.Clave ?? "—"}";
                    return (objeto, "presupuesto_rol", evento.Clave ?? "—", "Cambio",
                        $"{objeto}: {Antes("cupo")} → {Despues("cupo")}");
                }

            case "presupuesto.usuario":
                {
                    var nombre = nombreAfectado ?? "usuario no identificado";
                    var objeto = $"Cupo diario de {nombre}";
                    var clave = evento.UsuarioAfectado?.ToString() ?? "—";
                    var esAlta = AntesEsNulo("cupo");
                    var resumen = esAlta
                        ? $"{objeto}: {Despues("cupo")}"
                        : $"{objeto}: {Antes("cupo")} → {Despues("cupo")}";
                    return (objeto, "presupuesto_usuario", clave, esAlta ? "Alta" : "Cambio", resumen);
                }

            case "presupuesto.usuario.restablecer":
                {
                    var nombre = nombreAfectado ?? "usuario no identificado";
                    var objeto = $"Cupo diario de {nombre}";
                    return (objeto, "presupuesto_usuario", evento.UsuarioAfectado?.ToString() ?? "—", "Cambio",
                        $"{objeto}: {Antes("cupo")} → del rol");
                }

            case "acceso.rol":
                {
                    var objeto = $"Acceso al asistente del rol {evento.Clave ?? "—"}";
                    return (objeto, "presupuesto_rol", evento.Clave ?? "—", "Cambio",
                        $"{objeto}: {(Despues("acceso_habilitado") == "Sí" ? "habilitado" : "quitado")}");
                }

            case "acceso.usuario":
                {
                    var nombre = nombreAfectado ?? "usuario no identificado";
                    var objeto = $"Acceso al asistente de {nombre}";
                    return (objeto, "acceso_usuario_revocado", evento.UsuarioAfectado?.ToString() ?? "—", "Cambio",
                        $"{objeto}: {(Despues("acceso_revocado") == "Sí" ? "quitado" : "restablecido al del rol")}");
                }

            case "tope_organizacional":
                {
                    const string objeto = "Tope mensual organizacional";
                    return (objeto, "tope_organizacional", "—", "Cambio",
                        $"{objeto}: {Antes("tope_mensual_usd")} → {Despues("tope_mensual_usd")} USD");
                }

            case "mantenimiento.activar":
                return ("Mantenimiento del asistente", "modo_mantenimiento", "—", "Cambio",
                    "Mantenimiento del asistente activado");

            case "mantenimiento.desactivar":
                return ("Mantenimiento del asistente", "modo_mantenimiento", "—", "Cambio",
                    "Mantenimiento del asistente desactivado");

            default:
                // Un Tipo desconocido (futuro código de acción todavía no
                // mapeado acá) es «Cambio» genérico y humanizado, sin campos
                // ni valores — nunca falla ni oculta el evento (design.md D5).
                var objetoDesconocido = EtiquetasAuditoria.Humanizar(evento.Tipo);
                return (objetoDesconocido, evento.Tipo, "—", "Cambio",
                    $"Cambio de {objetoDesconocido.ToLowerInvariant()}");
        }
    }

    private static bool Coincide(EventoAuditoriaDto evento, FiltrosAsistente filtros)
    {
        if (filtros.Accion is { Length: > 0 } accion && !evento.Accion.Equals(accion, StringComparison.Ordinal))
            return false;

        if (filtros.Tabla is { Length: > 0 } tabla)
        {
            var tablaNormalizada = tabla.StartsWith("asistente.", StringComparison.OrdinalIgnoreCase)
                ? tabla["asistente.".Length..]
                : tabla;
            if (!evento.Tabla.Equals(tablaNormalizada, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (filtros.RowPk is { Length: > 0 } rowPk && !evento.RowPk.Equals(rowPk, StringComparison.Ordinal))
            return false;

        if (filtros.QNormalizado is { Length: > 0 } q)
        {
            var textoBuscable = EtiquetasAuditoria.Normalizar(string.Join(' ',
                evento.Actor, evento.Modulo, evento.Objeto, evento.Tabla, evento.RowPk,
                string.Join(' ', evento.Cambios.Select(c => c.EtiquetaCampo))));
            if (!textoBuscable.Contains(q, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
