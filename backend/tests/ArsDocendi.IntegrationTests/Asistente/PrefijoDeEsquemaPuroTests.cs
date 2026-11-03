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

}
