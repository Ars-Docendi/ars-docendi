using ArsDocendi.Shared.Aplicacion;

namespace ArsDocendi.Shared.Identity.Administracion;

public sealed record DatosPersonaSinCuenta(string Documento, string Nombre, string Apellido);

/// <summary>Frontera de escritura de personas para módulos que no administran cuentas.</summary>
public interface IAdministracionIdentity
{
    Task<Guid> CrearPersonaSinCuentaAsync(DatosPersonaSinCuenta datos, CancellationToken ct);
}

public sealed class ServicioPersonas(IRepositorioDocentes repositorio) : IAdministracionIdentity
{
    public async Task<Guid> CrearPersonaSinCuentaAsync(
        DatosPersonaSinCuenta datos,
        CancellationToken ct)
    {
        var documento = datos.Documento.Trim();
        var nombre = datos.Nombre.Trim();
        var apellido = datos.Apellido.Trim();
        if (string.IsNullOrWhiteSpace(documento)
            || string.IsNullOrWhiteSpace(nombre)
            || string.IsNullOrWhiteSpace(apellido))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "identity-person-invalid",
                "Documento, nombre y apellido son obligatorios.");
        }

        var persona = new Persona
        {
            Id = Guid.NewGuid(),
            Documento = documento,
            Nombre = nombre,
            Apellido = apellido,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        repositorio.AgregarPersona(persona);
        await repositorio.GuardarAsync(ct);
        return persona.Id;
    }
}
