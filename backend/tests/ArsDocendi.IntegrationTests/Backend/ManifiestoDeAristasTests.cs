namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// Verificación del manifiesto de aristas contra el grafo real de <c>backend/src</c>.
/// </summary>
/// <remarks>
/// Vive junto a <c>ArquitecturaIdentityTests</c> porque es el mismo oficio: leer
/// archivos del repositorio y afirmar una frontera. No toca la base.
///
/// La carga se defiende primero: una fila incompleta o con una vía que el verificador
/// no sabe comprobar no llega al comparador, porque una fila que no se puede verificar
/// se lee como verificada.
/// </remarks>
public sealed class ManifiestoDeAristasTests
{
    // -------------------------------------------------------------- la carga se defiende

    [Fact]
    public void Una_arista_sin_motivo_no_carga_y_la_nombra()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ManifiestoDeAristas.Interpretar(
                """
                {
                  "version": 1,
                  "proyectos": [],
                  "aristas": [
                    { "origen": "ArsDocendi.Host", "destino": "ArsDocendi.Shared",
                      "via": "project-reference", "motivo": "" }
                  ]
                }
                """));

        // El motivo es la mitad del valor del registro: sin él la fila declara que
        // la arista existe y no por qué, que es exactamente lo que la tabla del
        // documento ya hacía.
        Assert.Contains("ArsDocendi.Host -> ArsDocendi.Shared", error.Message, StringComparison.Ordinal);
        Assert.Contains("motivo", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_arista_sin_origen_ni_destino_no_carga()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ManifiestoDeAristas.Interpretar(
                """
                {
                  "version": 1,
                  "proyectos": [],
                  "aristas": [
                    { "origen": "", "destino": "ArsDocendi.Shared",
                      "via": "project-reference", "motivo": "Utilidades" }
                  ]
                }
                """));

        Assert.Contains("origen", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_via_fuera_del_vocabulario_no_carga_y_nombra_la_arista_y_la_via()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ManifiestoDeAristas.Interpretar(
                """
                {
                  "version": 1,
                  "proyectos": [],
                  "aristas": [
                    { "origen": "Modules.Asistente", "destino": "Modules.Designaciones.Contracts",
                      "via": "di-por-interfaz", "motivo": "Carril determinista" }
                  ]
                }
                """));

        // Una vía que el verificador no sabe comprobar es una fila que se lee como
        // verificada sin serlo. El error nombra las dos cosas para que quede claro
        // que la salida es enseñarle al verificador, no ensanchar el vocabulario.
        Assert.Contains(
            "Modules.Asistente -> Modules.Designaciones.Contracts",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains("di-por-interfaz", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_estado_de_proyecto_fuera_del_vocabulario_no_carga()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ManifiestoDeAristas.Interpretar(
                """
                {
                  "version": 1,
                  "proyectos": [ { "nombre": "Modules.Asistente.Contracts", "estado": "pendiente" } ],
                  "aristas": []
                }
                """));

        Assert.Contains("Modules.Asistente.Contracts", error.Message, StringComparison.Ordinal);
        Assert.Contains("pendiente", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dos_proyectos_homonimos_hacen_fallar_el_barrido()
    {
        var raiz = Path.Combine(Path.GetTempPath(), $"homonimos-{Guid.NewGuid():N}");
        EscribirProyecto(raiz, Path.Combine("uno", "Modules.Aulas"), "Modules.Aulas");
        EscribirProyecto(raiz, Path.Combine("dos", "Modules.Aulas"), "Modules.Aulas");

        try
        {
            // La clave del manifiesto es el nombre del .csproj sin extensión. Con dos
            // homónimos la clave deja de identificar, y una clave que se degrada en
            // silencio no es una clave: la fila diría «Modules.Aulas» sin saber cuál.
            var error = Assert.Throws<InvalidOperationException>(() => LectorDeAristas.Leer(raiz));

            Assert.Contains("Modules.Aulas", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    private static void EscribirProyecto(string raiz, string subdirectorio, string nombre)
    {
        var directorio = Path.Combine(raiz, subdirectorio);
        Directory.CreateDirectory(directorio);
        File.WriteAllText(
            Path.Combine(directorio, $"{nombre}.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
    }
}
