namespace Modules.Designaciones.Infrastructure.Guarani;

/// <summary>Período lectivo tal como lo informa Guaraní; el año nunca se deduce del nombre.</summary>
public sealed record PeriodoLectivoGuarani(string Id, string Nombre, int Anio);

/// <summary>
/// Una fila del endpoint de comisiones: una por combinación carrera × docente × banda,
/// con los campos de LEFT JOIN en null. Los nombres siguen los alias de la consulta SQL.
/// El documento sólo vive en memoria para vincular y nunca se persiste ni se registra.
/// </summary>
public sealed record FilaComisionGuarani(
    string? Comision,
    string? Asignacion,
    string? ComisionNombre,
    string? ComisionUbicacion,
    string? ElementoCodigo,
    string? ElementoNombre,
    string? PropuestaNombre,
    string? PropuestaCodigo,
    string? DiaSemana,
    string? HoraInicio,
    string? HoraFinalizacion,
    string? Aula,
    string? ModalidadCodigo,
    string? ModalidadDescripcion,
    string? DocenteNombre,
    string? DocenteTipoDocumento,
    string? DocenteDocumento,
    string? DocenteResponsabilidad);

/// <summary>Falla de la integración con una causa genérica (ver <c>CausasImportacion</c>).</summary>
public sealed class ExcepcionGuarani(string causa, string mensaje) : Exception(mensaje)
{
    public string Causa { get; } = causa;
}
