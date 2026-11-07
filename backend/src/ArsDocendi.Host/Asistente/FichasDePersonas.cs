using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Aplicacion;
using Modules.Asistente.Application;

namespace ArsDocendi.Host.Asistente;

/// <summary>
/// La «Tarjeta de docente»: apellido y nombre y las designaciones vigentes que el actor puede ver,
/// con el mismo criterio de ámbito que el detalle de personas.
/// </summary>
/// <remarks>
/// <b>Correo institucional sí; documento, teléfono y correo personal no.</b> El correo es el UPN
/// de la cuenta y sale con el mismo criterio que habilita la tarjeta. La ficha va sólo al cliente:
/// se arma en el cierre del turno, después de redactar, y nunca entra al prompt del modelo.
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

        if (ficha is null)
        {
            return null;
        }

        var datos = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["apellido"] = ficha.Apellido,
            ["nombre"] = ficha.Nombre,
            ["designaciones"] = string.Join('\n', ficha.Designaciones),
        };

        // Same gate as the card itself (AlcancePersonas, passed above). Only when the persona has one.
        if (!string.IsNullOrWhiteSpace(ficha.EmailInstitucional))
        {
            datos["email"] = ficha.EmailInstitucional;
        }

        return new FichaDelElemento(tipo, id, datos);
    }
}
