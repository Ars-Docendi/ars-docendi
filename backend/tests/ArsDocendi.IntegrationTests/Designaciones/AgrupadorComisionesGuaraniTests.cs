using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure.Guarani;
using Modules.Designaciones.Services;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class AgrupadorComisionesGuaraniTests
{
    [Fact]
    public void Producto_cartesiano_se_reduce_a_listas_sin_repetidos()
    {
        var filas = new List<FilaComisionGuarani>();
        foreach (var carrera in new[] { "INF", "IND" })
            foreach (var documento in new[] { "30.111.222", "28999000" })
                foreach (var (asignacion, dia) in new[] { ("1", "Lunes"), ("2", "Miércoles") })
                    filas.Add(Fila("10", carrera: carrera, documento: documento, asignacion: asignacion, dia: dia));

        var comision = Assert.Single(AgrupadorComisionesGuarani.Agrupar(filas));

        Assert.Equal(["IND", "INF"], comision.Carreras.Select(c => c.Codigo));
        Assert.Equal(["28999000", "30111222"], comision.Docentes.Select(d => d.Documento).Order());
        Assert.Equal([(short)1, (short)3], comision.Horarios.Select(h => h.Dia));
    }

    [Fact]
    public void Comision_sin_docentes_ni_bandas_se_importa_con_listas_vacias()
    {
        var comision = Assert.Single(AgrupadorComisionesGuarani.Agrupar(
            [Fila("10", documento: null, docenteNombre: null, asignacion: null, dia: null, desde: null, hasta: null)]));

        Assert.Empty(comision.Docentes);
        Assert.Empty(comision.Horarios);
        Assert.Single(comision.Carreras);
    }

    [Fact]
    public void Misma_asignacion_en_dos_comisiones_se_conserva_en_ambas()
    {
        var comisiones = AgrupadorComisionesGuarani.Agrupar([Fila("10", asignacion: "7"), Fila("11", asignacion: "7")]);

        Assert.All(comisiones, c => Assert.Equal("7", Assert.Single(c.Horarios).AsignacionId));
    }

    [Fact]
    public void Dos_asignaciones_en_el_mismo_horario_con_aulas_distintas_se_conservan()
    {
        var comision = Assert.Single(AgrupadorComisionesGuarani.Agrupar(
            [Fila("10", asignacion: "7", aula: "Aula 1"), Fila("10", asignacion: "8", aula: "Lab 2")]));

        Assert.Equal(["Aula 1", "Lab 2"], comision.Horarios.Select(h => h.Aula));
    }

    [Theory]
    [InlineData("Lunes", 1)]
    [InlineData("Miercoles", 3)]
    [InlineData("miércoles", 3)]
    [InlineData("Sabado", 6)]
    [InlineData("Domingo", 7)]
    public void Dia_de_texto_se_mapea_de_lunes_1_a_domingo_7(string dia, short esperado)
    {
        var comision = Assert.Single(AgrupadorComisionesGuarani.Agrupar([Fila("10", dia: dia)]));

        Assert.Equal(esperado, Assert.Single(comision.Horarios).Dia);
    }

    [Fact]
    public void Docente_sin_documento_se_agrupa_por_nombre()
    {
        var comision = Assert.Single(AgrupadorComisionesGuarani.Agrupar(
            [Fila("10", documento: null, asignacion: "1"), Fila("10", documento: null, asignacion: "2")]));

        var docente = Assert.Single(comision.Docentes);
        Assert.Null(docente.Documento);
    }

    [Fact]
    public void Lista_vacia_es_sin_comisiones()
    {
        var error = Assert.Throws<ExcepcionGuarani>(() => AgrupadorComisionesGuarani.Agrupar([]));

        Assert.Equal(CausasImportacion.SinComisiones, error.Causa);
    }

    [Fact]
    public void Dato_propio_de_la_comision_que_difiere_entre_filas_rechaza_el_lote()
    {
        var error = Assert.Throws<ExcepcionGuarani>(() => AgrupadorComisionesGuarani.Agrupar(
            [Fila("10", nombre: "01-1"), Fila("10", nombre: "01-2", asignacion: "2")]));

        Assert.Equal(CausasImportacion.RespuestaInvalida, error.Causa);
    }

    [Theory]
    [InlineData(null, "01-1", "03500", "Lunes", "08:00:00")]
    [InlineData("10", null, "03500", "Lunes", "08:00:00")]
    [InlineData("10", "01-1", null, "Lunes", "08:00:00")]
    [InlineData("10", "01-1", "03500", "Feriado", "08:00:00")]
    [InlineData("10", "01-1", "03500", "Lunes", "8 de la mañana")]
    [InlineData("10", "01-1", "03500", "Lunes", "13:00:00")]
    public void Fila_incompleta_o_con_formato_invalido_rechaza_el_lote(
        string? comision, string? nombre, string? materia, string dia, string desde)
    {
        var error = Assert.Throws<ExcepcionGuarani>(() => AgrupadorComisionesGuarani.Agrupar(
            [Fila(comision, nombre: nombre, materia: materia, dia: dia, desde: desde)]));

        Assert.Equal(CausasImportacion.RespuestaInvalida, error.Causa);
    }

    [Theory]
    [InlineData("30.111.222", "30111222")]
    [InlineData(" 30 111 222 ", "30111222")]
    [InlineData("ab-123", "AB123")]
    [InlineData("...", null)]
    [InlineData(null, null)]
    public void Documento_se_normaliza_a_letras_y_digitos_ascii(string? documento, string? esperado) =>
        Assert.Equal(esperado, AgrupadorComisionesGuarani.NormalizarDocumento(documento));

    private static FilaComisionGuarani Fila(
        string? comision,
        string? nombre = "01-1",
        string? materia = "03500",
        string carrera = "INF",
        string? documento = "30111222",
        string? docenteNombre = "DOCENTE PRUEBA",
        string? asignacion = "1",
        string? dia = "Lunes",
        string? desde = "08:00:00",
        string? hasta = "12:00:00",
        string? aula = "Aula 1") => new(
            comision, asignacion, nombre, "San Justo", materia, "Materia", "Carrera " + carrera, carrera,
            dia, desde, hasta, aula, "A", "Presencial", docenteNombre, "DNI", documento, "Ingenieria");
}
