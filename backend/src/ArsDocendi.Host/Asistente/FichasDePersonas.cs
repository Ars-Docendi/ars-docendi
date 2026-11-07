using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Aplicacion;
using Modules.Asistente.Application;

namespace ArsDocendi.Host.Asistente;

/// <summary>
/// La «Tarjeta de docente»: apellido y nombre y las designaciones vigentes que el actor puede ver,
/// con el mismo criterio de ámbito que el detalle de personas.
/// </summary>
/// <remarks>
/// <b>Sin correo ni documento.</b> El proveedor no ve el resultado del turno, así que no puede
/// saber si el actor ya tiene el correo a la vista; el perfil completo está a un click.
/// </remarks>
public sealed class FichasDePersonas(
    AlcancePersonas alcance,
    ServicioAdministracionPersonas personas,
    IHttpContextAccessor contexto) : IProveedorDeFichas
{
    public async Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct)
    {
        if (tipo != VinculosDePersonas.TipoDeLaPersona || !Guid.TryParse(id, out var personaId))
        {
            return null;
        }

        var usuario = contexto.HttpContext?.User;

        if (usuario is null)
        {
            return null;
        }

        IReadOnlySet<Guid>? materias;

        try
        {
            materias = await alcance.MateriasVisiblesAsync(usuario, ct);
        }
        catch (ExcepcionAplicacion)
        {
            return null;
        }

        var ficha = await personas.FichaAsync(personaId, materias, ct);

        return ficha is null
            ? null
            : new FichaDelElemento(tipo, id, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["apellido"] = ficha.Apellido,
                ["nombre"] = ficha.Nombre,
                ["designaciones"] = string.Join('\n', ficha.Designaciones),
            });
    }
}
