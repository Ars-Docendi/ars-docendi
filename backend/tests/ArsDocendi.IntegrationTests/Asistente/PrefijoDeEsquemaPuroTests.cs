using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>Lo que se puede afirmar del prefijo del prompt <b>sin</b> una base de datos.</summary>
public sealed class PrefijoDeEsquemaPuroTests
{
    [Fact]
    public void Solo_se_enumeran_tablas_de_catalogo_sin_datos_personales()
    {
        // ESTE TEST ES LA FRONTERA. El vocabulario viaja entero al proveedor del modelo dentro del
        // prefijo, así que agregar acá `identity.personas.apellido` publicaría el padrón sin que nada falle.
        string[] admitidas = ["identity.carreras", "designaciones.cargos", "designaciones.dedicaciones"];

        Assert.All(
            LectorDeValoresDeCatalogo.CatalogosCerrados.Concat(LectorDeValoresDeCatalogo.CatalogosDelGlosario),
            c => Assert.Contains($"{c.Esquema}.{c.Tabla}", admitidas));
    }

    [Fact]
    public void Sin_el_glosario_la_lista_declarada_es_exactamente_la_de_siempre()
    {
        // EL DEFAULT NO PUEDE DERIVAR. La lista de siempre se escribe acá, a mano:
        // derivarla de la constante haría que editar la segunda lista cambiara el
        // prefijo de todos los proveedores y rompiera los cassettes sin que nada falle.
        (string, string, string)[] deSiempre =
        [
            ("identity", "carreras", "code"),
            ("identity", "carreras", "name"),
            ("designaciones", "cargos", "codigo"),
            ("designaciones", "cargos", "nombre"),
        ];

        Assert.Equal(deSiempre, LectorDeValoresDeCatalogo.Declaradas(conGlosario: false));
    }

    [Fact]
    public void Con_el_glosario_se_suman_las_dedicaciones_al_final()
    {
        var declaradas = LectorDeValoresDeCatalogo.Declaradas(conGlosario: true);

        // Al final, para que los cuatro valores de siempre conserven su posición.
        Assert.Equal(LectorDeValoresDeCatalogo.Declaradas(conGlosario: false), declaradas.Take(4));
        Assert.Equal(
            [("designaciones", "dedicaciones", "nombre"), ("designaciones", "dedicaciones", "codigo")],
            declaradas.Skip(4));
    }

    [Fact]
    public void Los_identificadores_declarados_son_simples()
    {
        // Los identificadores se interpolan en el SQL porque no pueden ir como parámetros.
        Assert.All(
            LectorDeValoresDeCatalogo.Declaradas(conGlosario: true),
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
        // ESTO PASÓ DE VERDAD. «dame la información de contacto de Marina Diaz» terminó en «No
        // encontré ningún registro» sobre una persona que está en el padrón: se guarda como «Díaz»
        // y `ILIKE` ignora mayúsculas pero NO tildes.
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
