using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity;

/// <summary>Datos ya validados del principal autenticado y de la persona que declara.</summary>
public sealed record DatosPrimerLogin(
    Guid AzureOid,
    string Upn,
    string NombreParaMostrar,
    string Documento);

/// <summary>
/// Crea o actualiza la cuenta de autenticación y la vincula con la persona canónica.
/// El documento llega explícito desde la capa de autenticación: no se infiere del UPN.
/// </summary>
public interface IVinculadorPrimerLogin
{
    Task<Usuario> VincularAsync(DatosPrimerLogin datos, CancellationToken ct = default);
}

internal sealed class VinculadorPrimerLogin(IdentityDbContext db) : IVinculadorPrimerLogin
{
    public async Task<Usuario> VincularAsync(DatosPrimerLogin datos, CancellationToken ct = default)
    {
        if (datos.AzureOid == Guid.Empty)
        {
            throw new ArgumentException("El oid de Azure AD es obligatorio.", nameof(datos));
        }

        if (string.IsNullOrWhiteSpace(datos.Upn)
            || string.IsNullOrWhiteSpace(datos.NombreParaMostrar)
            || string.IsNullOrWhiteSpace(datos.Documento))
        {
            throw new ArgumentException("UPN, nombre para mostrar y documento son obligatorios.", nameof(datos));
        }

        var documento = datos.Documento.Trim();
        var persona = await db.Personas.SingleOrDefaultAsync(p => p.Documento == documento, ct)
            ?? throw new InvalidOperationException(
                $"No existe una persona registrada con documento {documento}.");

        var upn = datos.Upn.Trim();
        var porOid = await db.Usuarios.SingleOrDefaultAsync(u => u.AzureOid == datos.AzureOid, ct);
        var porUpn = await db.Usuarios.SingleOrDefaultAsync(u => u.Upn == upn, ct);

        if (porOid is not null && porUpn is not null && porOid.Id != porUpn.Id)
        {
            throw new InvalidOperationException(
                "El oid y el UPN pertenecen a cuentas distintas; no se puede vincular el login.");
        }

        var usuario = porOid ?? porUpn;
        if (usuario is null)
        {
            usuario = new Usuario
            {
                AzureOid = datos.AzureOid,
                Upn = upn,
                NombreParaMostrar = datos.NombreParaMostrar.Trim(),
                Activo = true,
                PersonaId = persona.Id,
                CreadoEn = DateTimeOffset.UtcNow,
                UltimoLoginEn = DateTimeOffset.UtcNow,
            };
            db.Usuarios.Add(usuario);
        }
        else
        {
            if (usuario.PersonaId is not null && usuario.PersonaId != persona.Id)
            {
                throw new InvalidOperationException(
                    "La cuenta de Azure AD ya está vinculada con otra persona.");
            }

            usuario.AzureOid = datos.AzureOid;
            usuario.Upn = upn;
            usuario.NombreParaMostrar = datos.NombreParaMostrar.Trim();
            usuario.PersonaId = persona.Id;
            usuario.Activo = true;
            usuario.UltimoLoginEn = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return usuario;
    }
}
