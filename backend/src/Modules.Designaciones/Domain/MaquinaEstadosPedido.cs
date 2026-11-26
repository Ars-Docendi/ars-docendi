using ArsDocendi.Shared.Auth;

namespace Modules.Designaciones.Domain;

/// <summary>
/// Máquina de estados de los pedidos de designación — LÓGICA PURA.
/// <para>
/// Dada <c>(pedido, carrera del pedido, acción, actor)</c> valida los guards y
/// devuelve una <see cref="TransicionPedido"/>, o lanza <see cref="ErrorDominioPedido"/>.
/// No consulta la base, no muta el pedido y no escribe historial: eso lo hace el
/// servicio. Por eso se puede testear entera sin PostgreSQL.
/// </para>
/// <para>
/// Es la autoridad del circuito. La máquina equivalente del frontend
/// (<c>api/maquinaEstados.ts</c>) adelanta las mismas reglas para no ofrecer
/// acciones inválidas en la UI, pero no sustituye a esta.
/// </para>
/// </summary>
public static class MaquinaEstadosPedido
{
    /// <summary>
    /// Avance de la cadena al aceptar. Después de <c>en_lote</c> no se acepta: el pedido
    /// sale del circuito de revisión y se concilia.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> EtapaSiguiente =
        new Dictionary<string, string>
        {
            [EstadosPedido.EnRevisionCoordinador] = EstadosPedido.EnRevisionSecretaria,
            [EstadosPedido.EnRevisionSecretaria] = EstadosPedido.EnRevisionDecanato,
            [EstadosPedido.EnRevisionDecanato] = EstadosPedido.EnLote,
        };

    /// <summary>
    /// Quién corrige cuando se devuelve desde cada etapa: la devolución retrocede
    /// exactamente un nivel [BR-designaciones-014].
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> PropietarioDeDevolucion =
        new Dictionary<string, string>
        {
            [EstadosPedido.EnRevisionCoordinador] = RolesCircuito.JefeCatedra,
            [EstadosPedido.EnRevisionSecretaria] = RolesCircuito.CoordinadorCarrera,
            [EstadosPedido.EnRevisionDecanato] = RolesCircuito.Secretaria,
        };

    /// <summary>
    /// Valida los guards de la acción y devuelve la transición resultante.
    /// </summary>
    /// <param name="alcance">
    /// Cátedra y carrera de la pertenencia materia–plan del pedido, resueltas por el
    /// servicio desde identity. El guard del Coordinador usa la carrera del plan; el del
    /// Jefe de Cátedra, la materia canónica.
    /// </param>
    /// <exception cref="ErrorDominioPedido">Si algún guard rechaza la acción.</exception>
    public static TransicionPedido AplicarAccion(
        Pedido pedido,
        AlcancePedido alcance,
        AccionPedido accion,
        ActorContexto actor)
    {
        // Idempotencia terminal: ninguna acción procede sobre un estado terminal
        // [BR-designaciones-011].
        if (pedido.EsTerminal)
        {
            throw new ErrorDominioPedido(
                $"El pedido está en un estado terminal (\"{pedido.Estado}\"): no admite ninguna acción.");
        }

        // En conciliación sólo se sincroniza o se rechaza; el circuito de revisión
        // (enviar, aceptar, devolver, priorizar…) ya terminó.
        if (EstadosPedido.EnConciliacion.Contains(pedido.Estado)
            && accion is not (AccionPedido.Sincronizar or AccionPedido.NoAprobar))
        {
            throw new ErrorDominioPedido(
                $"El pedido está en la conciliación del lote (\"{pedido.Estado}\"): sólo se puede sincronizar, rechazar o editar con permiso.");
        }

        if (pedido.Novedad == Novedades.SinNovedad
            && accion is AccionPedido.Enviar or AccionPedido.Reenviar or AccionPedido.Aceptar)
        {
            throw new ErrorDominioPedido(
                "Los pedidos legados Sin novedad deben editarse con una novedad admitida antes de continuar.");
        }

        return accion switch
        {
            AccionPedido.Enviar => Enviar(pedido, alcance, actor),
            AccionPedido.Cancelar => Cancelar(pedido, alcance, actor),
            AccionPedido.Aceptar a => Aceptar(pedido, alcance, actor, a.Comentario),
            AccionPedido.Rechazar a => Rechazar(pedido, alcance, actor, a.Justificativo),
            AccionPedido.Devolver a => Devolver(pedido, alcance, actor, a.Comentario),
            AccionPedido.Reenviar => Reenviar(pedido, alcance, actor),
            AccionPedido.Priorizar a => Priorizar(pedido, alcance, actor, a.Motivo, true),
            AccionPedido.Despriorizar a => Priorizar(pedido, alcance, actor, a.Comentario, false),
            AccionPedido.Sincronizar => Sincronizar(pedido, alcance, actor),
            AccionPedido.NoAprobar a => NoAprobar(pedido, alcance, actor, a.Motivo),
            _ => throw new ErrorDominioPedido($"Acción no soportada: {accion.GetType().Name}"),
        };
    }

