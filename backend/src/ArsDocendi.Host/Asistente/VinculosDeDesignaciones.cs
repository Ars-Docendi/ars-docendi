using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Modules.Asistente.Application;
using Modules.Designaciones.Contracts.Queries;

namespace ArsDocendi.Host.Asistente;

/// <summary>
/// Compone el puerto de vínculos del asistente con la autoridad de designaciones.
/// </summary>
/// <remarks>
/// <b>Vive en el Host y no en el módulo del asistente</b>, que es el punto entero.
/// El asistente declara <see cref="IResolutorDeVinculos"/> y no referencia a
/// ningún otro módulo: la arista <c>Modules.Asistente → Modules.&lt;X&gt;.Contracts</c>
/// es ARS-46 y necesita la aprobación del equipo, y el propio código lo dice donde
/// el enrutador de dominio quedó en modo sombra. El composition root ya ve a los dos
/// módulos —así compone <c>ServicioDocentes</c> con <c>IAdministracionDesignaciones</c>—
/// y es el lugar donde esta clase de decisión corresponde.
///
/// El día que Portal quiera ofrecer «ver el perfil», se le suma un brazo acá y el
/// asistente no se entera.
///
/// <b>Reproduce las DOS mitades del endpoint de detalle</b>, en su orden: la
/// política de permiso, que en ASP.NET vive en el atributo del controller, y el
/// ámbito, que vive en el servicio del módulo. Saltearse la primera dejaría pasar
/// el caso en que la matriz de permisos cambió después de emitido el token: la RLS
/// del asistente lee la matriz en vivo y el endpoint lee el claim, así que el
/// asistente puede devolver la fila de un trámite cuya pantalla responde 403.
/// </remarks>
public sealed class VinculosDeDesignaciones(
    IDesignacionesQueries designaciones,
    IAuthorizationService autorizacion,
    IHttpContextAccessor contexto) : IResolutorDeVinculos
{
    /// <summary>
    /// Nombre del recurso en el contrato con el cliente.
    /// </summary>
    /// <remarks>
    /// Lo elige el adaptador y no el asistente, que no sabe qué es un trámite. El
    /// cliente lo traduce a una ruta; si no lo reconoce, no pinta nada.
    /// </remarks>
    public const string TipoDelPedido = "pedido-designacion";

    private static readonly IReadOnlyDictionary<string, DestinoDelVinculo> Nada =
        new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal);

    public async Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken ct)
    {
        var usuario = contexto.HttpContext?.User;

        if (usuario is null)
        {
            return Nada;
        }

        var permiso = await autorizacion.AuthorizeAsync(usuario, Permisos.DesignacionesVer);

        if (!permiso.Succeeded)
        {
            return Nada;
        }

        var pedidos = await designaciones.UbicarPedidosAsync(candidatos, ct);

        return pedidos.ToDictionary(
            pedido => pedido.Numero,
            pedido => new DestinoDelVinculo(TipoDelPedido, pedido.Id.ToString()),
            StringComparer.Ordinal);
    }
}
