using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la validación del término candidato de un rechazo (design.md D3
/// de asistente-rechazos-dinamicos).
/// </summary>
/// <remarks>
/// El modelo propone; acá se comprueba que el código re-copia — nunca la
/// cadena del modelo tal cual, siempre un span de lo que el usuario tipeó.
/// </remarks>
public sealed class TerminoDelRechazoTests
{
    [Fact]
    public void Un_termino_con_otra_capitalizacion_devuelve_la_del_usuario()
    {
        var termino = TerminoDelRechazo.Validar("Python", "¿qué docentes saben python?");

        Assert.Equal("python", termino);
    }

    [Fact]
    public void Un_termino_con_acento_distinto_devuelve_el_del_usuario()
    {
        var termino = TerminoDelRechazo.Validar(
            "calificación", "¿cuál es la calificacion de Pérez?");

        Assert.Equal("calificacion", termino);
    }

    [Fact]
    public void Un_fragmento_parcial_de_una_palabra_no_matchea()
    {
        // «pyth» es un prefijo de «python», no una palabra propia: sin el
        // límite de palabra, citaría un fragmento que el usuario no escribió.
        Assert.Null(TerminoDelRechazo.Validar("pyth", "¿qué docentes saben python?"));
    }

    [Fact]
    public void Un_termino_ausente_del_mensaje_no_matchea()
    {
        Assert.Null(TerminoDelRechazo.Validar("salarios docentes", "¿cuánto cobran?"));
    }

    [Fact]
    public void Un_termino_de_mas_de_cuatro_palabras_se_descarta()
    {
        Assert.Null(TerminoDelRechazo.Validar(
            "uno dos tres cuatro cinco",
            "pregunta con uno dos tres cuatro cinco palabras adentro"));
    }

    [Fact]
    public void Un_demostrativo_solo_se_descarta()
    {
        Assert.Null(TerminoDelRechazo.Validar("eso", "¿y eso?"));
    }

    [Fact]
    public void Un_termino_con_guillemet_se_descarta()
    {
        Assert.Null(TerminoDelRechazo.Validar("el «título»", "hablemos del «título» del trabajo"));
    }

    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_termino_vacio_o_ausente_no_matchea(string? candidato)
    {
        Assert.Null(TerminoDelRechazo.Validar(candidato, "cualquier mensaje"));
    }

    [Fact]
    public void Un_termino_de_un_solo_caracter_se_descarta()
    {
        Assert.Null(TerminoDelRechazo.Validar("x", "la variable x no aparece"));
    }

    [Fact]
    public void Un_termino_verbatim_dentro_de_una_frase_mas_larga_matchea()
    {
        var termino = TerminoDelRechazo.Validar(
            "sueldo docente", "¿me podés decir el sueldo docente de cada persona?");

        Assert.Equal("sueldo docente", termino);
    }

    [Fact]
    public void Un_termino_al_final_del_mensaje_matchea()
    {
        var termino = TerminoDelRechazo.Validar("guarani", "¿los datos están en guarani?");

        Assert.Equal("guarani", termino);
    }

    [Fact]
    public void Un_mensaje_vacio_no_tiene_donde_matchear()
    {
        Assert.Null(TerminoDelRechazo.Validar("python", ""));
    }
}
