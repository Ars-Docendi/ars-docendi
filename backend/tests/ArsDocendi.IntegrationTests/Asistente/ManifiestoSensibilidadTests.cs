using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el manifiesto de sensibilidad, que gobierna qué sale hacia el
/// proveedor externo del modelo.
/// </summary>
/// <remarks>
/// No necesita base ni proveedor: compara dos archivos versionados y el tipo que
/// los lee. La comparación contra los identificadores reales del motor —que es la
/// otra mitad— vive en los tests de integración del ejecutor.
///
/// Se carga el manifiesto por la <b>misma vía que producción</b>, desde el recurso
/// embebido. Leerlo del disco acá probaría un archivo que el assembly podría no
/// estar llevando.
/// </remarks>
public sealed class ManifiestoSensibilidadTests
{
    private static readonly ManifiestoDeSensibilidad Sensibilidad =
        ManifiestoDeSensibilidad.Cargar();

    private static readonly Manifiesto Privilegios = Manifiesto.Cargar();

    // ------------------------------------------------------------- cobertura

    [Fact]
    public void Toda_columna_concedida_esta_clasificada()
    {
        // Es el requisito central: una columna legible sin clasificar es una
        // columna que viaja al proveedor sin que nadie lo haya decidido.
        var sinClasificar = ColumnasConcedidas()
            .Where(c => Sensibilidad.Clasificacion(c.Schema, c.Tabla, c.Columna)
                        == ClasificacionDeSensibilidad.Desconocida)
            .Select(c => $"{c.Schema}.{c.Tabla}.{c.Columna}")
            .ToArray();

        Assert.Empty(sinClasificar);
    }

    [Fact]
    public void La_verificacion_de_cobertura_no_es_vacia()
    {
        // Anti-vacuidad: si la extracción devolviera una lista vacía, el test de
        // arriba pasaría sin comparar nada.
        var concedidas = ColumnasConcedidas().ToArray();

        Assert.NotEmpty(concedidas);
        Assert.Contains(
            concedidas,
            c => c is { Schema: "identity", Tabla: "personas", Columna: "documento" });
    }

    [Fact]
    public void Una_columna_concedida_sin_clasificar_se_detecta()
    {
        // Verifica el detector, no el manifiesto: se pregunta por una columna que
        // el manifiesto no nombra y se comprueba que da Desconocida en vez de
        // degradarse a pública.
        Assert.Equal(
            ClasificacionDeSensibilidad.Desconocida,
            Sensibilidad.Clasificacion("identity", "personas", "columna_que_no_existe"));
    }

    [Fact]
    public void El_manifiesto_no_clasifica_columnas_que_nadie_concede()
    {
        // La otra dirección: una entrada de más es un manifiesto que miente sobre
        // qué está protegiendo.
        var concedidas = ColumnasConcedidas()
            .Select(c => $"{c.Schema}.{c.Tabla}.{c.Columna}")
            .ToHashSet(StringComparer.Ordinal);

        var demas = Sensibilidad.Entradas()
            .Select(e => $"{e.Schema}.{e.Tabla}.{e.Entrada.Columna}")
            .Where(clave => !concedidas.Contains(clave))
            .ToArray();

        Assert.Empty(demas);
    }

    // --------------------------------------------------------- clasificación

    [Theory]
    [InlineData("documento")]
    [InlineData("cuil")]
    [InlineData("telefono")]
    [InlineData("fecha_nacimiento")]
    public void Las_cuatro_columnas_personales_son_sensibles_por_valor(string columna)
    {
        Assert.Equal(
            ClasificacionDeSensibilidad.SensibleValor,
            Sensibilidad.Clasificacion("identity", "personas", columna));
    }

    [Fact]
    public void El_correo_institucional_es_sensible_por_valor()
    {
        // Es dato de contacto, y el manifiesto de privilegios ya lo concede solo al
        // rol con acceso a datos personales.
        Assert.Equal(
            ClasificacionDeSensibilidad.SensibleValor,
            Sensibilidad.Clasificacion("identity", "users", "upn"));
    }

