using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using Microsoft.AspNetCore.Http;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

public interface IServicioPedidosApi
{
    Task<IReadOnlyList<PedidoDto>> ListarAsync(Guid? periodoId, CancellationToken ct);
    Task<PedidoDto> ObtenerAsync(Guid id, CancellationToken ct);
    Task<PedidoDto> CrearAsync(GuardarPedidoDto datos, CancellationToken ct);
    Task<PedidoDto> EditarAsync(Guid id, GuardarPedidoDto datos, CancellationToken ct);
    Task EliminarAsync(Guid id, CancellationToken ct);
    Task<PedidoDto> AplicarAccionAsync(Guid id, AccionPedido accion, CancellationToken ct);
    Task<PedidoDto> AplicarAccionIdempotenteAsync(
        Guid id,
        AccionPedido accion,
        Guid clave,
        string ruta,
        string contenidoSolicitud,
        CancellationToken ct);
}

internal sealed class ServicioPedidosApi(
    ServicioPedidos servicio,
    IRepositorioPedidos repositorio,
    ResolutorActor resolutorActor,
    IConsultasIdentity identity,
    IRepositorioIdempotencia repositorioIdempotencia,
    IUnidadDeTrabajo unidadDeTrabajo) : IServicioPedidosApi
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PedidoDto>> ListarAsync(Guid? periodoId, CancellationToken ct)
    {
        var periodo = periodoId is null
            ? await repositorio.ObtenerPeriodoActivoAsync(ct)
            : null;
        var id = periodoId ?? periodo?.Id
            ?? throw new ExcepcionAplicacion(
                TipoErrorAplicacion.NoEncontrado,
                "resource-not-found",
                "No hay un período activo para listar pedidos.");
        var pedidos = await servicio.ListarPorAmbitoAsync(id, ct);
        return await MapearAsync(pedidos, ct);
    }

    public async Task<PedidoDto> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var pedido = await ObtenerAutorizadoAsync(id, ct);
        return (await MapearAsync([pedido], ct))[0];
    }

    public async Task<PedidoDto> CrearAsync(GuardarPedidoDto datos, CancellationToken ct)
    {
        var pedido = await servicio.CrearAsync(DatosPedido.Desde(datos), ct);
        return await ObtenerAsync(pedido.Id, ct);
    }

    public async Task<PedidoDto> EditarAsync(Guid id, GuardarPedidoDto datos, CancellationToken ct)
    {
        await servicio.EditarAsync(id, DatosPedido.Desde(datos), ct);
        return await ObtenerAsync(id, ct);
    }

    public Task EliminarAsync(Guid id, CancellationToken ct) => servicio.EliminarBorradorAsync(id, ct);

    public async Task<PedidoDto> AplicarAccionAsync(
        Guid id,
        AccionPedido accion,
        CancellationToken ct)
    {
        await servicio.AplicarAccionAsync(id, accion, ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<PedidoDto> AplicarAccionIdempotenteAsync(
        Guid id,
        AccionPedido accion,
        Guid clave,
        string ruta,
        string contenidoSolicitud,
        CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);
        var huella = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{id:N}|{ruta}|{contenidoSolicitud}")));
        PedidoDto? respuesta = null;

        await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
        {
            await repositorioIdempotencia.BloquearAsync(actor.UsuarioId, ruta, clave, token);
            var anterior = await repositorioIdempotencia.ObtenerVigenteAsync(
                actor.UsuarioId, ruta, clave, token);
            if (anterior is not null)
            {
                if (anterior.PedidoId != id || anterior.RequestHash != huella)
                {
                    throw new ExcepcionAplicacion(
                        TipoErrorAplicacion.Conflicto,
                        "idempotency-key-reused",
                        "La clave de idempotencia ya fue usada con otra solicitud.");
                }

                respuesta = JsonSerializer.Deserialize<PedidoDto>(anterior.ResponseBody, OpcionesJson)
                    ?? throw new InvalidOperationException(
                        "La respuesta idempotente almacenada no pudo deserializarse.");
                return;
            }

            respuesta = await AplicarAccionAsync(id, accion, token);
            repositorioIdempotencia.Agregar(new ComandoIdempotente
            {
                Id = Guid.NewGuid(),
                Clave = clave,
                ActorId = actor.UsuarioId,
                Ruta = ruta,
                PedidoId = id,
                RequestHash = huella,
                StatusCode = StatusCodes.Status200OK,
                ResponseBody = JsonSerializer.Serialize(respuesta, OpcionesJson),
                CreadoEn = DateTimeOffset.UtcNow,
            });
            await repositorioIdempotencia.GuardarAsync(token);
        }, ct);

        return respuesta ?? throw new InvalidOperationException(
            "El comando idempotente terminó sin producir una respuesta.");
    }

    private async Task<Pedido> ObtenerAutorizadoAsync(Guid id, CancellationToken ct)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(id, ct)
            ?? throw new ExcepcionAplicacion(
                TipoErrorAplicacion.NoEncontrado, "resource-not-found", "No se encontró el pedido solicitado.");
        var actor = await resolutorActor.ResolverAsync(ct);
        var carrera = await repositorio.ObtenerCarreraDelPedidoAsync(id, ct);
        if (!MaquinaEstadosPedido.AlcanzaAmbito(pedido, carrera, actor))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Prohibido,
                "pedido-scope-forbidden",
                "El pedido está fuera del ámbito del actor.");
        }
        return pedido;
    }

    private async Task<IReadOnlyList<PedidoDto>> MapearAsync(
        IReadOnlyList<Pedido> pedidos,
        CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);
        var personas = (await identity.ListarPersonasAsync(ct)).ToDictionary(p => p.Id);
        var materias = (await identity.ListarMateriasActivasAsync(ct)).ToDictionary(m => m.Id);
        var usuarios = (await identity.ListarUsuariosAsync(ct)).ToDictionary(u => u.Id);
        var resultado = new List<PedidoDto>();
        foreach (var pedido in pedidos)
        {
            if (!personas.TryGetValue(pedido.PersonaId, out var persona)
                || !materias.TryGetValue(pedido.MateriaId, out var materia))
            {
                continue;
            }
            var carrera = materia.CarreraId;
            resultado.Add(new PedidoDto(
                pedido.Id,
                pedido.Numero,
                pedido.Periodo is null
                    ? new PeriodoDto(pedido.PeriodoId, string.Empty, default, default, default, default, false, 0)
                    : Mapear(pedido.Periodo),
                new PersonaPedidoDto(
                    persona.Id, persona.Nombre, persona.Apellido, persona.Documento, persona.Legajo),
                new MateriaPedidoDto(
                    materia.Id, materia.Codigo, materia.Nombre, materia.CarreraId,
                    materia.Carrera?.Nombre ?? string.Empty),
                pedido.Novedad,
                pedido.Estado,
                pedido.Prioritario,
                pedido.CargoSolicitado is null ? null : new OpcionPedidoDto(
                    pedido.CargoSolicitado.Id,
                    pedido.CargoSolicitado.Codigo,
                    pedido.CargoSolicitado.Nombre),
                pedido.DedicacionSolicitada,
                pedido.Horas,
                pedido.HorasInvestigacion,
                pedido.HorasExternas,
                pedido.Justificacion,
                pedido.TipoBaja,
                pedido.TipoBajaDetalle,
                pedido.EtapaRetorno,
                pedido.PropietarioActual,
                pedido.Snapshot,
                pedido.Version,
                pedido.Adjuntos.Select(a => new AdjuntoPedidoDto(
                    a.Id, a.Tipo, a.Nombre, a.Uri)).ToArray(),
                pedido.Historial.OrderBy(h => h.CreadoEn).Select(h => new HistorialPedidoDto(
                    h.Id,
                    h.Accion,
                    h.RolId,
                    CodigoRol(h.RolId),
                    h.ActorId,
                    h.ActorId is { } actorId && usuarios.TryGetValue(actorId, out var usuario)
                        ? usuario.NombreParaMostrar
                        : null,
                    h.Etapa,
                    h.Comentario,
                    h.CreadoEn)).ToArray(),
                AccionesPermitidas(pedido, carrera, actor)));
        }
        return resultado;
    }

    private static IReadOnlyList<string> AccionesPermitidas(
        Pedido pedido,
        Guid carrera,
        ActorContexto actor)
    {
        var acciones = new List<string>();
        if (MaquinaEstadosPedido.PuedeEditar(pedido, actor)) acciones.Add("editar");
        if (MaquinaEstadosPedido.PuedeEliminar(pedido, actor)) acciones.Add("eliminar");
        Probar("enviar", new AccionPedido.Enviar());
        Probar("reenviar", new AccionPedido.Reenviar());
        Probar("aceptar", new AccionPedido.Aceptar());
        Probar("rechazar", new AccionPedido.Rechazar("validación"));
        Probar("devolver", new AccionPedido.Devolver("validación"));
        Probar("priorizar", new AccionPedido.Priorizar("validación"));
        Probar("despriorizar", new AccionPedido.Despriorizar());
        return acciones;

        void Probar(string nombre, AccionPedido accion)
        {
            try
            {
                MaquinaEstadosPedido.AplicarAccion(pedido, carrera, accion, actor);
                acciones.Add(nombre);
            }
            catch (ErrorDominioPedido)
            {
                // La máquina es la única autoridad; una acción rechazada no se ofrece.
            }
        }
    }

    private static string CodigoRol(Guid id) => id.ToString()[..8] switch
    {
        "a1000000" when id == Guid.Parse("a1000000-0000-4000-8000-000000000002") => RolesCircuito.JefeCatedra,
        "a1000000" when id == Guid.Parse("a1000000-0000-4000-8000-000000000003") => RolesCircuito.CoordinadorCarrera,
        "a1000000" when id == Guid.Parse("a1000000-0000-4000-8000-000000000004") => RolesCircuito.Secretaria,
        "a1000000" when id == Guid.Parse("a1000000-0000-4000-8000-000000000005") => RolesCircuito.Decanato,
        "a1000000" when id == Guid.Parse("a1000000-0000-4000-8000-000000000006") => RolesCircuito.Administrativo,
        _ => string.Empty,
    };

    private static PeriodoDto Mapear(Periodo p) => new(
        p.Id, p.Nombre, p.CargaDesde, p.CargaHasta,
        p.ImpactoDesde, p.ImpactoHasta, p.Activo, p.Version);
}
