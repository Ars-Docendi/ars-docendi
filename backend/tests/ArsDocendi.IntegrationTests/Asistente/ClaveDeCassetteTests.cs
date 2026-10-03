using System.Text;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La clave con que un cassette se busca en disco, calculada sobre el cable.
/// </summary>
/// <remarks>
/// Es la pieza de la que depende que un cassette grabado hoy se encuentre dentro
/// de un año: si la clave cambiara entre procesos, cada corrida buscaría archivos
/// que nadie escribió y el mecanismo entero fallaría cerrado sin motivo.
///
/// Se calcula sobre el <b>cuerpo de la solicitud</b> y no sobre la
/// <c>SolicitudAlModelo</c>, porque quien la necesita es un handler HTTP que ve
/// bytes y no tipos del puerto — que es justamente lo que deja al guard del SDK
/// en pie sin excepción nueva.
/// </remarks>
public sealed class ClaveDeCassetteTests
{
    private const string Prefijo = "Esquema de identity y designaciones. Respondé solo con SQL.";
    private const string Mensaje = "¿Qué docentes dictan Bases de Datos?";
    private const string Modelo = "claude-sonnet-5";

    // ------------------------------------------------------------ determinismo

    [Fact]
    public void La_misma_solicitud_produce_la_misma_clave_en_cualquier_proceso()
    {
        // El valor está escrito a mano y se calculó por fuera de este código
        // —SHA-256 de los cuatro campos unidos por el separador—, no capturándolo
        // de una corrida. Comparar contra lo que el propio código devuelve dos
        // veces en el MISMO proceso no probaría nada: `string.GetHashCode()` está
        // aleatorizado por proceso y también pasaría esa prueba.
        const string Esperada =
            "d09d348dc5e1011f1676c456b1076d557f45818ed34f17b951fce322d929e77d";

        Assert.Equal(Esperada, ClaveDeCassette.Calcular(Cuerpo()).Clave);
    }

    [Fact]
    public void Dos_calculos_de_la_misma_solicitud_coinciden()
    {
        Assert.Equal(
            ClaveDeCassette.Calcular(Cuerpo()).Clave,
            ClaveDeCassette.Calcular(Cuerpo()).Clave);
    }

    // ------------------------------------------------------- los cuatro campos

    /// <summary>Qué mutación del cuerpo mueve la clave y cuál no.</summary>
    public static TheoryData<string, bool> MutacionesDelCuerpo => new()
    {
        { "prefijo", true },
        { "mensaje", true },
        { "esfuerzo", true },
        // Omitir `output_config` es lo que el adaptador hace con esfuerzo mínimo, y
        // no es «esfuerzo bajo»: hay modelos que rechazan el campo con 400. Dos
        // llamadas que difieren en eso le hablan distinto al modelo y no pueden
        // compartir cassette.
        { "esfuerzo-omitido", true },
        { "modelo", true },
        // ES EL PUNTO DE QUE LA CLAVE SEAN CUATRO CAMPOS Y NO EL CUERPO ENTERO.
        // `MaximoDeTokensDeGeneracion` es una perilla que la propia documentación
        // invita a mover cuando aparece el aviso de corte; hashear el cuerpo
        // completo invalidaría todos los cassettes de golpe, y recuperarlos costaría
        // otra corrida financiada.
        { "techo-de-tokens", false },
    };

    [Theory]
    [MemberData(nameof(MutacionesDelCuerpo))]
    public void Solo_los_cuatro_campos_del_cable_mueven_la_clave(string mutacion, bool mueve)
    {
        var mutado = mutacion switch
        {
            "prefijo" => Cuerpo(prefijo: Prefijo + " Y nada más."),
            "mensaje" => Cuerpo(mensaje: "¿Cuántos pedidos hay?"),
            "esfuerzo" => Cuerpo(esfuerzo: "high"),
            "esfuerzo-omitido" => Cuerpo(esfuerzo: null),
            "modelo" => Cuerpo(modelo: "claude-opus-5"),
            "techo-de-tokens" => Cuerpo(maximoDeTokens: 16000),
            _ => throw new ArgumentOutOfRangeException(nameof(mutacion)),
        };

        var original = ClaveDeCassette.Calcular(Cuerpo()).Clave;
        var otra = ClaveDeCassette.Calcular(mutado).Clave;

        if (mueve)
        {
            Assert.NotEqual(original, otra);
        }
        else
        {
            Assert.Equal(original, otra);
        }
    }