    [Theory]
    [InlineData("designaciones", "pedido_historial", "comentario")]
    [InlineData("designaciones", "pedidos", "justificacion")]
    [InlineData("designaciones", "pedidos", "tipo_baja_detalle")]
    public void El_texto_libre_del_tramite_no_viaja(string schema, string tabla, string columna)
    {
        // Las tres son texto libre que puede nombrar a cualquiera. El manifiesto de
        // privilegios ya las señalaba como el mismo riesgo; acá se las clasifica en
        // consecuencia.
        Assert.Equal(
            ClasificacionDeSensibilidad.SensibleTexto,
            Sensibilidad.Clasificacion(schema, tabla, columna));
    }

    [Fact]
    public void Nombre_apellido_y_legajo_son_publicos()
    {
        // Decisión heredada del riesgo residual ya aceptado: son datos que circulan
        // en cualquier listado de cátedra. Se fija en un test para que cambiarlo sea
        // deliberado.
        Assert.All(
            new[] { "nombre", "apellido", "legajo" },
            columna => Assert.Equal(
                ClasificacionDeSensibilidad.Publica,
                Sensibilidad.Clasificacion("identity", "personas", columna)));
    }

    [Fact]
    public void Toda_columna_sensible_por_valor_trae_etiqueta()
    {
        // Sin etiqueta el marcador no diría de qué es, y el modelo redactaría una
        // frase sin sujeto.
        var sinEtiqueta = Sensibilidad.Entradas()
            .Where(e => e.Entrada.Clasificacion == ClasificacionDeSensibilidad.SensibleValor)
            .Where(e => string.IsNullOrWhiteSpace(e.Entrada.Etiqueta))
            .Select(e => $"{e.Schema}.{e.Tabla}.{e.Entrada.Columna}")
            .ToArray();

        Assert.Empty(sinEtiqueta);
    }

    [Fact]
    public void Toda_columna_no_publica_declara_su_motivo()
    {
        var sinMotivo = Sensibilidad.Entradas()
            .Where(e => e.Entrada.Clasificacion != ClasificacionDeSensibilidad.Publica)
            .Where(e => string.IsNullOrWhiteSpace(e.Entrada.Motivo))
            .Select(e => $"{e.Schema}.{e.Tabla}.{e.Entrada.Columna}")
            .ToArray();

        Assert.Empty(sinMotivo);
    }

    // ------------------------------------------------------------ validación

    [Fact]
    public void Una_categoria_desconocida_se_rechaza_al_cargar()
    {
        // Degradarla a «pública» filtraría en silencio, que es el peor modo de
        // falla posible para esta pieza.
        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ManifiestoDeSensibilidad.Interpretar(
                """
                {"tablas":[{"schema":"identity","tabla":"personas","columnas":[
                  {"columna":"documento","clasificacion":"secreta"}]}]}
                """));

        Assert.Contains("identity.personas.documento", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("secreta", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_manifiesto_sin_tablas_se_rechaza()
    {
        Assert.Throws<InvalidOperationException>(
            () => ManifiestoDeSensibilidad.Interpretar("""{"tablas":[]}"""));
    }

    [Fact]
    public void Las_tres_categorias_estan_representadas()
    {
        // Un manifiesto donde todo fuera público pasaría todos los tests de
        // cobertura y no protegería nada.
        var categorias = Sensibilidad.Entradas()
            .Select(e => e.Entrada.Clasificacion)
            .Distinct()
            .ToArray();

        Assert.Contains(ClasificacionDeSensibilidad.Publica, categorias);
        Assert.Contains(ClasificacionDeSensibilidad.SensibleValor, categorias);
        Assert.Contains(ClasificacionDeSensibilidad.SensibleTexto, categorias);
    }

    // ------------------------------------------------------------------ apoyo

    private static IEnumerable<(string Schema, string Tabla, string Columna)> ColumnasConcedidas() =>
        Privilegios.Tablas
            .Where(tabla => tabla.EsConcedida)
            .SelectMany(tabla => tabla.ColumnasConcedidas.Values
                .SelectMany(columnas => columnas)
                .Distinct(StringComparer.Ordinal)
                .Select(columna => (tabla.Schema, tabla.Tabla, columna)));
}
