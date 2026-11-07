using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services.Integridad;

namespace Modules.Designaciones.Services;

/// <summary>
/// Conciliación del lote con la rectificación: muestra los pedidos en lote del período
/// activo por docente y sincroniza los aprobados con las designaciones vigentes.
/// <para>
/// Sincronizar es el momento en que el trámite pasa a ser la realidad del sistema: abre,
/// cierra o cambia la designación, asigna o retira el rol Docente y deja el pedido
/// <c>aprobado</c>. Todo en una transacción que abarca designaciones e identity: si un
/// pedido no se puede aplicar, no se aplica ninguno.
/// </para>
/// </summary>
public interface IServicioConciliacion
{
    Task<ConciliacionDto> ConsultarAsync(CancellationToken ct);

    Task<ResultadoSincronizacionDto> SincronizarAsync(
        SincronizarConciliacionDto datos, Guid clave, CancellationToken ct);
}

internal sealed class ServicioConciliacion(
    RepositorioPedidos pedidos,
    RepositorioDesignaciones designaciones,
    RepositorioIdempotencia idempotencia,
    MaterializadorDesignaciones materializador,
    ServicioPedidos servicioPedidos,
    SelladorPedidos sellador,
    ResolutorActor resolutorActor,
    IConsultasIdentity identity,
    IAdministracionIdentity administracionIdentity,
    IUnidadDeTrabajoAdministracion unidadDeTrabajo,
    ILogger<ServicioConciliacion> logger) : IServicioConciliacion
{
    private const string RutaSincronizar = "conciliacion-sincronizar";
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ConciliacionDto> ConsultarAsync(CancellationToken ct)
    {
        var actor = await ResolverConciliadorAsync(ct);
        var periodo = await pedidos.ObtenerPeriodoActivoAsync(ct);
        if (periodo is null)
        {
            return new ConciliacionDto(null, []);
        }

        var visibles = (await pedidos.ListarEnConciliacionAsync(periodo.Id, ct))
            .Where(p => MaquinaEstadosPedido.AlcanzaAmbito(new AlcancePedido(p.MateriaId, p.CarreraId), actor))
            .ToArray();
        var personaIds = visibles.Select(p => p.PersonaId).ToHashSet();
        var personas = (await identity.ObtenerPersonasAsync(personaIds, ct)).ToDictionary(p => p.Id);
        var vigentes = (await designaciones.ListarTodasVigentesSinTrackingAsync(ct))
            .Where(d => personaIds.Contains(d.PersonaId))
            .GroupBy(d => d.PersonaId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var materias = (await identity.ListarMateriasAsync(ct)).ToDictionary(m => m.Id, m => m.Nombre);
        // Nombre de carrera: catálogo informativo materia–plan, sin FK desde pedidos.
        var carreras = (await identity.ListarMateriasPlanAsync(ct))
            .GroupBy(mp => mp.Plan!.CarreraId)
            .ToDictionary(g => g.Key, g => g.First().Plan!.Carrera!.Nombre);
        var actores = visibles.SelectMany(p => p.Historial)
            .Where(h => h.Accion == AccionesHistorial.Sincronizar && h.ActorId is not null)
            .Select(h => h.ActorId!.Value).Distinct().ToArray();
        var usuarios = (await identity.ObtenerUsuariosAsync(actores, ct))
            .ToDictionary(u => u.Id, u => u.NombreParaMostrar);

        var docentes = new List<DocenteConciliacionDto>();
        foreach (var pedido in visibles)
        {
            if (!personas.TryGetValue(pedido.PersonaId, out var persona))
            {
                continue;
            }

            var propias = vigentes.GetValueOrDefault(pedido.PersonaId) ?? [];
            var sincronizacion = pedido.Historial
                .Where(h => h.Accion == AccionesHistorial.Sincronizar)
                .MaxBy(h => h.CreadoEn);
            var rechazo = pedido.Historial
                .Where(h => h.Accion == AccionesHistorial.NoAprobar)
                .MaxBy(h => h.CreadoEn);
            var alcance = new AlcancePedido(pedido.MateriaId, pedido.CarreraId);
            var acciones = new List<string>();
            if (MaquinaEstadosPedido.PuedeConciliar(pedido, alcance, actor))
            {
                acciones.Add("sincronizar");
                if (pedido.MaterializadoEn is null) acciones.Add("rechazar");
            }
            if (MaquinaEstadosPedido.PuedeEditar(pedido, actor)) acciones.Add("editar");

            docentes.Add(new DocenteConciliacionDto(
                persona.Id,
                persona.Nombre,
                persona.Apellido,
                persona.Cuil,
                persona.Documento,
                pedido.Novedad == Novedades.Alta && propias.All(d => d.MateriaId == pedido.MateriaId),
                new PedidoConciliacionDto(
                    pedido.Id,
                    pedido.Numero,
                    pedido.Version,
                    pedido.Novedad,
                    pedido.Estado,
                    pedido.MateriaId,
                    materias.GetValueOrDefault(pedido.MateriaId, string.Empty),
                    pedido.CarreraId,
                    carreras.GetValueOrDefault(pedido.CarreraId, string.Empty),
                    Hoy(pedido, propias),
                    pedido.Novedad == Novedades.Baja ? null : Solicitado(pedido),
                    pedido.Justificacion,
                    pedido.TipoBaja,
                    pedido.TipoBajaDetalle,
                    pedido.Estado == EstadosPedido.NoAprobado ? rechazo?.Comentario : null,
                    sincronizacion?.CreadoEn,
                    sincronizacion?.ActorId is { } actorId ? usuarios.GetValueOrDefault(actorId) : null,
                    acciones),
                propias.Select(d => new DesignacionVigenteDto(
                    d.MateriaId,
                    materias.GetValueOrDefault(d.MateriaId, string.Empty),
                    d.CarreraId,
                    carreras.GetValueOrDefault(d.CarreraId, string.Empty),
                    Vigente(d))).ToArray()));
        }

        return new ConciliacionDto(
            new PeriodoDto(periodo.Id, periodo.Nombre, periodo.CargaDesde, periodo.CargaHasta,
                periodo.ImpactoDesde, periodo.ImpactoHasta, periodo.Activo, periodo.Version),
            docentes.OrderBy(d => d.Apellido).ThenBy(d => d.Nombre).ToArray());
    }

    /// <summary>
    /// Sincroniza los pedidos aceptados. Todo o nada e idempotente por
    /// <paramref name="clave"/>: un reintento con la misma clave devuelve el resultado
    /// anterior sin volver a escribir.
    /// </summary>
    public async Task<ResultadoSincronizacionDto> SincronizarAsync(
        SincronizarConciliacionDto datos, Guid clave, CancellationToken ct)
    {
        var actor = await ResolverConciliadorAsync(ct);
        var aceptados = datos.Pedidos?.DistinctBy(p => p.Id).OrderBy(p => p.Id).ToArray() ?? [];
        if (aceptados.Length == 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion, "conciliacion-sin-pedidos", "No hay pedidos aceptados para sincronizar.");
        }

        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('|', aceptados.Select(p => $"{p.Id:N}:{p.Version}")))));

        var resultado = await unidadDeTrabajo.EjecutarAsync(async token =>
        {
            await idempotencia.BloquearAsync(actor.UsuarioId, RutaSincronizar, clave, token);
            var anterior = await idempotencia.ObtenerVigenteAsync(actor.UsuarioId, RutaSincronizar, clave, token);
            if (anterior is not null)
            {
                if (anterior.RequestHash != huella)
                {
                    throw new ExcepcionAplicacion(
                        TipoErrorAplicacion.Conflicto,
                        "idempotency-key-reused",
                        "La clave de idempotencia ya fue usada con otra solicitud.");
                }
                return JsonSerializer.Deserialize<ResultadoSincronizacionDto>(anterior.ResponseBody, OpcionesJson)!;
            }

            var periodo = await pedidos.ObtenerPeriodoActivoAsync(token);
            var cargados = (await pedidos.ObtenerParaSincronizarAsync(aceptados.Select(p => p.Id).ToArray(), token))
                .ToDictionary(p => p.Id);
            var aplicar = aceptados
                .Select(a => (Pedido: cargados.GetValueOrDefault(a.Id) ?? throw new ExcepcionAplicacion(
                    TipoErrorAplicacion.NoEncontrado, "resource-not-found", "No se encontró uno de los pedidos aceptados."),
                    a.Version))
                .OrderBy(x => x.Pedido.PersonaId).ThenBy(x => x.Pedido.MateriaId)
                .ToArray();

            // Identity antes que vigencias, y personas en orden fijo: mismo orden de locks
            // que la administración de personas, para no cruzarse con ella.
            await designaciones.ValidarDestinosAsync(
                [actor.UsuarioId], aplicar.Select(x => x.Pedido.PersonaId).Distinct().ToArray(), token);
            foreach (var (pedido, _) in aplicar)
            {
                await designaciones.BloquearAsync(pedido.PersonaId, pedido.MateriaId, token);
            }

            foreach (var (pedido, version) in aplicar)
            {
                if (pedido.Version != version || pedido.Estado != EstadosPedido.EnLote || pedido.PeriodoId != periodo?.Id)
                {
                    throw new ExcepcionAplicacion(
                        TipoErrorAplicacion.Conflicto,
                        "conciliacion-pedido-cambio",
                        $"El pedido {pedido.Numero} cambió desde que se cargó la conciliación. Actualizá y volvé a aceptarlo.");
                }

                await SincronizarPedidoAsync(pedido, version, actor, token);
            }

            var respuesta = new ResultadoSincronizacionDto(aplicar.Select(x => x.Pedido.Numero).ToArray());
            idempotencia.Agregar(new ComandoIdempotente
            {
                Id = Guid.NewGuid(),
                Clave = clave,
                ActorId = actor.UsuarioId,
                Ruta = RutaSincronizar,
                // La tabla exige un pedido: el primero de la solicitud. La huella cubre a todos.
                PedidoId = aceptados[0].Id,
                RequestHash = huella,
                StatusCode = StatusCodes.Status200OK,
                ResponseBody = JsonSerializer.Serialize(respuesta, OpcionesJson),
                CreadoEn = DateTimeOffset.UtcNow,
            });
            await idempotencia.GuardarAsync(token);
            return respuesta;
        }, ct);

        logger.LogInformation("Conciliación: {Cantidad} pedidos sincronizados", resultado.Numeros.Count);
        return resultado;
    }

    private async Task SincronizarPedidoAsync(Pedido pedido, uint version, ActorContexto actor, CancellationToken ct)
    {
        var transicion = MaquinaEstadosPedido.AplicarAccion(
            pedido, new AlcancePedido(pedido.MateriaId, pedido.CarreraId), new AccionPedido.Sincronizar(), actor);
        await sellador.VerificarEstadoPrevioAsync(pedido, ct);
        pedidos.EsperarVersion(pedido, version);

        // Un pedido heredado del circuito anterior ya se escribió al aceptar Decanato.
        string? detalle = null;
        if (pedido.MaterializadoEn is null)
        {
            detalle = await materializador.MaterializarAsync(pedido, ct);
            pedido.MaterializadoEn = DateTimeOffset.UtcNow;
        }
        pedido.Estado = transicion.EstadoResultante;
        await servicioPedidos.RegistrarEnHistorialAsync(
            pedido, transicion.AccionHistorial, transicion.CodigoRolActuante, actor, detalle, ct);
        // Se guarda pedido por pedido: el sello siguiente encadena sobre la cabeza persistida.
        await pedidos.GuardarCambiosAsync(ct);

        switch (pedido.Novedad)
        {
            case Novedades.Alta:
                await administracionIdentity.AsignarRolDocenteAsync(
                    pedido.PersonaId, pedido.MateriaId, pedido.CarreraId, actor.UsuarioId, ct);
                break;
            case Novedades.Baja:
                await administracionIdentity.RetirarRolDocenteAsync(
                    pedido.PersonaId, pedido.MateriaId, pedido.CarreraId, ct);
                break;
        }
    }

    private async Task<ActorContexto> ResolverConciliadorAsync(CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);
        if (!actor.TienePermiso(Permisos.DesignacionesConciliar))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Prohibido,
                "conciliacion-forbidden",
                "Conciliar el lote requiere el permiso designaciones.conciliar.");
        }
        return actor;
    }

    /// <summary>
    /// Lo que el docente tiene hoy en la materia del pedido. Ya sincronizado, la designación
    /// vigente es la nueva: lo anterior es la fotografía tomada al enviar.
    /// </summary>
    private static ValoresDesignacionDto? Hoy(Pedido pedido, IReadOnlyList<Designacion> vigentes)
    {
        if (pedido.Estado == EstadosPedido.Aprobado)
        {
            return pedido.Novedad == Novedades.Alta || pedido.Snapshot?.Cargo is null
                ? null
                : new ValoresDesignacionDto(
                    pedido.Snapshot.Cargo, pedido.Snapshot.Dedicacion, pedido.Snapshot.Horas,
                    pedido.Snapshot.HorasInvestigacion, pedido.Snapshot.HorasExternas);
        }

        return vigentes.FirstOrDefault(d => d.MateriaId == pedido.MateriaId) is { } vigente
            ? Vigente(vigente)
            : null;
    }

    private static ValoresDesignacionDto Solicitado(Pedido pedido) => new(
        pedido.CargoSolicitado?.Nombre,
        pedido.DedicacionSolicitadaCatalogo?.Nombre ?? pedido.DedicacionSolicitada,
        pedido.Horas,
        pedido.HorasInvestigacion,
        pedido.HorasExternas);

    private static ValoresDesignacionDto Vigente(Designacion d) => new(
        d.Cargo?.Nombre,
        d.DedicacionCatalogo?.Nombre ?? d.Dedicacion,
        d.Horas,
        d.HorasInvestigacion,
        d.HorasExternas);
}
