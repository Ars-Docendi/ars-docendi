namespace ArsDocendi.Shared.Auth;

public static class Permisos
{
    public const string Claim = "permiso";
    public const string UsuariosVer = "usuarios.ver";
    public const string UsuariosAdministrar = "usuarios.administrar";
    public const string RolesVer = "roles.ver";
    public const string RolesAdministrar = "roles.administrar";
    public const string RolesGestionarMembresia = "roles.gestionar_membresia";
    public const string PeriodosAdministrar = "periodos.administrar";
    public const string DesignacionesVer = "designaciones.ver";
    public const string DesignacionesGestionar = "designaciones.gestionar";
    public const string DesignacionesAprobarCoordinacion = "designaciones.aprobar_coordinacion";
    public const string DesignacionesAprobarSecretaria = "designaciones.aprobar_secretaria";
    public const string DesignacionesAprobarDecanato = "designaciones.aprobar_decanato";
    public const string DesignacionesRevisar = "designaciones.revisar";

    /// <summary>
    /// Admisión al asistente conversacional. Se administra desde la membresía de
    /// roles, sin desplegar.
    /// </summary>
    public const string AsistenteConsultar = "asistente.consultar";

    public static readonly string[] Todos =
    [
        UsuariosVer,
        UsuariosAdministrar,
        RolesVer,
        RolesAdministrar,
        RolesGestionarMembresia,
        PeriodosAdministrar,
        DesignacionesVer,
        DesignacionesGestionar,
        DesignacionesAprobarCoordinacion,
        DesignacionesAprobarSecretaria,
        DesignacionesAprobarDecanato,
        AsistenteConsultar,
    ];
}
