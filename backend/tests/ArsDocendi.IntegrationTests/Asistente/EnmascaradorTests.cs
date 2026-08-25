using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la frontera de salida: qué del resultado puede viajar al proveedor.
/// </summary>
/// <remarks>
/// Todo en memoria, sin base y sin proveedor: el enmascarador es una función pura
/// sobre un resultado ya clasificado. Eso es a propósito — la pieza que decide qué
/// datos personales salen del sistema tiene que poder ejercitarse exhaustivamente
/// y barato.
/// </remarks>
public sealed class EnmascaradorTests
{
    private const string Documento = "30111222";
    private const string Telefono = "1155667788";

    private static readonly SensibilidadDeColumna Publica = SensibilidadDeColumna.Publica;

    private static readonly SensibilidadDeColumna DocumentoSensible =
        new(ClasificacionDeSensibilidad.SensibleValor, "documento");

    private static readonly SensibilidadDeColumna TelefonoSensible =
        new(ClasificacionDeSensibilidad.SensibleValor, "teléfono");

    private static readonly SensibilidadDeColumna TextoLibre =
        new(ClasificacionDeSensibilidad.SensibleTexto);

    // ------------------------------------------------------- sensible-valor

    [Fact]
    public void El_valor_sensible_no_sobrevive_al_enmascarado()
    {
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["apellido", "documento"],
            [Publica, DocumentoSensible],
            [["Gómez", Documento]]));

        Assert.DoesNotContain(Documento, Texto(enmascarado), StringComparison.Ordinal);
    }

    [Fact]
    public void El_alias_no_lo_deja_pasar()
    {
        // La columna viene con un nombre que no está en ningún manifiesto. Lo que
        // la tapa es la clasificación, que salió del identificador del motor.
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["codigo_interno"], [DocumentoSensible], [[Documento]]));

        Assert.DoesNotContain(Documento, Texto(enmascarado), StringComparison.Ordinal);
    }

    [Fact]
    public void Las_columnas_publicas_viajan_intactas()
    {
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["apellido", "documento"],
            [Publica, DocumentoSensible],
            [["Gómez", Documento]]));

        Assert.Equal(["apellido", "documento"], enmascarado.Columnas);
        Assert.Equal("Gómez", enmascarado.Filas[0][0]);
    }

    [Fact]
    public void Un_nulo_sensible_se_deja_nulo()
    {
        // Taparlo inventaría un dato donde no lo hay: la redacción lo muestra como
        // «sin dato», que es la verdad.
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["documento"], [DocumentoSensible], [[null]]));

        Assert.Null(enmascarado.Filas[0][0]);
    }

    // ----------------------------------------------------------- marcadores

    [Fact]
    public void El_mismo_valor_recibe_el_mismo_marcador()
    {
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["documento"],
            [DocumentoSensible],
            [[Documento], [Documento], ["40999888"]]));

        Assert.Equal(enmascarado.Filas[0][0], enmascarado.Filas[1][0]);
        Assert.NotEqual(enmascarado.Filas[0][0], enmascarado.Filas[2][0]);
    }

    [Fact]
    public void El_marcador_nombra_la_clase_de_dato()
    {
        // Sin esto el modelo redactaría una frase sin sujeto: sabe que hay algo
        // tapado pero no de qué.
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["a", "b"],
            [DocumentoSensible, TelefonoSensible],
            [[Documento, Telefono]]));

        Assert.Contains("documento", (string)enmascarado.Filas[0][0]!, StringComparison.Ordinal);
        Assert.Contains("teléfono", (string)enmascarado.Filas[0][1]!, StringComparison.Ordinal);
    }

    [Fact]
    public void El_marcador_depende_del_orden_y_no_del_valor()
    {
        // Si dependiera del valor —un hash— sería invertible por fuerza bruta: el
        // espacio de documentos es chico y conocido, así que el marcador viajaría
        // al proveedor y sería el dato con un paso más.
        var primero = Enmascarador.Enmascarar(Resultado(
            ["documento"], [DocumentoSensible], [[Documento], ["40999888"]]));
        var segundo = Enmascarador.Enmascarar(Resultado(
            ["documento"], [DocumentoSensible], [["40999888"], [Documento]]));

        // El MISMO valor, en respuestas distintas, recibe marcadores DISTINTOS:
        // eso es lo que prueba que el marcador no se deriva del valor.
        Assert.NotEqual(primero.Filas[0][0], segundo.Filas[1][0]);

        // Y valores DISTINTOS en la misma posición reciben el MISMO marcador, que
        // es la otra cara de lo mismo.
        Assert.Equal(primero.Filas[0][0], segundo.Filas[0][0]);
    }

    [Fact]
    public void Los_marcadores_de_clases_distintas_se_numeran_por_separado()
    {
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["a", "b"],
            [DocumentoSensible, TelefonoSensible],
            [[Documento, Telefono]]));

        Assert.EndsWith("1»", (string)enmascarado.Filas[0][0]!, StringComparison.Ordinal);
        Assert.EndsWith("1»", (string)enmascarado.Filas[0][1]!, StringComparison.Ordinal);
    }

    // ------------------------------------------------------- sensible-texto

    [Fact]
    public void La_columna_de_texto_libre_desaparece_entera()
    {
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["estado", "comentario"],
            [Publica, TextoLibre],
            [["rechazado", "hablé con Gómez, tiene el teléfono de Pérez"]]));

        Assert.Equal(["estado"], enmascarado.Columnas);
        Assert.DoesNotContain("comentario", Texto(enmascarado), StringComparison.Ordinal);
        Assert.DoesNotContain("Pérez", Texto(enmascarado), StringComparison.Ordinal);
    }

    [Fact]
    public void Un_resultado_todo_sensible_queda_sin_columnas()
    {
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["comentario"], [TextoLibre], [["cualquier cosa"]]));

        Assert.Empty(enmascarado.Columnas);
        Assert.All(enmascarado.Filas, fila => Assert.Empty(fila));
    }

    [Fact]
    public void La_supresion_no_desalinea_las_columnas_restantes()
    {
        // El bug fácil: sacar una columna del medio y que los valores se corran.
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["primera", "libre", "tercera"],
            [Publica, TextoLibre, Publica],
            [["A", "secreto", "C"]]));

        Assert.Equal(["primera", "tercera"], enmascarado.Columnas);
        Assert.Equal(["A", "C"], enmascarado.Filas[0]);
    }

    // ------------------------------------------------------------ intactos

    [Fact]
    public void Un_resultado_sin_columnas_sensibles_vuelve_igual()
    {
        var original = Resultado(["a", "b"], [Publica, Publica], [["uno", "dos"]]);

        Assert.Same(original, Enmascarador.Enmascarar(original));
    }

    [Fact]
    public void El_origen_desconocido_se_trata_como_publico()
    {
        // Decisión declarada: enmascarar todo origen desconocido rompería
        // count(*), que es la forma más común de consulta agregada.
        var enmascarado = Enmascarador.Enmascarar(Resultado(
            ["total"], [SensibilidadDeColumna.Desconocida], [[4]]));

        Assert.Equal(4, enmascarado.Filas[0][0]);
    }

    [Fact]
    public void El_truncado_se_conserva()
    {
        var enmascarado = Enmascarador.Enmascarar(new ResultadoDeConsulta(
            ["documento"], [[Documento]], Truncado: true, [DocumentoSensible]));

        Assert.True(enmascarado.Truncado);
    }

    [Fact]
    public void Sin_clasificacion_todo_se_trata_como_publico()
    {
        // Fija el valor por omisión del cuarto parámetro de ResultadoDeConsulta.
        // Es fail-open, y está acá para que quien lo cambie lo haga a propósito.
        var sinClasificar = new ResultadoDeConsulta(["documento"], [[Documento]], Truncado: false);

        Assert.Same(sinClasificar, Enmascarador.Enmascarar(sinClasificar));
    }

    // ------------------------------------------------------------------ apoyo

    private static ResultadoDeConsulta Resultado(
        IReadOnlyList<string> columnas,
        IReadOnlyList<SensibilidadDeColumna> sensibilidad,
        IReadOnlyList<IReadOnlyList<object?>> filas) =>
        new(columnas, filas, Truncado: false, sensibilidad);

    /// <summary>
    /// El prompt tal como lo vería el proveedor: es el texto sobre el que hay que
    /// afirmar, porque afirmar sobre las filas dejaría pasar un valor que se colara
    /// por el nombre de una columna.
    /// </summary>
    private static string Texto(ResultadoDeConsulta resultado) =>
        RedactorDeRespuesta.ArmarMensaje("¿Quiénes son?", resultado, actorEsGlobal: true);
}