    /// <summary>
    /// ¿El actor alcanza el ámbito del pedido? [BR-designaciones-009]
    /// Coordinador sólo su carrera; Secretaría, Decanato y Administrativo todo el
    /// departamento; Jefe de Cátedra su cátedra.
    /// </summary>
    public static bool AlcanzaAmbito(AlcancePedido alcance, ActorContexto actor)
    {
        ArgumentNullException.ThrowIfNull(alcance);
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.EsDeptoWide)
        {
            return true;
        }

        if (actor.Tiene(RolesCircuito.CoordinadorCarrera)
            && actor.CarrerasACargo.Contains(alcance.CarreraId))
        {
            return true;
        }

        return actor.Tiene(RolesCircuito.JefeCatedra)
            && actor.MateriasACargo.Contains(alcance.MateriaId);
    }

    /// <summary>¿El actor puede ejecutar acciones de revisión sobre este pedido?</summary>
    public static bool PuedeRevisar(Pedido pedido, AlcancePedido alcance, ActorContexto actor)
    {
        if (!EsEtapaDeRevision(pedido.Estado) || !AlcanzaAmbito(alcance, actor))
        {
            return false;
        }

        return actor.Tiene(RolesCircuito.RolPorEtapa[pedido.Estado])
            || actor.Tiene(RolesCircuito.Administrativo);
    }

    /// <summary>
    /// Administrativo y Secretaría cargan pedidos de cualquier materia del Departamento, además
    /// del Jefe de Cátedra sobre las suyas.
    /// </summary>
    public static bool EsCargadorDepartamental(ActorContexto actor) =>
        actor.Tiene(RolesCircuito.Administrativo) || actor.Tiene(RolesCircuito.Secretaria);

    /// <summary>¿El actor puede cargar (crear, enviar, cancelar) un pedido sobre esta materia?</summary>
    public static bool PuedeCargar(AlcancePedido alcance, ActorContexto actor) =>
        EsCargadorDepartamental(actor)
        || (actor.Tiene(RolesCircuito.JefeCatedra) && actor.MateriasACargo.Contains(alcance.MateriaId));

    /// <summary>Rol con el que actúa quien carga: Jefe si la materia es suya; si no, Secretaría o Administrativo.</summary>
    public static string RolCargador(AlcancePedido alcance, ActorContexto actor) =>
        actor.Tiene(RolesCircuito.JefeCatedra) && actor.MateriasACargo.Contains(alcance.MateriaId)
            ? RolesCircuito.JefeCatedra
            : actor.Tiene(RolesCircuito.Secretaria) ? RolesCircuito.Secretaria : RolesCircuito.Administrativo;

    /// <summary>
    /// ¿El actor puede editar? Un borrador, quien puede cargar pedidos; un devuelto, su
    /// propietario actual, y si volvió al primer nivel también Administrativo o Secretaría
    /// [BR-designaciones-008]. Además, con <c>designaciones.editar</c>, un pedido en lote o
    /// rechazado en la conciliación que todavía no se escribió sobre las designaciones. El
    /// ámbito lo valida <see cref="AlcanzaAmbito"/>.
    /// </summary>
    public static bool PuedeEditar(Pedido pedido, ActorContexto actor)
    {
        if (pedido.EsTerminal)
        {
            return false;
        }

        if (EstadosPedido.EnConciliacion.Contains(pedido.Estado))
        {
            // Un pedido heredado del circuito anterior ya se materializó al aceptar
            // Decanato: editarlo dejaría al pedido distinto de lo que se escribió.
            return pedido.MaterializadoEn is null
                && actor.TienePermiso(Permisos.DesignacionesEditar);
        }

        var esBorradorDeCargador =
            pedido.Estado == EstadosPedido.Borrador
            && (actor.Tiene(RolesCircuito.JefeCatedra) || EsCargadorDepartamental(actor));

        return esBorradorDeCargador
            || (pedido.Estado == EstadosPedido.Devuelto && EsPropietarioDelDevuelto(pedido, actor));
    }

    /// <summary>Propietario del devuelto; el primer nivel (Jefe) también lo corrigen Administrativo y Secretaría.</summary>
    private static bool EsPropietarioDelDevuelto(Pedido pedido, ActorContexto actor) =>
        pedido.PropietarioActual is not null
        && (actor.Tiene(pedido.PropietarioActual)
            || (pedido.PropietarioActual == RolesCircuito.JefeCatedra && EsCargadorDepartamental(actor)));

    /// <summary>
    /// ¿Puede eliminarlo? Sólo un borrador, y sólo quien puede cargar pedidos. Un devuelto NO,
    /// aunque también sea editable: ya tiene una revisión en su historial.
    /// </summary>
    public static bool PuedeEliminar(Pedido pedido, ActorContexto actor) =>
        pedido.Estado == EstadosPedido.Borrador
        && (actor.Tiene(RolesCircuito.JefeCatedra) || EsCargadorDepartamental(actor));

    /// <summary>
    /// ¿El actor puede conciliar este pedido? Está en lote, el actor tiene
    /// <c>designaciones.conciliar</c> y alcanza su ámbito.
    /// </summary>
    public static bool PuedeConciliar(Pedido pedido, AlcancePedido alcance, ActorContexto actor) =>
        pedido.Estado == EstadosPedido.EnLote
        && actor.TienePermiso(Permisos.DesignacionesConciliar)
        && AlcanzaAmbito(alcance, actor);

    /// <summary>
    /// Con qué rol se registra en el historial una acción que se autoriza por permiso
    /// (conciliar o editar después del circuito): el rol de circuito de mayor alcance
    /// que tenga el actor, porque el historial registra un rol, no un permiso.
    /// </summary>
    public static string RolActuantePorPermiso(ActorContexto actor)
    {
        string[] enOrdenDeAlcance =
        [
            RolesCircuito.Secretaria,
            RolesCircuito.Administrativo,
            RolesCircuito.Decanato,
            RolesCircuito.CoordinadorCarrera,
            RolesCircuito.JefeCatedra,
        ];

        return Array.Find(enOrdenDeAlcance, actor.Tiene)
            ?? throw new ErrorDominioPedido(
                "El actor no tiene ningún rol de sistema que participe del circuito.");
    }

    private static TransicionPedido Sincronizar(
        Pedido pedido, AlcancePedido alcance, ActorContexto actor)
    {
        GuardarConciliador(pedido, alcance, actor);

        return new TransicionPedido(
            EstadosPedido.Aprobado,
            AccionesHistorial.Sincronizar,
            RolActuantePorPermiso(actor));
    }

    private static TransicionPedido NoAprobar(
        Pedido pedido, AlcancePedido alcance, ActorContexto actor, string motivo)
    {
        GuardarConciliador(pedido, alcance, actor);
        var texto = RequerirTexto(motivo, "El rechazo en la conciliación exige un motivo obligatorio.");

        if (pedido.MaterializadoEn is not null)
        {
            throw new ErrorDominioPedido(
                "El pedido ya se escribió sobre las designaciones al aceptarlo Decanato: no se puede rechazar en la conciliación.");
        }

        return new TransicionPedido(
            EstadosPedido.NoAprobado,
            AccionesHistorial.NoAprobar,
            RolActuantePorPermiso(actor),
            Comentario: texto);
    }

    private static void GuardarConciliador(Pedido pedido, AlcancePedido alcance, ActorContexto actor)
    {
        if (pedido.Estado != EstadosPedido.EnLote)
        {
            throw new ErrorDominioPedido(
                $"Sólo se concilia un pedido en lote (estado actual: \"{pedido.Estado}\").");
        }

        if (!actor.TienePermiso(Permisos.DesignacionesConciliar))
        {
            throw new ErrorDominioPedido("Conciliar el lote requiere el permiso designaciones.conciliar.");
        }

        if (!AlcanzaAmbito(alcance, actor))
        {
            throw new ErrorDominioPedido("El pedido está fuera del ámbito del actor [BR-designaciones-009].");
        }
    }

    private static bool EsEtapaDeRevision(string estado) =>
        RolesCircuito.RolPorEtapa.ContainsKey(estado);

    private static TransicionPedido Enviar(Pedido pedido, AlcancePedido alcance, ActorContexto actor)
    {
        if (pedido.Estado != EstadosPedido.Borrador)
        {
            throw new ErrorDominioPedido(
                $"Sólo se puede enviar a revisión un pedido en borrador (estado actual: \"{pedido.Estado}\").");
        }

        if (!PuedeCargar(alcance, actor))
        {
            throw new ErrorDominioPedido(
                "Sólo el Jefe de Cátedra de la materia, un Administrativo o Secretaría pueden enviar el pedido [BR-designaciones-009].");
        }

        return new TransicionPedido(
            EstadosPedido.EnRevisionCoordinador,
            AccionesHistorial.Enviar,
            RolCargador(alcance, actor));
    }

    private static TransicionPedido Cancelar(
        Pedido pedido, AlcancePedido alcance, ActorContexto actor)
    {
        if (pedido.Estado != EstadosPedido.Borrador)
        {
            throw new ErrorDominioPedido(
                $"Sólo se puede cancelar un pedido en borrador (estado actual: \"{pedido.Estado}\").");
        }

        if (!PuedeCargar(alcance, actor))
        {
            throw new ErrorDominioPedido(
                "Sólo el Jefe de Cátedra de la materia, un Administrativo o Secretaría pueden cancelar el pedido [BR-designaciones-009].");
        }

        return new TransicionPedido(
            EstadosPedido.Cancelado,
            AccionesHistorial.Cancelar,
            RolCargador(alcance, actor));
    }

    /// <summary>
    /// Guard común de las acciones de revisión: el pedido está en una etapa de
    /// revisión, el actor alcanza el ámbito [BR-designaciones-009] y es el revisor de
    /// la etapa actual, o Administrativo [BR-designaciones-013]. Devuelve el código
    /// del rol con el que actúa.
    /// </summary>
    private static string GuardarRevisor(Pedido pedido, AlcancePedido alcance, ActorContexto actor)
    {
        if (!EsEtapaDeRevision(pedido.Estado))
        {
            throw new ErrorDominioPedido(
                $"La acción de revisión requiere un pedido en revisión (estado actual: \"{pedido.Estado}\").");
        }

        if (!AlcanzaAmbito(alcance, actor))
        {
            throw new ErrorDominioPedido("El pedido está fuera del ámbito del actor [BR-designaciones-009].");
        }

        var rolEsperado = RolesCircuito.RolPorEtapa[pedido.Estado];

        // El revisor de la etapa tiene precedencia: si el actor acumula ese rol y
        // además Administrativo, actúa con el que corresponde a la etapa.
        if (actor.Tiene(rolEsperado))
        {
            return rolEsperado;
        }

        if (actor.Tiene(RolesCircuito.Administrativo))
        {
            return RolesCircuito.Administrativo;
        }

        throw new ErrorDominioPedido(
            $"Sólo el revisor de la etapa actual ({rolEsperado}) o Administrativo puede actuar [BR-designaciones-013].");
    }

    private static string RequerirTexto(string? texto, string mensaje) =>
        string.IsNullOrWhiteSpace(texto) ? throw new ErrorDominioPedido(mensaje) : texto;

    private static TransicionPedido Aceptar(
        Pedido pedido, AlcancePedido alcance, ActorContexto actor, string? comentario)
    {
        var rol = GuardarRevisor(pedido, alcance, actor);

        if (rol == RolesCircuito.Administrativo)
        {
            throw new ErrorDominioPedido(
                "Administrativo revisa pero no aprueba pedidos [BR-designaciones-015].");
        }

        return new TransicionPedido(
            EtapaSiguiente[pedido.Estado],
            AccionesHistorial.Aceptar,
            rol,
            Comentario: comentario);
    }

    private static TransicionPedido Rechazar(
        Pedido pedido, AlcancePedido alcance, ActorContexto actor, string justificativo)
    {
        var rol = GuardarRevisor(pedido, alcance, actor);
        var texto = RequerirTexto(
            justificativo, "El rechazo exige un justificativo obligatorio [BR-designaciones-005].");

        return new TransicionPedido(
            EstadosPedido.Rechazado,
            AccionesHistorial.Rechazar,
            rol,
            Comentario: texto);
    }

    private static TransicionPedido Devolver(
        Pedido pedido, AlcancePedido alcance, ActorContexto actor, string comentario)
    {
        var rol = GuardarRevisor(pedido, alcance, actor);
        var texto = RequerirTexto(
            comentario, "La devolución exige un comentario obligatorio [BR-designaciones-005].");

        return new TransicionPedido(
            EstadosPedido.Devuelto,
            AccionesHistorial.Devolver,
            rol,
            // Al reenviar, el pedido retoma exactamente la etapa que lo devolvió:
            // no reinicia la cadena [BR-designaciones-014].
            EtapaRetorno: pedido.Estado,
            PropietarioActual: PropietarioDeDevolucion[pedido.Estado],
            Comentario: texto);
    }

    private static TransicionPedido Reenviar(
        Pedido pedido, AlcancePedido alcance, ActorContexto actor)
    {
        if (pedido.Estado != EstadosPedido.Devuelto)
        {
            throw new ErrorDominioPedido(
                $"Sólo se puede reenviar un pedido devuelto (estado actual: \"{pedido.Estado}\").");
        }

        if (!EsPropietarioDelDevuelto(pedido, actor))
        {
            throw new ErrorDominioPedido(
                "Sólo el propietario del pedido devuelto puede reenviarlo [BR-designaciones-014].");
        }

        if (!AlcanzaAmbito(alcance, actor))
        {
            throw new ErrorDominioPedido("El pedido está fuera del ámbito del actor [BR-designaciones-009].");
        }

        if (pedido.EtapaRetorno is null)
        {
            throw new ErrorDominioPedido("El pedido devuelto no tiene etapa de retorno.");
        }

        return new TransicionPedido(
            pedido.EtapaRetorno,
            AccionesHistorial.Reenviar,
            actor.Tiene(pedido.PropietarioActual!) ? pedido.PropietarioActual! : RolCargador(alcance, actor),
            EtapaRetorno: null,
            PropietarioActual: null);
    }

    /// <summary>
    /// Marca o desmarca la prioridad sin cambiar el estado [BR-designaciones-017].
    /// Priorizar exige justificativo; despriorizar no: bajar la urgencia no requiere
    /// la misma fundamentación que subirla.
    /// </summary>
    private static TransicionPedido Priorizar(
        Pedido pedido, AlcancePedido alcance, ActorContexto actor, string? comentario, bool prioritario)
    {
        if (!AlcanzaAmbito(alcance, actor))
        {
            throw new ErrorDominioPedido("El pedido está fuera del ámbito del actor [BR-designaciones-009].");
        }

        var texto = prioritario
            ? RequerirTexto(comentario, "Marcar prioritario exige un justificativo obligatorio [BR-designaciones-017].")
            : comentario;

        return new TransicionPedido(
            pedido.Estado,
            prioritario ? AccionesHistorial.Priorizar : AccionesHistorial.Despriorizar,
            RolActuanteParaPrioridad(actor),
            Prioritario: prioritario,
            Comentario: texto);
    }

    /// <summary>
    /// Con qué rol se registra una acción de prioridad. Cualquier actor dentro de su
    /// ámbito puede marcarla, así que se toma el rol de circuito más específico que
    /// tenga, empezando por el que le da el ámbito más acotado.
    /// </summary>
    private static string RolActuanteParaPrioridad(ActorContexto actor)
    {
        string[] enOrdenDeEspecificidad =
        [
            RolesCircuito.JefeCatedra,
            RolesCircuito.CoordinadorCarrera,
            RolesCircuito.Secretaria,
            RolesCircuito.Decanato,
            RolesCircuito.Administrativo,
        ];

        return Array.Find(enOrdenDeEspecificidad, actor.Tiene)
            ?? throw new ErrorDominioPedido(
                "El actor no tiene ningún rol de sistema que participe del circuito.");
    }
}
