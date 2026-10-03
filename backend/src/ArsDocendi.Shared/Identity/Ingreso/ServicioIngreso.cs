using Microsoft.Extensions.Logging;

namespace ArsDocendi.Shared.Identity.Ingreso;

/// <summary>
/// Regla de ingreso con cuentas Microsoft. No crea usuarios: sólo deja pasar a
/// quien la administración dio de alta, activo y con un rol vigente. El mail se
/// usa únicamente si Microsoft lo informa como verificado.
/// </summary>
public sealed class ServicioIngreso(
    RepositorioIngreso repositorio,
    ServicioSesion sesion,
    ILogger<ServicioIngreso> logger)
{
    /// <summary>Tenant con el que Microsoft identifica a las cuentas personales.</summary>
    public static readonly Guid TenantCuentasPersonales = Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad");

    public async Task<ResultadoIngreso> ResolverAsync(DatosCuentaMicrosoft cuenta, CancellationToken ct)
    {
        if (!MailVerificado(cuenta)) return Rechazar(MotivoRechazoIngreso.MailNoVerificado, cuenta);

        var usuario = await repositorio.BuscarPorUpnAsync(cuenta.Email!.Trim().ToLowerInvariant(), ct);
        if (usuario is null) return Rechazar(MotivoRechazoIngreso.NoRegistrado, cuenta);
        if (!usuario.Activo) return Rechazar(MotivoRechazoIngreso.Inactivo, cuenta);

        var identidad = await sesion.ObtenerAsync(usuario.Id, ct);
        if (identidad is null) return Rechazar(MotivoRechazoIngreso.SinRol, cuenta);

        if (identidad.CantidadRoles > 1)
        {
            logger.LogWarning(
                "El usuario {UsuarioId} tiene {CantidadRoles} roles vigentes; la sesión usa {RolCodigo}",
                identidad.UsuarioId, identidad.CantidadRoles, identidad.RolCodigo);
        }

        return ResultadoIngreso.Aceptar(usuario.Id);
    }

    public static bool EsCuentaPersonal(DatosCuentaMicrosoft cuenta) =>
        cuenta.TenantId == TenantCuentasPersonales;

    // xms_edov cubre los dominios verificados por la organización. Para cuentas
    // personales Microsoft verifica el mail al crearlas, pero no está garantizado
    // que emita el claim: la PoC confirma si esta excepción hace falta.
    private static bool MailVerificado(DatosCuentaMicrosoft cuenta) =>
        !string.IsNullOrWhiteSpace(cuenta.Email)
        && (cuenta.EmailDominioVerificado == true || EsCuentaPersonal(cuenta));

    private ResultadoIngreso Rechazar(MotivoRechazoIngreso motivo, DatosCuentaMicrosoft cuenta)
    {
        logger.LogInformation(
            "Ingreso con Microsoft rechazado: {Motivo} (cuenta {TipoCuenta})",
            motivo, EsCuentaPersonal(cuenta) ? "personal" : "organizacional");
        return ResultadoIngreso.Rechazar(motivo);
    }
}
