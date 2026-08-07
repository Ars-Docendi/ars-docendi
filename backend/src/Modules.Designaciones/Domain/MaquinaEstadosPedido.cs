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
    /// <summary>Avance de la cadena al aceptar. <c>en_lote</c> es terminal para el alcance actual.</summary>
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
    /// <param name="carreraDelPedido">
    /// Derivada de <c>identity.materias.carrera_id</c> por el servicio. El pedido no la
    /// desnormaliza, pero el guard de ámbito del Coordinador la necesita.
    /// </param>
    /// <exception cref="ErrorDominioPedido">Si algún guard rechaza la acción.</exception>
    public static TransicionPedido AplicarAccion(
        Pedido pedido,
        Guid carreraDelPedido,
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

        return accion switch
        {
            AccionPedido.Enviar => Enviar(pedido, actor),
            AccionPedido.Cancelar => Cancelar(pedido, actor),
            AccionPedido.Aceptar a => Aceptar(pedido, carreraDelPedido, actor, a.Comentario),
            AccionPedido.Rechazar a => Rechazar(pedido, carreraDelPedido, actor, a.Justificativo),
            AccionPedido.Devolver a => Devolver(pedido, carreraDelPedido, actor, a.Comentario),
            AccionPedido.Reenviar => Reenviar(pedido, actor),
            AccionPedido.Priorizar a => Priorizar(pedido, carreraDelPedido, actor, a.Motivo, true),
            AccionPedido.Despriorizar a => Priorizar(pedido, carreraDelPedido, actor, a.Comentario, false),
            _ => throw new ErrorDominioPedido($"Acción no soportada: {accion.GetType().Name}"),
        };
    }

    /// <summary>
    /// ¿El actor alcanza el ámbito del pedido? [BR-designaciones-009]
    /// Coordinador sólo su carrera; Secretaría, Decanato y Administrativo todo el
    /// departamento; Jefe de Cátedra su cátedra.
    /// </summary>
    public static bool AlcanzaAmbito(Pedido pedido, Guid carreraDelPedido, ActorContexto actor)
    {
        if (actor.EsDeptoWide)
        {
            return true;
        }

        if (actor.Tiene(RolesCircuito.CoordinadorCarrera)
            && actor.CarrerasACargo.Contains(carreraDelPedido))
        {
            return true;
        }

        return actor.Tiene(RolesCircuito.JefeCatedra)
            && actor.MateriasACargo.Contains(pedido.MateriaId);
    }

    /// <summary>¿El actor puede ejecutar acciones de revisión sobre este pedido?</summary>
    public static bool PuedeRevisar(Pedido pedido, Guid carreraDelPedido, ActorContexto actor)
    {
        if (!EsEtapaDeRevision(pedido.Estado) || !AlcanzaAmbito(pedido, carreraDelPedido, actor))
        {
            return false;
        }

        return actor.Tiene(RolesCircuito.RolPorEtapa[pedido.Estado])
            || actor.Tiene(RolesCircuito.Administrativo);
    }

    /// <summary>¿Puede avanzar la cadena? Administrativo revisa pero nunca aprueba [BR-designaciones-015].</summary>
    public static bool PuedeAceptar(Pedido pedido, Guid carreraDelPedido, ActorContexto actor) =>
        PuedeRevisar(pedido, carreraDelPedido, actor)
        && actor.Tiene(RolesCircuito.RolPorEtapa[pedido.Estado]);

    /// <summary>
    /// ¿El actor puede editar? Sólo un borrador siendo Jefe de Cátedra, o un devuelto
    /// del que es propietario actual [BR-designaciones-008].
    /// </summary>
    public static bool PuedeEditar(Pedido pedido, ActorContexto actor)
    {
        if (pedido.EsTerminal)
        {
            return false;
        }

        var esBorradorDelJefe =
            pedido.Estado == EstadosPedido.Borrador && actor.Tiene(RolesCircuito.JefeCatedra);
        var esDevueltoDelPropietario =
            pedido.Estado == EstadosPedido.Devuelto
            && pedido.PropietarioActual is not null
            && actor.Tiene(pedido.PropietarioActual);

        return esBorradorDelJefe || esDevueltoDelPropietario;
    }

    /// <summary>
    /// ¿Puede eliminarlo? Sólo un borrador propio del Jefe de Cátedra. Un devuelto NO,
    /// aunque también sea editable: ya tiene una revisión en su historial.
    /// </summary>
    public static bool PuedeEliminar(Pedido pedido, ActorContexto actor) =>
        pedido.Estado == EstadosPedido.Borrador && actor.Tiene(RolesCircuito.JefeCatedra);

    private static bool EsEtapaDeRevision(string estado) =>
        RolesCircuito.RolPorEtapa.ContainsKey(estado);

    private static TransicionPedido Enviar(Pedido pedido, ActorContexto actor)
    {
        if (pedido.Estado != EstadosPedido.Borrador)
        {
            throw new ErrorDominioPedido(
                $"Sólo se puede enviar a revisión un pedido en borrador (estado actual: \"{pedido.Estado}\").");
        }

        if (!actor.Tiene(RolesCircuito.JefeCatedra))
        {
            throw new ErrorDominioPedido("Sólo el Jefe de Cátedra puede enviar el pedido a revisión.");
        }

        if (!actor.MateriasACargo.Contains(pedido.MateriaId))
        {
            throw new ErrorDominioPedido(
                "El pedido pertenece a una cátedra que el actor no tiene a cargo [BR-designaciones-009].");
        }

        return new TransicionPedido(
            EstadosPedido.EnRevisionCoordinador,
            AccionesHistorial.Enviar,
            RolesCircuito.JefeCatedra);
    }

    private static TransicionPedido Cancelar(Pedido pedido, ActorContexto actor)
    {
        if (pedido.Estado != EstadosPedido.Borrador)
        {
            throw new ErrorDominioPedido(
                $"Sólo se puede cancelar un pedido en borrador (estado actual: \"{pedido.Estado}\").");
        }

        if (!actor.Tiene(RolesCircuito.JefeCatedra))
        {
            throw new ErrorDominioPedido("Sólo el Jefe de Cátedra puede cancelar el pedido.");
        }

        return new TransicionPedido(
            EstadosPedido.Cancelado,
            AccionesHistorial.Cancelar,
            RolesCircuito.JefeCatedra);
    }

    /// <summary>
    /// Guard común de las acciones de revisión: el pedido está en una etapa de
    /// revisión, el actor alcanza el ámbito [BR-designaciones-009] y es el revisor de
    /// la etapa actual, o Administrativo [BR-designaciones-013]. Devuelve el código
    /// del rol con el que actúa.
    /// </summary>
    private static string GuardarRevisor(Pedido pedido, Guid carreraDelPedido, ActorContexto actor)
    {
        if (!EsEtapaDeRevision(pedido.Estado))
        {
            throw new ErrorDominioPedido(
                $"La acción de revisión requiere un pedido en revisión (estado actual: \"{pedido.Estado}\").");
        }

        if (!AlcanzaAmbito(pedido, carreraDelPedido, actor))
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
        Pedido pedido, Guid carreraDelPedido, ActorContexto actor, string? comentario)
    {
        var rol = GuardarRevisor(pedido, carreraDelPedido, actor);

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
        Pedido pedido, Guid carreraDelPedido, ActorContexto actor, string justificativo)
    {
        var rol = GuardarRevisor(pedido, carreraDelPedido, actor);
        var texto = RequerirTexto(
            justificativo, "El rechazo exige un justificativo obligatorio [BR-designaciones-005].");

        return new TransicionPedido(
            EstadosPedido.Rechazado,
            AccionesHistorial.Rechazar,
            rol,
            Comentario: texto);
    }

    private static TransicionPedido Devolver(
        Pedido pedido, Guid carreraDelPedido, ActorContexto actor, string comentario)
    {
        var rol = GuardarRevisor(pedido, carreraDelPedido, actor);
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

    private static TransicionPedido Reenviar(Pedido pedido, ActorContexto actor)
    {
        if (pedido.Estado != EstadosPedido.Devuelto)
        {
            throw new ErrorDominioPedido(
                $"Sólo se puede reenviar un pedido devuelto (estado actual: \"{pedido.Estado}\").");
        }

        if (pedido.PropietarioActual is null || !actor.Tiene(pedido.PropietarioActual))
        {
            throw new ErrorDominioPedido(
                "Sólo el propietario del pedido devuelto puede reenviarlo [BR-designaciones-014].");
        }

        if (pedido.EtapaRetorno is null)
        {
            throw new ErrorDominioPedido("El pedido devuelto no tiene etapa de retorno.");
        }

        return new TransicionPedido(
            pedido.EtapaRetorno,
            AccionesHistorial.Reenviar,
            pedido.PropietarioActual,
            EtapaRetorno: null,
            PropietarioActual: null);
    }

    /// <summary>
    /// Marca o desmarca la prioridad sin cambiar el estado [BR-designaciones-017].
    /// Priorizar exige justificativo; despriorizar no: bajar la urgencia no requiere
    /// la misma fundamentación que subirla.
    /// </summary>
    private static TransicionPedido Priorizar(
        Pedido pedido, Guid carreraDelPedido, ActorContexto actor, string? comentario, bool prioritario)
    {
        if (!AlcanzaAmbito(pedido, carreraDelPedido, actor))
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
