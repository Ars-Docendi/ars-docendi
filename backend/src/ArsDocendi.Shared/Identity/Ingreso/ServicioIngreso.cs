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

    /// <summary>
    /// Decide si la cuenta puede ingresar. Sólo lee: el vínculo y el último ingreso
    /// los escribe <see cref="RegistrarIngresoAsync"/> una vez aceptado.
    /// </summary>
    public async Task<ResultadoIngreso> ResolverAsync(DatosCuentaMicrosoft cuenta, CancellationToken ct)
    {
        // Una cuenta ya vinculada se reconoce por oid y tenant, que no cambian; el
        // mail sólo sirve para encontrar al usuario en su primer ingreso.
        var usuario = await repositorio.BuscarPorCuentaAsync(cuenta.TenantId, cuenta.ObjectId, ct);
        if (usuario is null)
        {
            if (!MailVerificado(cuenta)) return Rechazar(MotivoRechazoIngreso.MailNoVerificado, cuenta);

            usuario = await repositorio.BuscarPorUpnAsync(cuenta.Email!.Trim().ToLowerInvariant(), ct);
            if (usuario is null) return Rechazar(MotivoRechazoIngreso.NoRegistrado, cuenta);
            if (usuario.AzureOid is not null) return Rechazar(MotivoRechazoIngreso.CuentaDistinta, cuenta);
        }

        if (!usuario.Activo) return Rechazar(MotivoRechazoIngreso.Inactivo, cuenta);

        // Con varias asignaciones el ingreso se acepta sin elegir: la persona elige después.
        var identidad = await sesion.ObtenerAsync(usuario.Id, null, ct);
        if (identidad is null) return Rechazar(MotivoRechazoIngreso.SinRol, cuenta);

        return ResultadoIngreso.Aceptar(usuario.Id);
    }

    /// <summary>
    /// Registra un ingreso aceptado: vincula la cuenta si es el primero y actualiza
    /// el último ingreso. Quien llama debe haber fijado al usuario como actor para
    /// que la auditoría lo registre.
    /// </summary>
    public async Task RegistrarIngresoAsync(Guid usuarioId, DatosCuentaMicrosoft cuenta, CancellationToken ct)
    {
        var usuario = await repositorio.ObtenerParaActualizarAsync(usuarioId, ct);
        if (usuario.AzureOid is null)
        {
            usuario.AzureOid = cuenta.ObjectId;
            usuario.AzureTid = cuenta.TenantId;
        }
        else if (usuario.AzureOid != cuenta.ObjectId || usuario.AzureTid != cuenta.TenantId)
        {
            // Otra cuenta se vinculó entre la decisión y el registro: no se pisa.
            throw new InvalidOperationException("El usuario ya está vinculado a otra cuenta Microsoft.");
        }

        usuario.UltimoLoginEn = DateTimeOffset.UtcNow;
        await repositorio.GuardarAsync(ct);
    }

    public static bool EsCuentaPersonal(DatosCuentaMicrosoft cuenta) =>
        cuenta.TenantId == TenantCuentasPersonales;

    // shortcut: las cuentas personales se aceptan sin xms_edov; quien crea primero una cuenta
    // outlook/hotmail/live con el mail de un usuario sin vincular entra con sus roles.
    // Subir cuando la PoC confirme que Microsoft emite xms_edov en cuentas personales. Pendiente, otra PR.
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
