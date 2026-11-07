using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Designaciones.Api;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;

namespace ArsDocendi.IntegrationTests.Designaciones;

/// <summary>
/// Arma la conciliación como la registra el módulo, con un actor que tiene
/// <c>designaciones.conciliar</c>. Las designaciones vigentes se escriben al sincronizar,
/// no al aceptar en Decanato: los tests de vigencias pasan por acá.
/// </summary>
internal static class ConciliacionPrueba
{
    /// <summary>Usuario Secretaría del seed sintético.</summary>
    public static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    public static IServicioConciliacion Crear(
        Guid usuario,
        IdentityDbContext identityDb,
        DesignacionesDbContext db,
        TimeProvider? reloj = null,
        params string[] permisos)
    {
        var identity = new ConsultasIdentity(identityDb);
        var resolutor = new ResolutorActor(
            new UsuarioConPermisos(usuario, permisos.Length == 0 ? [Permisos.DesignacionesConciliar] : permisos),
            identity);
        var pedidos = new RepositorioPedidos(db);
        var designaciones = new RepositorioDesignaciones(db);
        var sellador = SellosPrueba.Sellador(db);
        var servicioPedidos = new ServicioPedidos(
            pedidos, designaciones, resolutor, identity, new UnidadDeTrabajo(db), sellador,
            NullLogger<ServicioPedidos>.Instance);
        return new ServicioConciliacion(
            pedidos,
            designaciones,
            new RepositorioIdempotencia(db),
            new MaterializadorDesignaciones(designaciones, reloj),
            servicioPedidos,
            sellador,
            resolutor,
            identity,
            new ServicioPersonas(new RepositorioDocentes(identityDb)),
            new UnidadDeTrabajoAdministracion(),
            NullLogger<ServicioConciliacion>.Instance);
    }

    /// <summary>Sincroniza un único pedido con su versión persistida, como Secretaría.</summary>
    public static async Task<ResultadoSincronizacionDto> SincronizarAsync(
        Guid pedidoId,
        IdentityDbContext identityDb,
        DesignacionesDbContext db,
        CancellationToken ct,
        TimeProvider? reloj = null)
    {
        var version = await db.Pedidos.AsNoTracking().Where(p => p.Id == pedidoId).Select(p => p.Version).SingleAsync(ct);
        db.ChangeTracker.Clear();
        return await Crear(Secretaria, identityDb, db, reloj).SincronizarAsync(
            new SincronizarConciliacionDto([new PedidoAceptadoDto(pedidoId, version)]), Guid.NewGuid(), ct);
    }

    internal sealed class UsuarioConPermisos(Guid id, IReadOnlyList<string> permisos) : ICurrentUser
    {
        public string UserId => id.ToString();
        public string? Email => "fixture@example.invalid";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => permisos;
        public bool IsAuthenticated => true;
    }
}
