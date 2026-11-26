namespace Modules.Designaciones.Domain;

/// <summary>
/// Intento de importación de las comisiones de un período lectivo desde Guaraní.
/// A lo sumo uno <c>en_curso</c> por período (lo garantiza la base).
/// </summary>
public sealed class ImportacionComisiones
{
    public Guid Id { get; set; }
    public int AnioAcademico { get; set; }
    public required string PeriodoLectivoGuaraniId { get; set; }
    public required string PeriodoLectivoNombre { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTimeOffset IniciadaEn { get; set; }
    public DateTimeOffset? FinalizadaEn { get; set; }
    public required string Estado { get; set; }
    public int? Cantidad { get; set; }
    public int? DocentesSinVincular { get; set; }
    public int? CarrerasSinVincular { get; set; }
    public string? Causa { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public static class EstadosImportacion
{
    public const string EnCurso = "en_curso";
    public const string Exitosa = "exitosa";
    public const string Fallida = "fallida";
}

/// <summary>Causas genéricas de una importación fallida; nunca contienen datos de Guaraní.</summary>
public static class CausasImportacion
{
    public const string Timeout = "timeout";
    public const string Autenticacion = "autenticacion";
    public const string RespuestaInvalida = "respuesta_invalida";
    public const string SinComisiones = "sin_comisiones";
    public const string NoDisponible = "no_disponible";
    public const string Interrumpida = "interrumpida";
}

/// <summary>
/// Comisión copiada de Guaraní. Se identifica por la PK de Guaraní, estable entre
/// reimportaciones; los vínculos con identity son nullable (NULL = no existe en el sistema).
/// </summary>
public sealed class ComisionGuarani
{
    public required string ComisionGuaraniId { get; set; }
    public Guid ImportacionId { get; set; }
    public int AnioAcademico { get; set; }
    public required string PeriodoLectivoGuaraniId { get; set; }
    public required string PeriodoLectivoNombre { get; set; }
    public required string Nombre { get; set; }
    public required string MateriaCodigo { get; set; }
    public required string MateriaNombre { get; set; }
    public Guid? MateriaId { get; set; }
    public string? ModalidadCodigo { get; set; }
    public string? Modalidad { get; set; }
    public string? Ubicacion { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public List<ComisionCarrera> Carreras { get; set; } = [];
    public List<ComisionDocente> Docentes { get; set; } = [];
    public List<ComisionHorario> Horarios { get; set; } = [];
}

public sealed class ComisionCarrera
{
    public required string ComisionGuaraniId { get; set; }
    public required string CarreraCodigo { get; set; }
    public required string CarreraNombre { get; set; }
    public Guid? CarreraId { get; set; }
}

public sealed class ComisionDocente
{
    public required string ComisionGuaraniId { get; set; }
    public short Orden { get; set; }
    public required string Nombre { get; set; }
    public string? Responsabilidad { get; set; }
    public Guid? PersonaId { get; set; }
}

/// <summary>
/// Banda horaria de la comisión. La asignación de Guaraní (día + horario + aula) puede
/// compartirse entre comisiones; cada una guarda su propia fila con el mismo id.
/// </summary>
public sealed class ComisionHorario
{
    public required string ComisionGuaraniId { get; set; }
    public required string AsignacionGuaraniId { get; set; }
    public short Dia { get; set; }
    public TimeOnly Desde { get; set; }
    public TimeOnly Hasta { get; set; }
    public string? Aula { get; set; }
}
