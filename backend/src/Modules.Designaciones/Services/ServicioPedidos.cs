using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Storage.Contracts;
using Microsoft.Extensions.Logging;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services.Integridad;

namespace Modules.Designaciones.Services;

/// <summary>
/// Orquesta el ciclo de vida de los pedidos de designación: resuelve el ámbito del
/// actor, delega los guards a <see cref="MaquinaEstadosPedido"/>, persiste la
/// transición con su evento de historial. Las designaciones vigentes se escriben al
/// sincronizar la conciliación (<see cref="ServicioConciliacion"/>), no acá.
/// </summary>
internal sealed partial class ServicioPedidos(
    RepositorioPedidos pedidos,
    RepositorioDesignaciones designaciones,
    ResolutorActor resolutorActor,
    IConsultasIdentity identity,
    UnidadDeTrabajo unidadDeTrabajo,
    SelladorPedidos sellador,
    ILogger<ServicioPedidos> logger,
    IAdministracionIdentity? administracionIdentity = null,
    IAlmacenamientoArchivos? almacenamiento = null,
    IProteccionArchivos? proteccionArchivos = null)
{
    /// <summary>
    /// Crea un pedido en borrador.
    /// <para>
    /// Valida BR-designaciones-001 antes de escribir para producir el mensaje que pide
    /// la spec, pero la autoridad es el índice único parcial: dos requests simultáneos
    /// pasan los dos esta validación, y el repositorio traduce la violación del índice
    /// al mismo error de dominio.
    /// </para>
    /// </summary>
    private async Task<Pedido> CrearProtegidoAsync(DatosPedido datos, CancellationToken ct)
    {
        Pedido resultado = null!;
        await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
            resultado = await CrearEnTransaccionAsync(datos, token), ct);
        return resultado;
    }

    private async Task<Pedido> CrearEnTransaccionAsync(DatosPedido datos, CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);

        var alcanceCarga = new AlcancePedido(datos.MateriaId, datos.CarreraId);
        if (!MaquinaEstadosPedido.PuedeCargar(alcanceCarga, actor))
        {
            throw new ErrorDominioPedido(
                "Sólo el Jefe de Cátedra de la materia, un Administrativo o Secretaría pueden cargar un pedido [BR-designaciones-009].");
        }

        var archivos = await ValidarDatosAsync(datos, actor, ct);
        var datosResueltos = await ResolverPersonaAsync(datos, ct);
        await designaciones.ValidarDestinosAsync([actor.UsuarioId], [datosResueltos.PersonaId!.Value], ct);

        if (await pedidos.ExisteVivoParaPersonaEnPeriodoAsync(
            datosResueltos.PeriodoId, datosResueltos.PersonaId!.Value, null, ct))
        {
            // Sin datos del pedido bloqueante: puede ser de una cátedra ajena al actor.
            throw new ErrorPedidoDuplicado(
                "Ya existe un pedido en curso para ese docente en el período [BR-designaciones-001].");
        }

        var pedido = new Pedido
        {
            Id = Guid.NewGuid(),
            Numero = await pedidos.SiguienteNumeroAsync(ct),
            PeriodoId = datos.PeriodoId,
            PersonaId = datosResueltos.PersonaId.Value,
            MateriaId = datos.MateriaId,
            CarreraId = datos.CarreraId,
            Novedad = datos.Novedad,
            Estado = EstadosPedido.Borrador,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        AplicarDatos(pedido, datosResueltos);
        foreach (var adjunto in ConstruirAdjuntos(pedido.Id, datosResueltos.Adjuntos, archivos, datosResueltos.Adjuntos.Any(EsLegacyCompat) || almacenamiento is null)) pedido.Adjuntos.Add(adjunto);

        pedidos.Agregar(pedido);
        await RegistrarEnHistorialAsync(pedido, AccionesHistorial.Crear,
            MaquinaEstadosPedido.RolCargador(alcanceCarga, actor), actor, null, ct);

        await pedidos.GuardarCambiosAsync(ct);

        logger.LogInformation(
            "Pedido {Numero} creado en período {PeriodoId} sobre materia {MateriaId}",
            pedido.Numero, datosResueltos.PeriodoId, datosResueltos.MateriaId);

        return pedido;
    }

    private async Task<Pedido> EditarProtegidoAsync(Guid pedidoId, DatosPedido datos, CancellationToken ct)
    {
        if (datos.Version is null)
        {
            throw new ErrorDominioPedido("La edición requiere la versión vigente del pedido.");
        }
        if (datos.Persona is not null)
            throw new ErrorDominioPedido("La edición debe referir a una persona canónica existente.");
        var personaId = datos.PersonaId
            ?? throw new ErrorDominioPedido("La edición debe referir a una persona canónica existente.");
        var actor = await resolutorActor.ResolverAsync(ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct)
            ?? throw new ErrorDominioPedido($"No existe el pedido {pedidoId}.");
        var alcance = new AlcancePedido(pedido.MateriaId, pedido.CarreraId);
        if (!MaquinaEstadosPedido.PuedeEditar(pedido, actor)
            || !MaquinaEstadosPedido.AlcanzaAmbito(alcance, actor))
        {
            throw new ErrorDominioPedido("El actor no puede editar este pedido en su estado o ámbito actual.");
        }
        // Lo que ya está adjunto lo subió quien creó el pedido: quien puede editarlo
        // (Secretaría en la conciliación, por ejemplo) lo conserva sin ser su propietario.
        var adjuntosPrevios = pedido.Adjuntos
            .Where(a => a.ArchivoId is not null).Select(a => a.ArchivoId!.Value).ToHashSet();
        var archivos = await ValidarDatosAsync(datos, actor, ct, adjuntosPrevios);
        if (await pedidos.ExisteVivoParaPersonaEnPeriodoAsync(
            datos.PeriodoId, personaId, pedidoId, ct))
        {
            throw new ErrorPedidoDuplicado(
                "Ya existe un pedido en curso para ese docente en el período [BR-designaciones-001].");
        }

        await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
        {
            await designaciones.ValidarDestinosAsync([actor.UsuarioId],
                pedido.PersonaId == personaId ? [] : [personaId], token);
            await sellador.VerificarEstadoPrevioAsync(pedido, token);
            pedidos.EsperarVersion(pedido, datos.Version.Value);
            var personaPrevia = pedido.PersonaId;
            var materiaPrevia = pedido.MateriaId;
            pedido.PeriodoId = datos.PeriodoId;
            pedido.PersonaId = personaId;
            pedido.MateriaId = datos.MateriaId;
            pedido.CarreraId = datos.CarreraId;
            pedido.Novedad = datos.Novedad;
            AplicarDatos(pedido, datos);
            pedidos.ReemplazarAdjuntos(pedido, ConstruirAdjuntos(pedido.Id, datos.Adjuntos, archivos, datos.Adjuntos.Any(EsLegacyCompat) || almacenamiento is null));
            var enConciliacion = EstadosPedido.EnConciliacion.Contains(pedido.Estado);
            if (enConciliacion)
            {
                // Corregido según la rectificación: debe seguir siendo sincronizable, y
                // un rechazado en la conciliación vuelve a conciliarse.
                await designaciones.BloquearAsync(pedido.PersonaId, pedido.MateriaId, token);
                await ValidarCompletitudCircuitoAsync(pedido, token);
                await ValidarDesignacionVigenteAsync(pedido, token);
                // La fotografía de "lo que tenía" sólo se rehace si cambió de quién o de
                // qué cátedra es el pedido: la del primer envío ya no lo describiría.
                if (pedido.Snapshot is null || personaPrevia != personaId || materiaPrevia != datos.MateriaId)
                    pedido.Snapshot = await ArmarSnapshotAsync(pedido, token);
                pedido.Estado = EstadosPedido.EnLote;
            }
            var rol = enConciliacion
                ? MaquinaEstadosPedido.RolActuantePorPermiso(actor)
                : pedido.Estado == EstadosPedido.Borrador
                    || (pedido.PropietarioActual is { } propietario && !actor.Tiene(propietario))
                    ? MaquinaEstadosPedido.RolCargador(alcance, actor)
                    : pedido.PropietarioActual ?? RolesCircuito.JefeCatedra;
            await RegistrarEnHistorialAsync(pedido, AccionesHistorial.Editar, rol, actor, null, token);
            await pedidos.GuardarCambiosAsync(token);
        }, ct);

        return pedido;
    }

    /// <summary>
    /// Aplica una transición de estado. Es el único camino por el que un pedido cambia
    /// de estado: los guards viven en la máquina, no acá.
    /// </summary>
    public async Task<Pedido> AplicarAccionAsync(Guid pedidoId, AccionPedido accion, CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);

        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct)
            ?? throw new ErrorDominioPedido($"No existe el pedido {pedidoId}.");

        var alcance = new AlcancePedido(pedido.MateriaId, pedido.CarreraId);
        var transicion = MaquinaEstadosPedido.AplicarAccion(pedido, alcance, accion, actor);

        await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
        {
            await designaciones.ValidarDestinosAsync([actor.UsuarioId], [pedido.PersonaId], token);
            await designaciones.BloquearAsync(pedido.PersonaId, pedido.MateriaId, token);
            // Después del lock de designaciones, mismo orden que el sellado.
            await sellador.VerificarEstadoPrevioAsync(pedido, token);
            if (accion is AccionPedido.Enviar or AccionPedido.Reenviar or AccionPedido.Aceptar)
            {
                await ValidarCompletitudCircuitoAsync(pedido, token);
                await ValidarDesignacionVigenteAsync(pedido, token);
            }
            // La fotografía se obtiene después de esperar administración y antes de commit.
            // Reenviar/aprobar nunca recalculan la verdad histórica del primer envío.
            if (transicion.AccionHistorial == AccionesHistorial.Enviar)
                pedido.Snapshot = await ArmarSnapshotAsync(pedido, token);
            AplicarTransicion(pedido, transicion);
            // Aceptar en Decanato deja el pedido en lote sin tocar las designaciones
            // vigentes: se escriben recién al sincronizar la conciliación.
            await RegistrarEnHistorialAsync(pedido, transicion.AccionHistorial, transicion.CodigoRolActuante, actor, transicion.Comentario, token);

            await pedidos.GuardarCambiosAsync(token);
        }, ct);

        logger.LogInformation(
            "Pedido {Numero}: {Accion} por rol {Rol} → estado {Estado}",
            pedido.Numero, transicion.AccionHistorial, transicion.CodigoRolActuante, pedido.Estado);

        return pedido;
    }

    /// <summary>
    /// Elimina definitivamente un borrador propio. A diferencia de las transiciones,
    /// eliminar no deja evento en el historial: el pedido deja de existir. Un devuelto
    /// no se puede eliminar aunque sea editable — ya tiene una revisión asociada.
    /// </summary>
    private async Task EliminarBorradorProtegidoAsync(Guid pedidoId, CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);

        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct)
            ?? throw new ErrorDominioPedido($"No existe el pedido {pedidoId}.");

        if (!MaquinaEstadosPedido.PuedeEliminar(pedido, actor)
            || !MaquinaEstadosPedido.PuedeCargar(new AlcancePedido(pedido.MateriaId, pedido.CarreraId), actor))
        {
            throw new ErrorDominioPedido(
                "Sólo el Jefe de Cátedra de la materia, un Administrativo o Secretaría pueden eliminar un pedido en borrador.");
        }

        pedidos.Eliminar(pedido);
        await pedidos.GuardarCambiosAsync(ct);

        logger.LogInformation("Pedido {Numero} eliminado en borrador", pedido.Numero);
    }

    /// <summary>
    /// Pedidos visibles para el actor dentro del período, acotados por su ámbito
    /// [BR-designaciones-009].
    /// </summary>
    public async Task<IReadOnlyList<Pedido>> ListarPorAmbitoAsync(Guid periodoId, CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);

        if (actor.EsDeptoWide)
        {
            return await pedidos.ListarDelPeriodoAsync(periodoId, ct);
        }

        var resultado = new List<Pedido>();
        if (actor.Tiene(RolesCircuito.CoordinadorCarrera) && actor.CarrerasACargo.Count > 0)
        {
            resultado.AddRange(await pedidos.ListarPorCarrerasAsync(
                periodoId, [.. actor.CarrerasACargo], ct));
        }

        if (actor.Tiene(RolesCircuito.JefeCatedra) && actor.MateriasACargo.Count > 0)
        {
            resultado.AddRange(await pedidos.ListarPorMateriasAsync(
                periodoId, [.. actor.MateriasACargo], ct));
        }

        return resultado.DistinctBy(p => p.Id)
            .OrderByDescending(p => p.Prioritario)
            .ThenByDescending(p => p.CreadoEn)
            .ToArray();
    }

    private static void AplicarTransicion(Pedido pedido, TransicionPedido transicion)
    {
        pedido.Estado = transicion.EstadoResultante;
        pedido.EtapaRetorno = transicion.EtapaRetorno;
        pedido.PropietarioActual = transicion.PropietarioActual;

        if (transicion.Prioritario is { } prioritario)
        {
            pedido.Prioritario = prioritario;
        }
    }

    /// <summary>
    /// Agrega el evento al historial con el rol CONCRETO con el que se actuó. No se
    /// infiere del usuario: un usuario puede tener varios roles, y cuál autorizó la
    /// acción lo decide la máquina de estados. El evento se sella en la misma transacción
    /// [BR-designaciones-019], así que se registra con el pedido ya en su estado posterior.
    /// </summary>
    internal async Task RegistrarEnHistorialAsync(
        Pedido pedido, string accion, string codigoRol, ActorContexto actor, string? comentario,
        CancellationToken ct)
    {
        var historial = new PedidoHistorial
        {
            Id = Guid.NewGuid(),
            PedidoId = pedido.Id,
            Accion = accion,
            RolId = ResolverRolId(codigoRol),
            ActorId = actor.UsuarioId,
            Etapa = pedido.Estado,
            Comentario = comentario,
            CreadoEn = CanonicoSellos.AMicrosegundos(DateTimeOffset.UtcNow),
        };
        pedido.Historial.Add(historial);
        pedidos.AgregarHistorial(historial);
        await sellador.SellarAsync(pedido, historial, ct);
    }

    // Los roles de sistema tienen UUID fijo en el seed (002_identity_roles.sql), así
    // que el mapeo code → id no requiere ir a la base en cada transición.
    private static Guid ResolverRolId(string codigoRol) => codigoRol switch
    {
        RolesCircuito.JefeCatedra => new Guid("a1000000-0000-4000-8000-000000000002"),
        RolesCircuito.CoordinadorCarrera => new Guid("a1000000-0000-4000-8000-000000000003"),
        RolesCircuito.Secretaria => new Guid("a1000000-0000-4000-8000-000000000004"),
        RolesCircuito.Decanato => new Guid("a1000000-0000-4000-8000-000000000005"),
        RolesCircuito.Administrativo => new Guid("a1000000-0000-4000-8000-000000000006"),
        _ => throw new ErrorDominioPedido($"Rol de circuito no reconocido: \"{codigoRol}\"."),
    };

    /// <summary>
    /// Fotografía lo vigente del docente en la cátedra del pedido. Puede quedar en
    /// blanco: un Alta refiere a alguien que todavía no tiene designación.
    /// </summary>
    private async Task<SnapshotPedido> ArmarSnapshotAsync(Pedido pedido, CancellationToken ct)
    {
        var vigente = await designaciones.ObtenerVigenteAsync(pedido.PersonaId, pedido.MateriaId, ct);
        var materia = (await identity.ListarMateriasAsync(ct)).Single(m => m.Id == pedido.MateriaId);

        return new SnapshotPedido(
            Cargo: vigente?.Cargo?.Nombre,
            Dedicacion: vigente?.DedicacionCatalogo?.Nombre,
            Horas: vigente?.Horas,
            Materia: materia.Nombre,
            HorasInvestigacion: vigente?.HorasInvestigacion,
            HorasExternas: vigente?.HorasExternas,
            SectorExterno: vigente?.SectorExterno?.Codigo);
    }

    private async Task<IReadOnlyDictionary<Guid, ArchivoDto>> ValidarDatosAsync(
        DatosPedido datos, ActorContexto actor, CancellationToken ct,
        IReadOnlySet<Guid>? adjuntosPrevios = null)
    {
        if (!Novedades.Admitidas.Contains(datos.Novedad))
            throw new ErrorDominioPedido($"La novedad no está admitida para nuevos pedidos: \"{datos.Novedad}\".");
        var periodo = await pedidos.ObtenerPeriodoActivoAsync(ct);
        if (periodo is null || periodo.Id != datos.PeriodoId)
            throw new ErrorDominioPedido("El pedido debe pertenecer al período activo.");
        if (datos.Persona is not null && datos.Novedad != Novedades.Alta)
            throw new ErrorDominioPedido("Sólo un alta puede recibir los datos de una persona nueva.");
        if (datos.Persona is not null
            && (string.IsNullOrWhiteSpace(datos.Persona.Documento)
                || string.IsNullOrWhiteSpace(datos.Persona.Nombre)
                || string.IsNullOrWhiteSpace(datos.Persona.Apellido)))
            throw new ErrorDominioPedido("El alta exige documento, nombre y apellido.");
        if (datos.Persona is not null && datos.PersonaId is not null)
            throw new ErrorDominioPedido("El alta no puede enviar una persona nueva y una referencia canónica a la vez.");
        if (datos.Persona is null && datos.PersonaId is null)
            throw new ErrorDominioPedido("El pedido debe referir a una persona canónica.");
        var persona = datos.PersonaId is { } personaId
            ? (await identity.ListarPersonasAsync(ct)).SingleOrDefault(p => p.Id == personaId)
            : null;
        if (datos.PersonaId is not null && persona is null)
            throw new ErrorDominioPedido("La persona seleccionada no existe.");
        // La pertenencia materia–plan es sólo informativa acá: confirma que la materia
        // efectivamente se dicta en la carrera elegida, sin fijar un plan puntual.
        var seDictaEnLaCarrera = (await identity.ListarMateriasPlanAsync(ct)).Any(mp =>
            mp.MateriaId == datos.MateriaId && mp.Plan!.CarreraId == datos.CarreraId
            && mp.Activo && mp.Materia!.Activo && mp.Plan.Activo);
        if (!seDictaEnLaCarrera)
            throw new ErrorDominioPedido(
                "La materia seleccionada no existe, está inactiva o no se dicta en esa carrera.");
        if (!actor.EsDeptoWide
            && !(actor.Tiene(RolesCircuito.CoordinadorCarrera) && actor.CarrerasACargo.Contains(datos.CarreraId))
            && !(actor.Tiene(RolesCircuito.JefeCatedra) && actor.MateriasACargo.Contains(datos.MateriaId)))
            throw new ErrorDominioPedido("La materia seleccionada está fuera del ámbito del actor.");
        if (datos.SectorExternoId is { } sectorId)
        {
            if (datos.Novedad is not (Novedades.Alta or Novedades.CambioDeCargoODedicacion))
                throw new ErrorDominioPedido("Sólo un Alta o un Cambio puede ser de un docente externo.");
            if (datos.CargoSolicitadoId is not null || datos.DedicacionSolicitadaId is not null)
                throw new ErrorDominioPedido("Un pedido de docente externo no lleva cargo ni dedicación.");
            if (!await pedidos.ExisteSectorExternoActivoAsync(sectorId, ct))
                throw new ErrorDominioPedido("El sector externo no existe o está inactivo.");
        }
        if (datos.CargoSolicitadoId is { } cargoId
            && !await pedidos.ExisteCargoActivoAsync(cargoId, ct))
            throw new ErrorDominioPedido("El cargo seleccionado no existe o está inactivo.");
        if (datos.Horas is <= 0 || datos.HorasInvestigacion is < 0 || datos.HorasExternas is < 0)
            throw new ErrorDominioPedido("Las cargas horarias no pueden ser negativas y las horas del pedido deben ser positivas.");
        if (datos.TipoBaja is not null && !TiposBaja.Todos.Contains(datos.TipoBaja))
            throw new ErrorDominioPedido("El tipo de baja no es válido.");
        if (datos.TipoBaja == TiposBaja.Otro && string.IsNullOrWhiteSpace(datos.TipoBajaDetalle))
            throw new ErrorDominioPedido("El tipo de baja Otro exige un detalle.");
        if ((datos.Novedad is Novedades.Alta or Novedades.CambioDeCargoODedicacion
                && datos.SectorExternoId is null && datos.DedicacionSolicitadaId is null)
            || (datos.DedicacionSolicitadaId is { } dedicacionId
                && !await pedidos.ExisteDedicacionActivaAsync(dedicacionId, ct)))
            throw new ErrorDominioPedido("La dedicación solicitada no es válida.");
        if (datos.Adjuntos.Any(a => !EsTipoAdjuntoValido(a.Tipo)
            || (a.ArchivoId is null && almacenamiento is not null && !EsLegacyCompat(a))
            || (almacenamiento is null && string.IsNullOrWhiteSpace(a.Nombre))))
            throw new ErrorDominioPedido("Uno de los adjuntos no referencia un archivo confirmado.");

        var tiposAdjuntos = datos.Adjuntos.Select(a => a.Tipo).ToHashSet();
        if (datos.Novedad == Novedades.Alta
            && !new[] { TiposAdjunto.Cv, TiposAdjunto.DniFrente, TiposAdjunto.DniDorso }
                .All(tiposAdjuntos.Contains))
            throw new ErrorDominioPedido(
                "El alta exige adjuntar CV, DNI frente y DNI dorso [BR-designaciones-002].");
        if (datos.Novedad == Novedades.Baja
            && (!tiposAdjuntos.Contains(TiposAdjunto.Justificativo)
                || string.IsNullOrWhiteSpace(datos.TipoBaja)))
            throw new ErrorDominioPedido(
                "La baja exige tipo y adjunto justificativo [BR-designaciones-003].");
        if (datos.Novedad == Novedades.CambioDeCargoODedicacion
            && string.IsNullOrWhiteSpace(datos.Justificacion))
            throw new ErrorDominioPedido(
                "El cambio de cargo o dedicación exige justificación [BR-designaciones-004].");
        if ((datos.Novedad is Novedades.Baja or Novedades.CambioDeCargoODedicacion)
            && (persona is null || string.IsNullOrWhiteSpace(persona.Legajo)))
            throw new ErrorDominioPedido(
                "La baja y el cambio exigen un docente con legajo [BR-designaciones-018].");
        if (datos.Novedad is Novedades.Baja or Novedades.CambioDeCargoODedicacion
            && (persona is null
                || await designaciones.ObtenerVigenteAsync(persona.Id, datos.MateriaId, ct) is null))
            throw new ErrorDominioPedido(
                "La persona no tiene una designación vigente en la materia seleccionada.");

        return await ValidarAdjuntosAsync(
            datos.Adjuntos, actor.UsuarioId, adjuntosPrevios ?? new HashSet<Guid>(), ct);
    }

    /// <summary>
    /// Un archivo nuevo tiene que ser del actor; uno que el pedido ya tenía adjunto sólo tiene
    /// que seguir disponible para el mismo propósito.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, ArchivoDto>> ValidarAdjuntosAsync(
        IReadOnlyList<Modules.Designaciones.Api.GuardarAdjuntoPedidoDto> adjuntos,
        Guid propietarioId,
        IReadOnlySet<Guid> adjuntosPrevios,
        CancellationToken ct)
    {
        if (almacenamiento is null) return new Dictionary<Guid, ArchivoDto>();
        var archivos = new Dictionary<Guid, ArchivoDto>();
        foreach (var adjunto in adjuntos)
        {
            if (adjunto.ArchivoId is not { } archivoId)
            {
                if (!EsLegacyCompat(adjunto)) throw new ErrorDominioPedido("El adjunto no fue validado por almacenamiento.");
                continue;
            }
            var proposito = adjunto.Tipo switch
            {
                TiposAdjunto.Cv => PropositosArchivo.Cv,
                TiposAdjunto.DniFrente => PropositosArchivo.DniFrente,
                TiposAdjunto.DniDorso => PropositosArchivo.DniDorso,
                TiposAdjunto.Justificativo => PropositosArchivo.Justificativo,
                _ => string.Empty,
            };
            if (adjuntosPrevios.Contains(archivoId))
            {
                var previo = await almacenamiento.ObtenerAsync(archivoId, ct);
                if (previo is null || previo.Estado != EstadosArchivo.Disponible || previo.Proposito != proposito)
                    throw new ErrorDominioPedido("El archivo no está disponible para ese propósito.");
                archivos[archivoId] = previo;
                continue;
            }
            try
            {
                archivos[archivoId] = await almacenamiento.RequerirDisponibleDePropietarioAsync(
                    archivoId, proposito, propietarioId, ct);
            }
            catch (ExcepcionAplicacion ex)
            {
                throw new ErrorDominioPedido(ex.Message);
            }
        }
        return archivos;
    }

    private async Task<DatosPedido> ResolverPersonaAsync(DatosPedido datos, CancellationToken ct)
    {
        if (datos.Persona is null)
        {
            if (datos.PersonaId is null)
                throw new ErrorDominioPedido("El pedido debe referir a una persona canónica.");
            return datos;
        }

        if (administracionIdentity is null)
            throw new InvalidOperationException("No está registrada la administración de identidad.");
        var personaId = await administracionIdentity.CrearPersonaSinCuentaAsync(
            new DatosPersonaSinCuenta(datos.Persona.Documento, datos.Persona.Nombre, datos.Persona.Apellido), ct);
        return datos with { PersonaId = personaId, Persona = null };
    }

    private async Task ValidarDesignacionVigenteAsync(Pedido pedido, CancellationToken ct)
    {
        if (pedido.Novedad is Novedades.Baja or Novedades.CambioDeCargoODedicacion
            && await designaciones.ObtenerVigenteAsync(pedido.PersonaId, pedido.MateriaId, ct) is null)
            throw new ErrorDominioPedido(
                "La persona ya no tiene una designación vigente en la materia seleccionada.");
    }

    private static void AplicarDatos(Pedido pedido, DatosPedido datos)
    {
        pedido.CargoSolicitadoId = datos.CargoSolicitadoId;
        pedido.DedicacionSolicitadaId = datos.DedicacionSolicitadaId;
        pedido.SectorExternoId = datos.SectorExternoId;
        pedido.Horas = datos.Horas;
        pedido.HorasInvestigacion = datos.HorasInvestigacion;
        pedido.HorasExternas = datos.HorasExternas;
        pedido.Justificacion = Normalizar(datos.Justificacion);
        pedido.TipoBaja = Normalizar(datos.TipoBaja);
        pedido.TipoBajaDetalle = Normalizar(datos.TipoBajaDetalle);
    }

    private static IReadOnlyList<PedidoAdjunto> ConstruirAdjuntos(
        Guid pedidoId,
        IReadOnlyList<Modules.Designaciones.Api.GuardarAdjuntoPedidoDto> datos,
        IReadOnlyDictionary<Guid, ArchivoDto> archivos,
        bool aceptarLegacy) =>
        datos.Select(a =>
        {
            if (a.ArchivoId is not { } archivoId)
            {
                if (!aceptarLegacy || !EsLegacyCompat(a))
                    throw new ErrorDominioPedido("El adjunto no fue validado por almacenamiento.");
                return new PedidoAdjunto
                {
                    Id = Guid.NewGuid(),
                    PedidoId = pedidoId,
                    Tipo = a.Tipo,
                    Nombre = a.Nombre!.Trim(),
                    Uri = string.IsNullOrWhiteSpace(a.Uri) ? null : a.Uri!.Trim(),
                    CreadoEn = DateTimeOffset.UtcNow,
                };
            }
            if (!archivos.TryGetValue(archivoId, out var archivo))
                throw new ErrorDominioPedido("El adjunto no fue validado por almacenamiento.");
            return new PedidoAdjunto
            {
                Id = Guid.NewGuid(),
                PedidoId = pedidoId,
                Tipo = a.Tipo,
                Nombre = archivo.NombreOriginal,
                Uri = null,
                ArchivoId = archivoId,
                CreadoEn = DateTimeOffset.UtcNow,
            };
        }).ToArray();

    private static bool EsTipoAdjuntoValido(string tipo) => tipo is
        TiposAdjunto.Cv or TiposAdjunto.DniFrente or TiposAdjunto.DniDorso or TiposAdjunto.Justificativo;

    private static bool EsLegacyCompat(Modules.Designaciones.Api.GuardarAdjuntoPedidoDto adjunto) =>
        adjunto.ArchivoId is null
        && !string.IsNullOrWhiteSpace(adjunto.Nombre)
        && (string.IsNullOrWhiteSpace(adjunto.Uri) || adjunto.Uri.StartsWith("synthetic://", StringComparison.Ordinal));

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
