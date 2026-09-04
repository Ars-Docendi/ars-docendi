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

    /// <summary>
    /// Ver la consulta que el asistente generó para responder.
    /// </summary>
    /// <remarks>
    /// Superficie de diagnóstico, no de uso: el <c>WHERE</c> de una consulta
    /// generada puede llevar un documento. Se siembra <b>sin concedérselo a ningún
    /// rol</b> y se otorga desde la administración de membresías, sin desplegar.
    /// </remarks>
    public const string AsistenteVerConsulta = "asistente.ver_consulta";

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
        AsistenteVerConsulta,
    ];
}
