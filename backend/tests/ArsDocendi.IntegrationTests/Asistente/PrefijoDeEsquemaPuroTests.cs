using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Lo que se puede afirmar del prefijo del prompt <b>sin</b> una base de datos.
/// </summary>
/// <remarks>
/// Cuatro aserciones sobre constantes del ensamblado: la lista de catálogos
/// cerrados que se enumeran en el prefijo, la forma de sus identificadores, y dos
/// reglas del texto de instrucciones. Ninguna necesita que exista una tabla.
///
/// Estaban en <see cref="PrefijoDeEsquemaTests"/>, que hereda de
/// <c>ClasePostgresAislada</c>: cada una provisionaba una base entera para leer
/// una constante. Lo que sí necesita la base —que los <c>COMMENT ON</c> existan,
/// que el prefijo los lleve, que los dos roles difieran en las columnas
/// personales— se queda allá.
/// </remarks>
public sealed class PrefijoDeEsquemaPuroTests
{
    [Fact]
    public void Solo_se_enumeran_tablas_de_catalogo_sin_datos_personales()
    {
        // ESTE TEST ES LA FRONTERA. El vocabulario viaja entero al proveedor del
        // modelo dentro del prefijo, así que agregar acá `identity.personas.apellido`
        // publicaría el padrón sin que nada falle. La lista de tablas admitidas se
        // escribe acá y no se deriva de la otra, a propósito: derivarla haría que
        // ampliar una ampliara la otra sola.
        string[] admitidas = ["identity.carreras", "designaciones.cargos"];

        Assert.All(
            LectorDeValoresDeCatalogo.CatalogosCerrados,
            c => Assert.Contains($"{c.Esquema}.{c.Tabla}", admitidas));
    }

    [Fact]
    public void Los_identificadores_declarados_son_simples()
    {
        // Los identificadores se interpolan en el SQL porque no pueden ir como
        // parámetros. Salen de una constante del ensamblado y nunca de una pregunta,
        // pero eso lo garantiza la lectura del código y esto lo garantiza la suite.
        Assert.All(
            LectorDeValoresDeCatalogo.CatalogosCerrados,
            c => Assert.All(
                new[] { c.Esquema, c.Tabla, c.Columna },
                identificador => Assert.Matches("^[a-z_][a-z0-9_]*$", identificador)));
    }

    [Fact]
    public void El_prompt_prohibe_copiar_las_palabras_del_usuario_al_filtro()
    {
        // La regla cubre lo que la enumeración no: las materias no son un catálogo
        // cerrado —un departamento suma materias— así que para ésas la salida es
        // comparar por la palabra distintiva en vez de por igualdad.
        Assert.Contains(
            "VALORES POSIBLES", InstruccionesDeGeneracion.Instrucciones, StringComparison.Ordinal);
        Assert.Contains(
            "ILIKE", InstruccionesDeGeneracion.Instrucciones, StringComparison.Ordinal);
    }

    [Fact]
    public void El_prompt_manda_ignorar_las_tildes_al_comparar_texto_libre()
    {
        // ESTO PASÓ DE VERDAD. «dame la información de contacto de Marina Diaz»
        // terminó en «No encontré ningún registro» sobre una persona que está en el
        // padrón: se guarda como «Díaz» y `ILIKE` ignora mayúsculas pero NO tildes.
        //
        // Y no falló en silencio: falló AFIRMANDO. El actor era global, así que el
        // vacío se narró como inexistencia, y la misma pregunta reformulada dos
        // turnos después devolvió el teléfono y el correo. Dos respuestas
        // contradictorias sobre el mismo dato, y la primera dicha con seguridad.
        //
        // `public.unaccent` ya estaba instalada y concedida —`001_asistente_grants.sql`
        // la crea con un comentario que dice para qué—, y el prompt nunca la nombró:
        // provisión muerta contra exactamente el defecto que existía para evitar.
        var instrucciones = InstruccionesDeGeneracion.Instrucciones;

        // LAS DOS MITADES, y por eso se cuenta en vez de buscar la palabra. Con
        // `public.unaccent` sólo sobre la columna, «Diaz» sigue sin encontrar a
        // «Díaz»: el literal del usuario también llega con lo que el usuario tipeó.
        var veces = instrucciones.Split("public.unaccent", StringSplitOptions.None).Length - 1;

        Assert.True(
            veces >= 2,
            $"El prompt nombra `public.unaccent` {veces} vez/veces; hace falta a los dos "
            + "lados de la comparación, sobre la columna y sobre el literal.");
        Assert.Contains(
            "tilde", instrucciones, StringComparison.OrdinalIgnoreCase);
    }
}
