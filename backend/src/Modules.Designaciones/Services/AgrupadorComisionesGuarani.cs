using System.Globalization;
using System.Text;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure.Guarani;

namespace Modules.Designaciones.Services;

public sealed record CarreraImportada(string Codigo, string Nombre);

/// <summary>Docente de una comisión. <see cref="Documento"/> sólo se usa para vincular.</summary>
public sealed record DocenteImportado(string Nombre, string? Responsabilidad, string? Documento);

public sealed record HorarioImportado(string AsignacionId, short Dia, TimeOnly Desde, TimeOnly Hasta, string? Aula);

public sealed record ComisionImportada(
    string Id,
    string Nombre,
    string MateriaCodigo,
    string MateriaNombre,
    string? ModalidadCodigo,
    string? Modalidad,
    string? Ubicacion,
    IReadOnlyList<CarreraImportada> Carreras,
    IReadOnlyList<DocenteImportado> Docentes,
    IReadOnlyList<HorarioImportado> Horarios);

/// <summary>
/// Convierte las filas planas de Guaraní (una por carrera × docente × banda, con nulls de
/// LEFT JOIN) en comisiones con sus listas sin repetidos. Rechaza el lote completo si un dato
/// propio de la comisión difiere entre sus filas o si falta algo obligatorio: es una
/// inconsistencia de origen que hay que ver, no corregir en silencio.
/// </summary>
public static class AgrupadorComisionesGuarani
{
    public static IReadOnlyList<ComisionImportada> Agrupar(IReadOnlyList<FilaComisionGuarani> filas)
    {
        if (filas.Count == 0)
            throw new ExcepcionGuarani(CausasImportacion.SinComisiones, "Guaraní no informó comisiones para el período.");

        var comisiones = new List<ComisionImportada>();
        foreach (var grupo in filas.GroupBy(f => Requerido(f.Comision, "comision"), StringComparer.Ordinal))
        {
            var primera = grupo.First();
            var nombre = Unico(grupo, f => f.ComisionNombre, "comision_nombre", requerido: true)!;
            var materiaCodigo = Unico(grupo, f => f.ElementoCodigo, "elemento_codigo", requerido: true)!;
            var materiaNombre = Unico(grupo, f => f.ElementoNombre, "elemento_nombre", requerido: true)!;

            comisiones.Add(new ComisionImportada(
                primera.Comision!,
                nombre,
                materiaCodigo,
                materiaNombre,
                Unico(grupo, f => f.ModalidadCodigo, "modalidad_codigo"),
                Unico(grupo, f => f.ModalidadDescripcion, "modalidad_descripcion"),
                Unico(grupo, f => f.ComisionUbicacion, "comision_ubicacion"),
                Carreras(grupo),
                Docentes(grupo),
                Horarios(grupo)));
        }
        return comisiones;
    }

    /// <summary>
    /// Normaliza un documento para compararlo: sólo letras y dígitos ASCII, en mayúsculas.
    /// Es la misma regla que aplica <c>IConsultasIdentity.BuscarPersonasPorDocumentosAsync</c>.
    /// </summary>
    public static string? NormalizarDocumento(string? documento)
    {
        if (documento is null) return null;
        var limpio = new string(documento.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        return limpio.Length == 0 ? null : limpio;
    }

    private static IReadOnlyList<CarreraImportada> Carreras(IEnumerable<FilaComisionGuarani> grupo) =>
        grupo.Where(f => f.PropuestaCodigo is not null)
            .GroupBy(f => f.PropuestaCodigo!, StringComparer.Ordinal)
            .Select(g => new CarreraImportada(g.Key, Unico(g, f => f.PropuestaNombre, "propuesta_nombre", requerido: true)!))
            .OrderBy(c => c.Codigo, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<DocenteImportado> Docentes(IEnumerable<FilaComisionGuarani> grupo) =>
        grupo.Where(f => f.DocenteNombre is not null)
            .GroupBy(f => NormalizarDocumento(f.DocenteDocumento) ?? "nombre:" + f.DocenteNombre, StringComparer.Ordinal)
            .Select(g => new DocenteImportado(
                g.First().DocenteNombre!,
                Unico(g, f => f.DocenteResponsabilidad, "docente_responsabilidad"),
                NormalizarDocumento(g.First().DocenteDocumento)))
            .OrderBy(d => d.Nombre, StringComparer.Ordinal)
            .ThenBy(d => d.Documento, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<HorarioImportado> Horarios(IEnumerable<FilaComisionGuarani> grupo) =>
        grupo.Where(f => f.Asignacion is not null)
            .GroupBy(f => f.Asignacion!, StringComparer.Ordinal)
            .Select(g =>
            {
                var dia = Dia(Unico(g, f => f.DiaSemana, "dia_semana", requerido: true)!);
                var desde = Hora(Unico(g, f => f.HoraInicio, "hora_inicio", requerido: true)!);
                var hasta = Hora(Unico(g, f => f.HoraFinalizacion, "hora_finalizacion", requerido: true)!);
                if (desde >= hasta) throw Invalida("Una banda horaria termina antes de empezar.");
                return new HorarioImportado(g.Key, dia, desde, hasta, Unico(g, f => f.Aula, "aula"));
            })
            .OrderBy(h => h.Dia).ThenBy(h => h.Desde).ThenBy(h => h.AsignacionId, StringComparer.Ordinal)
            .ToArray();

    private static string? Unico(
        IEnumerable<FilaComisionGuarani> filas,
        Func<FilaComisionGuarani, string?> campo,
        string nombreCampo,
        bool requerido = false)
    {
        var valores = filas.Select(campo).Distinct(StringComparer.Ordinal).ToArray();
        if (valores.Length > 1) throw Invalida($"El campo {nombreCampo} difiere entre filas del mismo registro.");
        var valor = valores.SingleOrDefault();
        if (requerido && valor is null) throw Invalida($"Falta el campo obligatorio {nombreCampo}.");
        return valor;
    }

    private static string Requerido(string? valor, string nombreCampo) =>
        valor ?? throw Invalida($"Falta el campo obligatorio {nombreCampo}.");

    private static short Dia(string texto)
    {
        var normalizado = SinTildes(texto).ToLowerInvariant();
        return normalizado switch
        {
            "lunes" => 1,
            "martes" => 2,
            "miercoles" => 3,
            "jueves" => 4,
            "viernes" => 5,
            "sabado" => 6,
            "domingo" => 7,
            _ => throw Invalida("Día de la semana no reconocido."),
        };
    }

    private static TimeOnly Hora(string texto) =>
        TimeOnly.TryParseExact(texto, ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            ? hora
            : throw Invalida("Horario con formato inválido.");

    private static string SinTildes(string texto) =>
        new(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

    private static ExcepcionGuarani Invalida(string mensaje) => new(CausasImportacion.RespuestaInvalida, mensaje);
}
