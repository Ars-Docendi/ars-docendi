using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.Extensions.Logging;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

/// <summary>
/// Orquesta el ciclo de vida de los pedidos de designación: resuelve el ámbito del
/// actor, delega los guards a <see cref="MaquinaEstadosPedido"/>, persiste la
/// transición con su evento de historial y dispara la materialización cuando el
/// pedido termina de recorrer el circuito.
/// </summary>
internal sealed class ServicioPedidos(
    RepositorioPedidos pedidos,
    RepositorioDesignaciones designaciones,
    MaterializadorDesignaciones materializador,
    ResolutorActor resolutorActor,
    IConsultasIdentity identity,
    UnidadDeTrabajo unidadDeTrabajo,
    ILogger<ServicioPedidos> logger,
    IAdministracionIdentity? administracionIdentity = null)
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
    public async Task<Pedido> CrearAsync(DatosPedido datos, CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);

        if (!actor.Tiene(RolesCircuito.JefeCatedra) || !actor.MateriasACargo.Contains(datos.MateriaId))
        {
            throw new ErrorDominioPedido(
                "Sólo el Jefe de Cátedra de la materia puede cargar un pedido sobre esa cátedra [BR-designaciones-009].");
        }

        await ValidarDatosAsync(datos, actor, ct);
        var datosResueltos = await ResolverPersonaAsync(datos, ct);

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
            Novedad = datos.Novedad,
            Estado = EstadosPedido.Borrador,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        AplicarDatos(pedido, datosResueltos);
        foreach (var adjunto in ConstruirAdjuntos(pedido.Id, datosResueltos.Adjuntos)) pedido.Adjuntos.Add(adjunto);

        pedidos.Agregar(pedido);
        RegistrarEnHistorial(pedido, AccionesHistorial.Crear, RolesCircuito.JefeCatedra, actor, null);

        await pedidos.GuardarCambiosAsync(ct);

        logger.LogInformation(
            "Pedido {Numero} creado en período {PeriodoId} sobre materia {MateriaId}",
            pedido.Numero, datosResueltos.PeriodoId, datosResueltos.MateriaId);

        return pedido;
    }

    public async Task<Pedido> EditarAsync(Guid pedidoId, DatosPedido datos, CancellationToken ct)
    {
        if (datos.Version is null)
        {
            throw new ErrorDominioPedido("La edición requiere la versión vigente del pedido.");
        }
        if (datos.PersonaId is null || datos.Persona is not null)
            throw new ErrorDominioPedido("La edición debe referir a una persona canónica existente.");
        var actor = await resolutorActor.ResolverAsync(ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct)
            ?? throw new ErrorDominioPedido($"No existe el pedido {pedidoId}.");
        var carrera = await pedidos.ObtenerCarreraDelPedidoAsync(pedidoId, ct);
        if (!MaquinaEstadosPedido.PuedeEditar(pedido, actor)
            || !MaquinaEstadosPedido.AlcanzaAmbito(pedido, carrera, actor))
        {
            throw new ErrorDominioPedido("El actor no puede editar este pedido en su estado o ámbito actual.");
        }
        await ValidarDatosAsync(datos, actor, ct);
        if (await pedidos.ExisteVivoParaPersonaEnPeriodoAsync(
            datos.PeriodoId, datos.PersonaId.Value, pedidoId, ct))
        {
            throw new ErrorPedidoDuplicado(
                "Ya existe un pedido en curso para ese docente en el período [BR-designaciones-001].");
        }

        await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
        {
            pedidos.EsperarVersion(pedido, datos.Version.Value);
            pedido.PeriodoId = datos.PeriodoId;
            pedido.PersonaId = datos.PersonaId.Value;
            pedido.MateriaId = datos.MateriaId;
            pedido.Novedad = datos.Novedad;
            AplicarDatos(pedido, datos);
            pedidos.ReemplazarAdjuntos(pedido, ConstruirAdjuntos(pedido.Id, datos.Adjuntos));
            var rol = pedido.Estado == EstadosPedido.Borrador
                ? RolesCircuito.JefeCatedra
                : pedido.PropietarioActual ?? RolesCircuito.JefeCatedra;
            RegistrarEnHistorial(pedido, AccionesHistorial.Editar, rol, actor, null);
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

        var pedido = await pedidos.ObtenerLivianoAsync(pedidoId, ct)
            ?? throw new ErrorDominioPedido($"No existe el pedido {pedidoId}.");

        var carrera = await pedidos.ObtenerCarreraDelPedidoAsync(pedidoId, ct);
        var transicion = MaquinaEstadosPedido.AplicarAccion(pedido, carrera, accion, actor);

        // El snapshot se congela AL ENVIAR, no al crear: mientras el pedido está en
        // borrador vale el estado vigente del docente. Una vez enviado, el trámite
        // conserva su verdad histórica aunque la designación cambie después.
        if (transicion.AccionHistorial == AccionesHistorial.Enviar)
        {
            await ValidarDesignacionVigenteAsync(pedido, ct);
            pedido.Snapshot = await ArmarSnapshotAsync(pedido, ct);
        }

        await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
        {
            AplicarTransicion(pedido, transicion);
            RegistrarEnHistorial(
                pedido, transicion.AccionHistorial, transicion.CodigoRolActuante, actor, transicion.Comentario);

            // El circuito terminó: el trámite se vuelve realidad sobre el estado
            // vigente. Va dentro de la misma transacción que la transición para que un
            // fallo parcial no deje un pedido aprobado sin designación.
            if (transicion.EstadoResultante == EstadosPedido.EnLote)
            {
                await materializador.MaterializarAsync(pedido, token);
            }

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
    public async Task EliminarBorradorAsync(Guid pedidoId, CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);

        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct)
            ?? throw new ErrorDominioPedido($"No existe el pedido {pedidoId}.");

        if (!MaquinaEstadosPedido.PuedeEliminar(pedido, actor)
            || !actor.MateriasACargo.Contains(pedido.MateriaId))
        {
            throw new ErrorDominioPedido(
                "Sólo el Jefe de Cátedra propietario puede eliminar un pedido en borrador.");
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
    /// acción lo decide la máquina de estados.
    /// </summary>
    private void RegistrarEnHistorial(
        Pedido pedido, string accion, string codigoRol, ActorContexto actor, string? comentario)
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
            CreadoEn = DateTimeOffset.UtcNow,
        };
        pedido.Historial.Add(historial);
        pedidos.AgregarHistorial(historial);
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
            Dedicacion: vigente?.DedicacionCatalogo?.Nombre ?? vigente?.Dedicacion,
            Horas: vigente?.Horas,
            Materia: materia.Nombre,
            HorasInvestigacion: vigente?.HorasInvestigacion,
            HorasExternas: vigente?.HorasExternas);
    }

    private async Task ValidarDatosAsync(
        DatosPedido datos, ActorContexto actor, CancellationToken ct)
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
        var materia = (await identity.ListarMateriasAsync(ct)).SingleOrDefault(m => m.Id == datos.MateriaId);
        if (materia is null || !materia.Activo)
            throw new ErrorDominioPedido("La materia seleccionada no existe o está inactiva.");
        if (!actor.EsDeptoWide
            && !(actor.Tiene(RolesCircuito.CoordinadorCarrera)
                && actor.CarrerasACargo.Contains(materia.CarreraId))
            && !(actor.Tiene(RolesCircuito.JefeCatedra)
                && actor.MateriasACargo.Contains(materia.Id)))
            throw new ErrorDominioPedido("La materia seleccionada está fuera del ámbito del actor.");
        if (datos.CargoSolicitadoId is { } cargoId
            && !await pedidos.ExisteCargoActivoAsync(cargoId, ct))
            throw new ErrorDominioPedido("El cargo seleccionado no existe o está inactivo.");
        if (datos.Horas is <= 0 || datos.HorasInvestigacion is < 0 || datos.HorasExternas is < 0)
            throw new ErrorDominioPedido("Las cargas horarias no pueden ser negativas y las horas del pedido deben ser positivas.");
        if (datos.TipoBaja is not null && !TiposBaja.Todos.Contains(datos.TipoBaja))
            throw new ErrorDominioPedido("El tipo de baja no es válido.");
        if (datos.TipoBaja == TiposBaja.Otro && string.IsNullOrWhiteSpace(datos.TipoBajaDetalle))
            throw new ErrorDominioPedido("El tipo de baja Otro exige un detalle.");
        if ((datos.Novedad is Novedades.Alta or Novedades.CambioDeCargoODedicacion && datos.DedicacionSolicitadaId is null)
            || (datos.DedicacionSolicitadaId is { } dedicacionId
                && !await pedidos.ExisteDedicacionActivaAsync(dedicacionId, ct)))
            throw new ErrorDominioPedido("La dedicación solicitada no es válida.");
        if (datos.Adjuntos.Any(a => !EsTipoAdjuntoValido(a.Tipo) || string.IsNullOrWhiteSpace(a.Nombre)))
            throw new ErrorDominioPedido("Uno de los adjuntos tiene tipo o nombre inválido.");

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
        pedido.DedicacionSolicitada = null;
        pedido.Horas = datos.Horas;
        pedido.HorasInvestigacion = datos.HorasInvestigacion;
        pedido.HorasExternas = datos.HorasExternas;
        pedido.Justificacion = Normalizar(datos.Justificacion);
        pedido.TipoBaja = Normalizar(datos.TipoBaja);
        pedido.TipoBajaDetalle = Normalizar(datos.TipoBajaDetalle);
    }

    private static IReadOnlyList<PedidoAdjunto> ConstruirAdjuntos(
        Guid pedidoId,
        IReadOnlyList<Modules.Designaciones.Api.GuardarAdjuntoPedidoDto> datos) =>
        datos.Select(a => new PedidoAdjunto
        {
            Id = Guid.NewGuid(),
            PedidoId = pedidoId,
            Tipo = a.Tipo,
            Nombre = a.Nombre.Trim(),
            Uri = Normalizar(a.Uri),
            CreadoEn = DateTimeOffset.UtcNow,
        }).ToArray();

    private static bool EsTipoAdjuntoValido(string tipo) => tipo is
        TiposAdjunto.Cv or TiposAdjunto.DniFrente or TiposAdjunto.DniDorso or TiposAdjunto.Justificativo;

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
