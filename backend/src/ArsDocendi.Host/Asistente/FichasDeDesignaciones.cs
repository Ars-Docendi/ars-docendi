using System.Globalization;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Modules.Asistente.Application;
using Modules.Designaciones.Contracts.Queries;

namespace ArsDocendi.Host.Asistente;

/// <summary>
/// Compone el puerto de fichas del asistente con la autoridad de designaciones.
/// </summary>
/// <remarks>
/// <b>Vive en el Host por la misma razón que <see cref="VinculosDeDesignaciones"/>:</b>
/// el asistente declara <see cref="IProveedorDeFichas"/> y no referencia a
/// Designaciones (ARS-46); el composition root ve a los dos y es donde se unen.
///
/// <b>Reproduce las dos mitades del detalle</b>, en su orden: la política
/// <c>designaciones.ver</c>, que ASP.NET evalúa en el atributo del controller, y el
/// ámbito, que decide el servicio del módulo al armar la ficha. Saltear la primera
/// ofrecería una ficha —y su botón «Abrir pedido»— a quien perdió el permiso después de
/// emitido el token.
///
/// <b>Traduce el DTO tipado a las claves documentadas del puerto.</b> El asistente
/// transporta un diccionario porque no puede conocer <see cref="FichaDePedidoDto"/>;
/// las claves son parte del contrato con el cliente (<c>api-contracts.md</c>). Las
/// opcionales (<c>cargo</c>, <c>dedicacion</c>) no se incluyen cuando el trámite no
/// las tiene.
/// </remarks>
public sealed class FichasDeDesignaciones(
    IDesignacionesQueries designaciones,
    IAuthorizationService autorizacion,
    IHttpContextAccessor contexto) : IProveedorDeFichas
{
    public async Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct)
    {
        // Sólo el tipo que este adaptador conoce; cualquier otro no es suyo.
        if (tipo != VinculosDeDesignaciones.TipoDelPedido || !Guid.TryParse(id, out var pedidoId))
        {
            return null;
        }

        var usuario = contexto.HttpContext?.User;

        if (usuario is null)
        {
            return null;
        }

        var permiso = await autorizacion.AuthorizeAsync(usuario, Permisos.DesignacionesVer);

        if (!permiso.Succeeded)
        {
            return null;
        }

        var ficha = await designaciones.FichaDePedidoAsync(pedidoId, ct);

        return ficha is null ? null : new FichaDelElemento(tipo, id, Datos(ficha));
    }

    private static Dictionary<string, string?> Datos(FichaDePedidoDto ficha)
    {
        var datos = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["numero"] = ficha.Numero,
            ["novedad"] = ficha.Novedad,
            ["materia"] = ficha.Materia,
            ["estado"] = ficha.Estado,
            ["estadoEtiqueta"] = ficha.EstadoEtiqueta,
            ["periodo"] = ficha.Periodo,
        };

        if (!string.IsNullOrWhiteSpace(ficha.Cargo))
        {
            datos["cargo"] = ficha.Cargo;
        }

        if (!string.IsNullOrWhiteSpace(ficha.Dedicacion))
        {
            datos["dedicacion"] = ficha.Dedicacion;
        }

        datos["ultimaActividad"] = ficha.UltimaActividad.ToString("O", CultureInfo.InvariantCulture);

        return datos;
    }
}
