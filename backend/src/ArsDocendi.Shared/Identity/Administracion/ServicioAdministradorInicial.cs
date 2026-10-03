using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArsDocendi.Shared.Identity.Administracion;

/// <summary>Administrador que el despliegue declara para que un ambiente vacío sea operable.</summary>
public sealed class DatosAdministradorInicial
{
    public const string Seccion = "AdministradorInicial";

    public string? Upn { get; set; }
    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public string? Documento { get; set; }
}

/// <summary>
/// Da de alta al administrador inicial desde la configuración del despliegue, para que
/// producción tenga quien dé de alta al resto desde la pantalla de usuarios. Sólo
/// crea: si ya existe un usuario con ese UPN no lo toca, aunque lo hayan modificado o
/// desactivado. Sus datos personales nunca se versionan.
/// </summary>
public sealed class ServicioAdministradorInicial(
    IdentityDbContext db,
    ILogger<ServicioAdministradorInicial> logger)
{
    private const string RolAdministracion = "sys_admin";

    public async Task AsegurarAsync(DatosAdministradorInicial? datos, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(datos?.Upn)) return;
        if (string.IsNullOrWhiteSpace(datos.Nombre)
            || string.IsNullOrWhiteSpace(datos.Apellido)
            || string.IsNullOrWhiteSpace(datos.Documento))
        {
            throw new InvalidOperationException(
                $"{DatosAdministradorInicial.Seccion} necesita Upn, Nombre, Apellido y Documento.");
        }

        var upn = datos.Upn.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Upn.ToLower() == upn, ct))
        {
            logger.LogInformation("El administrador inicial ya existe; no se modifica");
            return;
        }

        var documento = datos.Documento.Trim();
        var ahora = DateTimeOffset.UtcNow;
        var persona = await db.Personas.Include(p => p.Usuario)
            .SingleOrDefaultAsync(p => p.Documento == documento, ct);
        if (persona?.Usuario is not null)
        {
            throw new InvalidOperationException(
                "El documento del administrador inicial ya pertenece a otra cuenta.");
        }

        persona ??= db.Personas.Add(new Persona
        {
            Id = Guid.NewGuid(),
            Documento = documento,
            Nombre = datos.Nombre.Trim(),
            Apellido = datos.Apellido.Trim(),
            CreadoEn = ahora,
        }).Entity;
        var rol = await db.Roles.SingleAsync(r => r.Codigo == RolAdministracion, ct);
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Upn = upn,
            NombreParaMostrar = $"{persona.Nombre} {persona.Apellido}",
            Activo = true,
            PersonaId = persona.Id,
            CreadoEn = ahora,
        };
        db.Usuarios.Add(usuario);
        db.UsuarioRoles.Add(new UsuarioRol
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            RolId = rol.Id,
            OtorgadoEn = ahora,
            CreadoEn = ahora,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Administrador inicial creado: {UsuarioId}", usuario.Id);
    }
}
