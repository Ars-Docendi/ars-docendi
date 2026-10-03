using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la traducción de tablas a etiquetas humanas (design.md D4 de
/// asistente-rechazos-dinamicos).
/// </summary>
public sealed class EtiquetasDeAreasTests
{
    [Fact]
    public void Sin_cobertura_no_hay_nada_que_nombrar()
    {
        Assert.Null(EtiquetasDeAreas.Nombrar([]));
    }

    [Fact]
    public void Una_tabla_desconocida_se_salta_en_silencio()
    {
        var texto = EtiquetasDeAreas.Nombrar([new AreaCubierta("otro.esquema", null, 1)]);

        Assert.Null(texto);
    }

    [Fact]
    public void Las_tablas_conocidas_se_nombran_con_su_etiqueta_humana()
    {
        var texto = EtiquetasDeAreas.Nombrar([
            new AreaCubierta("identity.materias", null, 3),
            new AreaCubierta("identity.carreras", null, 2),
        ]);

        Assert.NotNull(texto);
        Assert.Contains("materias", texto);
        Assert.Contains("carreras", texto);
        Assert.DoesNotContain("identity", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Dos_tablas_de_la_misma_etiqueta_no_se_repiten()
    {
        var texto = EtiquetasDeAreas.Nombrar([
            new AreaCubierta("designaciones.pedidos", null, 4),
            new AreaCubierta("designaciones.pedido_historial", null, 2),
        ]);

        Assert.Equal("pedidos de designación", texto);
    }

    [Fact]
    public void El_orden_es_el_declarado_no_el_de_llegada()
    {
        var texto = EtiquetasDeAreas.Nombrar([
            new AreaCubierta("identity.carreras", null, 1),
            new AreaCubierta("designaciones.designaciones", null, 1),
        ]);

        // «designaciones» está declarada antes que «carreras» en el mapa,
        // aunque acá llega después en la lista de cobertura.
        Assert.Equal("designaciones, carreras", texto);
    }

    [Fact]
    public void Mas_de_cinco_etiquetas_se_recortan_con_el_sufijo()
    {
        var texto = EtiquetasDeAreas.Nombrar([
            new AreaCubierta("designaciones.designaciones", null, 1),
            new AreaCubierta("designaciones.pedidos", null, 1),
            new AreaCubierta("identity.materias", null, 1),
            new AreaCubierta("identity.carreras", null, 1),
            new AreaCubierta("designaciones.cargos", null, 1),
            new AreaCubierta("designaciones.periodos", null, 1),
        ]);

        Assert.NotNull(texto);
        Assert.EndsWith(", entre otros datos", texto, StringComparison.Ordinal);
        Assert.Equal(5, texto.Split(',').Length - 1); // 5 etiquetas + el sufijo, separados por coma
    }

    // ---------------------------------------------- guard de cobertura (tarea 3.2)

    [Fact]
    public void Toda_tabla_concedida_del_manifiesto_tiene_etiqueta()
    {
        var manifiesto = Manifiesto.Cargar();

        var sinEtiqueta = manifiesto.Tablas
            .Where(t => t.EsConcedida)
            .Where(t => !EtiquetasDeAreas.Conoce(t.Cualificado))
            .Select(t => t.Cualificado)
            .ToList();

        Assert.True(
            sinEtiqueta.Count == 0,
            "Las siguientes tablas concedidas no tienen etiqueta en EtiquetasDeAreas: "
                + string.Join(", ", sinEtiqueta));
    }
}
