namespace ArsDocendi.Shared.Identity;

/// <summary>
/// Entidad canónica de un ser humano. Existe con o sin cuenta de Azure AD: un
/// pedido de designación de novedad "Alta" refiere a un docente que nunca se
/// autenticó. Por eso <see cref="Legajo"/> es opcional (BR-designaciones-018).
/// </summary>
public sealed class Persona
{
    public Guid Id { get; set; }
    public required string Documento { get; set; }
    public string? Cuil { get; set; }
    public string? Legajo { get; set; }
    public required string Nombre { get; set; }
    public required string Apellido { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? Telefono { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public Usuario? Usuario { get; set; }
}

/// <summary>
/// Cuenta de Azure AD. Sólo autenticación: los datos personales viven en
/// <see cref="Persona"/>. <see cref="PersonaId"/> se resuelve en el primer login.
/// </summary>
public sealed class Usuario
{
    public Guid Id { get; set; }
    public Guid AzureOid { get; set; }
    public required string Upn { get; set; }
    public required string NombreParaMostrar { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? UltimoLoginEn { get; set; }
    public Guid? PersonaId { get; set; }
    public uint Version { get; set; }

    public Persona? Persona { get; set; }
    public ICollection<UsuarioRol> Roles { get; set; } = [];
}

/// <summary>
/// Rol del sistema. El catálogo NO es cerrado, pero los 7 roles originales llevan
/// <see cref="EsSistema"/> y están protegidos por trigger.
/// <para>
/// Un rol con <see cref="EsSistema"/> en <c>false</c> agrupa permisos pero NO
/// participa del circuito de aprobación de designaciones: la máquina de estados
/// resuelve la etapa por <see cref="Codigo"/> contra los roles de sistema.
/// </para>
/// </summary>
public sealed class Rol
{
    public Guid Id { get; set; }
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    /// <summary>Ámbito exigido a la asignación: <c>global</c>, <c>materia</c> o <c>carrera</c>.</summary>
    public required string Ambito { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public uint Version { get; set; }

    public ICollection<RolPermiso> Permisos { get; set; } = [];
}

/// <summary>
/// Permiso del catálogo cerrado. Cada <see cref="Codigo"/> corresponde a un check
/// de autorización del backend; no se crean permisos desde la UI.
/// </summary>
public sealed class Permiso
{
    public Guid Id { get; set; }
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public required string Descripcion { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

/// <summary>Membresía rol → permiso. Es la parte editable del modelo de autorización.</summary>
public sealed class RolPermiso
{
    public Guid RolId { get; set; }
    public Guid PermisoId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public Rol? Rol { get; set; }
    public Permiso? Permiso { get; set; }
}

/// <summary>
/// Asignación de un rol a un usuario, opcionalmente acotada a una materia y/o
/// carrera. El trigger <c>enforce_role_scope</c> valida que el ámbito cargado
/// coincida con el que declara el rol. Soft-delete vía <see cref="EliminadoEn"/>
/// para permitir revocar y volver a otorgar la misma asignación.
/// </summary>
public sealed class UsuarioRol
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid RolId { get; set; }
    public Guid? MateriaId { get; set; }
    public Guid? CarreraId { get; set; }
    public DateTimeOffset OtorgadoEn { get; set; }
    public Guid? OtorgadoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? EliminadoEn { get; set; }

    public Usuario? Usuario { get; set; }
    public Rol? Rol { get; set; }
    public Materia? Materia { get; set; }
    public Carrera? Carrera { get; set; }
}

/// <summary>Carrera. Vive en identity por ser destino de ámbito de las asignaciones de rol.</summary>
public sealed class Carrera
{
    public Guid Id { get; set; }
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public ICollection<Materia> Materias { get; set; } = [];
}

/// <summary>
/// Materia. Es también la unidad de "cátedra": el rol <c>jefe_catedra</c> tiene
/// ámbito de materia, y un pedido de designación cubre exactamente una.
/// </summary>
public sealed class Materia
{
    public Guid Id { get; set; }
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public Guid CarreraId { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public Carrera? Carrera { get; set; }
}

/// <summary>
/// Fila del log universal de cambios. Sólo lectura desde el código: la escribe el
/// trigger <c>audit.log_change</c>. Es la fuente de la metadata de auditoría que
/// las tablas de negocio deliberadamente NO desnormalizan.
/// </summary>
public sealed class RegistroCambio
{
    public long Id { get; set; }
    public required string NombreSchema { get; set; }
    public required string NombreTabla { get; set; }
    public required string ClaveFila { get; set; }
    public required string Accion { get; set; }
    public string? FilaAnterior { get; set; }
    public string? FilaNueva { get; set; }
    public string[]? ColumnasCambiadas { get; set; }
    public Guid? CambiadoPor { get; set; }
    public DateTimeOffset CambiadoEn { get; set; }
    public string? RequestId { get; set; }
}

/// <summary>Marca no productiva creada exclusivamente por el seed sintético.</summary>
public sealed class IdentidadSembrada
{
    public Guid UsuarioId { get; set; }
    public required string VersionDataset { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
