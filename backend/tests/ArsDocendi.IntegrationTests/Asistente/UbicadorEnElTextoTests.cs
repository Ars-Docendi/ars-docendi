using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Las posiciones, dentro del texto de la respuesta, de los valores que el resultado
/// ya vinculó.
/// </summary>
/// <remarks>
/// Función pura: no hay base ni módulo. LO QUE SE CUIDA es que el texto no invente
/// enlaces. Un valor se señala sólo si una celda con vínculo lo tiene, como palabra
/// completa, y con el índice exacto que el cliente va a usar para cortar el texto.
/// </remarks>
public sealed class UbicadorEnElTextoTests
{
    private static readonly IReadOnlyList<IReadOnlyList<object?>> UnaFila = [["2026-9005", "devuelto"]];

    private static readonly IReadOnlyList<VinculoDelResultado> VinculoDeLaPrimeraCelda =
        [new(0, 0, "pedido-designacion", "abc")];

    [Fact]
    public void El_numero_vinculado_se_ubica_con_su_desde_y_su_largo_exactos()
    {
        const string texto = "El pedido 2026-9005 está en revisión.";

        var ubicado = Assert.Single(UbicadorEnElTexto.Ubicar(texto, UnaFila, VinculoDeLaPrimeraCelda));

        Assert.Equal(texto.IndexOf("2026-9005", StringComparison.Ordinal), ubicado.Desde);
        Assert.Equal("2026-9005".Length, ubicado.Largo);
        Assert.Equal("2026-9005", texto.Substring(ubicado.Desde, ubicado.Largo));
        Assert.Equal(("pedido-designacion", "abc"), (ubicado.Tipo, ubicado.Id));
    }

    [Theory]
    [InlineData("El pedido 2026-90051 está en revisión.")]
    [InlineData("El pedido x2026-9005 está en revisión.")]
    [InlineData("El pedido 2026-9005a está en revisión.")]
    [InlineData("El pedido a-2026-9005 está en revisión.")]
    [InlineData("El pedido 2026-9005-b está en revisión.")]
    public void Un_valor_pegado_a_una_letra_un_digito_o_un_guion_no_coincide(string texto)
    {
        Assert.Empty(UbicadorEnElTexto.Ubicar(texto, UnaFila, VinculoDeLaPrimeraCelda));
    }

    [Theory]
    [InlineData("(2026-9005)")]
    [InlineData("2026-9005, el único.")]
    [InlineData("«2026-9005»")]
    [InlineData("2026-9005")]
    public void La_puntuacion_o_el_borde_del_texto_no_impiden_la_coincidencia(string texto)
    {
        Assert.Single(UbicadorEnElTexto.Ubicar(texto, UnaFila, VinculoDeLaPrimeraCelda));
    }

    [Fact]
    public void Un_valor_sin_vinculo_no_se_ubica()
    {
        // El texto menciona 2026-8000 y el resultado no lo vinculó: es prosa.
        const string texto = "Ver 2026-8000 y también 2026-9005.";

        var ubicado = Assert.Single(UbicadorEnElTexto.Ubicar(texto, UnaFila, VinculoDeLaPrimeraCelda));

        Assert.Equal("2026-9005", texto.Substring(ubicado.Desde, ubicado.Largo));
    }

    [Fact]
    public void Una_celda_sin_vinculo_no_se_ubica_aunque_el_texto_la_nombre()
    {
        // La segunda celda («devuelto») está en el texto pero no tiene vínculo.
        Assert.Empty(UbicadorEnElTexto.Ubicar("Quedó devuelto.", UnaFila, VinculoDeLaPrimeraCelda));
    }

    [Fact]
    public void Dos_apariciones_dan_dos_posiciones_en_orden()
    {
        const string texto = "2026-9005 se devolvió; luego 2026-9005 volvió.";

        var ubicados = UbicadorEnElTexto.Ubicar(texto, UnaFila, VinculoDeLaPrimeraCelda);

        Assert.Equal([0, texto.LastIndexOf("2026-9005", StringComparison.Ordinal)], ubicados.Select(u => u.Desde));
    }

    [Fact]
    public void Tildes_y_enie_antes_del_valor_dan_el_mismo_indice_que_IndexOf()
    {
        const string texto = "Año pasado: el pedido 2026-9005 de la cátedra de Diseño.";

        var ubicado = Assert.Single(UbicadorEnElTexto.Ubicar(texto, UnaFila, VinculoDeLaPrimeraCelda));

        Assert.Equal(texto.IndexOf("2026-9005", StringComparison.Ordinal), ubicado.Desde);
    }

    [Fact]
    public void Un_emoji_antes_del_valor_cuenta_dos_unidades_UTF16()
    {
        // El cliente corta con String.prototype.slice, que cuenta unidades de código:
        // un emoji fuera del plano básico son DOS.
        const string texto = "📌 2026-9005";

        var ubicado = Assert.Single(UbicadorEnElTexto.Ubicar(texto, UnaFila, VinculoDeLaPrimeraCelda));

        Assert.Equal(3, ubicado.Desde);
        Assert.Equal(texto.IndexOf("2026-9005", StringComparison.Ordinal), ubicado.Desde);
    }

    [Fact]
    public void La_comparacion_es_ordinal_y_no_ignora_mayusculas()
    {
        IReadOnlyList<IReadOnlyList<object?>> filas = [["ABC-1"]];

        Assert.Empty(UbicadorEnElTexto.Ubicar("Ver abc-1.", filas, [new(0, 0, "t", "1")]));
    }

    [Fact]
    public void Sin_vinculos_o_sin_texto_no_hay_nada_que_senalar()
    {
        Assert.Empty(UbicadorEnElTexto.Ubicar("2026-9005", UnaFila, []));
        Assert.Empty(UbicadorEnElTexto.Ubicar(string.Empty, UnaFila, VinculoDeLaPrimeraCelda));
    }

    [Fact]
    public void Dos_celdas_con_vinculo_se_ubican_cada_una_con_su_destino()
    {
        IReadOnlyList<IReadOnlyList<object?>> filas = [["2026-9005"], ["2026-9006"]];
        IReadOnlyList<VinculoDelResultado> vinculos = [new(0, 0, "p", "a"), new(1, 0, "p", "b")];

        var ubicados = UbicadorEnElTexto.Ubicar("Entre 2026-9006 y 2026-9005.", filas, vinculos);

        Assert.Equal(["b", "a"], ubicados.Select(u => u.Id));
    }
}