    // -------------------------------------------------- un campo que no está

    [Theory]
    [InlineData("""{"system":[{"type":"text","text":"x"}],"messages":[{"role":"user","content":"y"}]}""", "model")]
    [InlineData("""{"model":"m","messages":[{"role":"user","content":"y"}]}""", "system")]
    [InlineData("""{"model":"m","system":[{"type":"text","text":"x"}]}""", "messages")]
    public void Un_cuerpo_al_que_le_falta_un_campo_falla_nombrandolo(string cuerpo, string campo)
    {
        var falla = Assert.Throws<InvalidOperationException>(
            () => ClaveDeCassette.Calcular(cuerpo));

        // Ruidoso y no una clave sobre cadena vacía: un campo que deja de estar
        // —porque el formato del cable cambió— haría que todas las solicitudes
        // colapsaran a la misma clave y se sirvieran unas a otras.
        Assert.Contains(campo, falla.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_cuerpo_que_no_es_JSON_falla_y_no_devuelve_clave()
    {
        Assert.Throws<InvalidOperationException>(() => ClaveDeCassette.Calcular("no soy json"));
    }

    // ------------------------------------------------- lo que sale además de la clave

    [Fact]
    public void La_identidad_trae_el_hash_del_prefijo_y_el_modelo_para_el_sello()
    {
        var identidad = ClaveDeCassette.Calcular(Cuerpo());

        // El sello del cassette necesita los dos, y sacarlos del mismo lugar que la
        // clave evita que el archivo declare un modelo distinto del que lo produjo.
        Assert.Equal(Modelo, identidad.Modelo);
        Assert.Equal(ClaveDeCassette.HuellaDe(Prefijo), identidad.HashDelPrefijo);
    }

    [Fact]
    public void El_prefijo_se_lee_del_texto_del_bloque_y_no_de_su_JSON()
    {
        // Si la huella se calculara sobre el JSON crudo del bloque de sistema, un
        // cambio de formato del SDK —otro orden de claves, otro espaciado— la
        // movería sin que el prefijo hubiera cambiado, y todos los cassettes
        // quedarían sellados con un prefijo «ajeno» que nadie tocó.
        var conMarca = ClaveDeCassette.Calcular(Cuerpo()).HashDelPrefijo;
        var sinMarca = ClaveDeCassette.Calcular(Cuerpo(conCacheControl: false)).HashDelPrefijo;

        Assert.Equal(conMarca, sinMarca);
    }

    // ------------------------------------------------------------------ apoyo

    /// <summary>Un cuerpo con la forma que el adaptador manda a la API de mensajes.</summary>
    private static string Cuerpo(
        string prefijo = Prefijo,
        string mensaje = Mensaje,
        string modelo = Modelo,
        string? esfuerzo = "medium",
        int maximoDeTokens = 4000,
        bool conCacheControl = true)
    {
        var marca = conCacheControl
            ? ""","cache_control":{"type":"ephemeral"}"""
            : string.Empty;

        var salida = esfuerzo is null
            ? string.Empty
            : ""","output_config":{"effort":""" + Texto(esfuerzo) + "}";

        var cuerpo = new StringBuilder();
        cuerpo.Append("{\"model\":").Append(Texto(modelo));
        cuerpo.Append(",\"max_tokens\":").Append(maximoDeTokens);
        cuerpo.Append(",\"system\":[{\"type\":\"text\",\"text\":").Append(Texto(prefijo));
        cuerpo.Append(marca).Append("}]");
        cuerpo.Append(",\"messages\":[{\"role\":\"user\",\"content\":").Append(Texto(mensaje));
        cuerpo.Append("}]").Append(salida).Append('}');

        return cuerpo.ToString();
    }

    private static string Texto(string valor) => System.Text.Json.JsonSerializer.Serialize(valor);
}
