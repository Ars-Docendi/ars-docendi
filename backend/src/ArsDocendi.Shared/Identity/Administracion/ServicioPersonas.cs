using ArsDocendi.Shared.Aplicacion;

namespace ArsDocendi.Shared.Identity.Administracion;

public sealed record DatosPersonaSinCuenta(string Documento, string Nombre, string Apellido);

/// <summary>Frontera de escritura de personas para módulos que no administran cuentas.</summary>
public interface IAdministracionIdentity
{
    Task<Guid> CrearPersonaSinCuentaAsync(DatosPersonaSinCuenta datos, CancellationToken ct);

    /// <summary>
    /// Asigna el rol Docente en la materia y carrera a la cuenta de la persona. Sin efecto si
    /// la persona todavía no tiene cuenta (sin mail): la administración lo ve precargado al
    /// crearla. Idempotente: si ya lo tiene vigente no agrega otro.
    /// </summary>
    Task AsignarRolDocenteAsync(
        Guid personaId, Guid materiaId, Guid carreraId, Guid otorgadoPor, CancellationToken ct);

    /// <summary>
    /// Da de baja el rol Docente de la materia y carrera, conservando los demás roles. Sin
    /// efecto si la persona no tiene cuenta o no tiene ese rol. Idempotente.
    /// </summary>
    Task RetirarRolDocenteAsync(Guid personaId, Guid materiaId, Guid carreraId, CancellationToken ct);
}

public sealed class ServicioPersonas(IRepositorioDocentes repositorio) : IAdministracionIdentity
{
    /// <summary>UUID fijo del rol de sistema Docente (006_identity_catalogos.sql).</summary>
    internal static readonly Guid RolDocenteId = new("a1000000-0000-4000-8000-000000000001");

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

    public async Task AsignarRolDocenteAsync(
        Guid personaId, Guid materiaId, Guid carreraId, Guid otorgadoPor, CancellationToken ct)
    {
        var usuario = (await repositorio.ObtenerPersonaAsync(personaId, true, ct))?.Usuario;
        if (usuario is null || usuario.Roles.Any(r => EsDocenteEn(r, materiaId, carreraId)))
        {
            return;
        }

        var ahora = DateTimeOffset.UtcNow;
        var asignacion = new UsuarioRol
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            RolId = RolDocenteId,
            MateriaId = materiaId,
            CarreraId = carreraId,
            OtorgadoEn = ahora,
            OtorgadoPor = otorgadoPor,
            CreadoEn = ahora,
        };
        usuario.Roles.Add(asignacion);
        repositorio.AgregarAsignacion(asignacion);
        await repositorio.GuardarAsync(ct);
    }

    public async Task RetirarRolDocenteAsync(
        Guid personaId, Guid materiaId, Guid carreraId, CancellationToken ct)
    {
        var usuario = (await repositorio.ObtenerPersonaAsync(personaId, true, ct))?.Usuario;
        var vigentes = usuario?.Roles.Where(r => EsDocenteEn(r, materiaId, carreraId)).ToArray() ?? [];
        if (vigentes.Length == 0)
        {
            return;
        }

        var ahora = DateTimeOffset.UtcNow;
        foreach (var asignacion in vigentes)
        {
            asignacion.EliminadoEn = ahora;
        }
        await repositorio.GuardarAsync(ct);
    }

    private static bool EsDocenteEn(UsuarioRol asignacion, Guid materiaId, Guid carreraId) =>
        asignacion.EliminadoEn is null
        && asignacion.RolId == RolDocenteId
        && asignacion.MateriaId == materiaId
        && asignacion.CarreraId == carreraId;
}
