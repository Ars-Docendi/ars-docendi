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

    // ------------------------------------------- las tres direcciones, sobre el comparador

    [Fact]
    public void Una_arista_nueva_sin_fila_dispara_la_direccion_uno()
    {
        var (manifiesto, grafo) = Reales();
        var conAristaNueva = grafo with
        {
            Aristas = [.. grafo.Aristas, new AristaReal("Modules.Portal", "Modules.Tareas.Contracts")],
        };

        var desviaciones = ComparadorDeAristas.Comparar(manifiesto, conAristaNueva);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.AristaNoDeclarada);
        Assert.Equal("Modules.Portal -> Modules.Tareas.Contracts", detectada.Objeto);
    }

    [Fact]
    public void Una_fila_sin_arista_real_dispara_la_direccion_dos()
    {
        var (manifiesto, grafo) = Reales();
        var conFilaDePapel = manifiesto with
        {
            Aristas =
            [
                .. manifiesto.Aristas,
                new AristaDeclarada
                {
                    Origen = "ArsDocendi.Host",
                    Destino = "Modules.Aulas.Contracts",
                    Via = "project-reference",
                    Motivo = "DI / interfaces de composición",
                },
            ],
        };

        // Es literalmente la fila que la tabla del documento tenía y que ningún
        // .csproj referencia. Una fila de papel no protege nada: describe un grafo
        // que no existe.
        var desviaciones = ComparadorDeAristas.Comparar(conFilaDePapel, grafo);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.AristaDeclaradaInexistente);
        Assert.Equal("ArsDocendi.Host -> Modules.Aulas.Contracts", detectada.Objeto);
    }

    [Fact]
    public void Un_proyecto_nuevo_sin_clasificar_dispara_la_direccion_tres()
    {
        var (manifiesto, grafo) = Reales();
        var conProyectoNuevo = grafo with
        {
            Proyectos = [.. grafo.Proyectos, "Modules.Encuestas"],
        };

        // La misma dirección que atrapó __EFMigrationsHistory en el manifiesto de
        // privilegios, aplicada a proyectos: uno nuevo rompe el CI en vez de entrar
        // sin que nadie lo mire.
        var desviaciones = ComparadorDeAristas.Comparar(manifiesto, conProyectoNuevo);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.ProyectoSinClasificar);
        Assert.Equal("Modules.Encuestas", detectada.Objeto);
    }

    [Fact]
    public void Un_proyecto_declarado_que_ya_no_existe_dispara_la_direccion_tres()
    {
        var (manifiesto, grafo) = Reales();
        var sinElProyecto = grafo with
        {
            Proyectos = [.. grafo.Proyectos.Where(n => n != "Modules.Tareas.Contracts")],
            Aristas = [.. grafo.Aristas.Where(a => a.Destino != "Modules.Tareas.Contracts")],
        };

        var desviaciones = ComparadorDeAristas.Comparar(manifiesto, sinElProyecto);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.ProyectoDeclaradoInexistente);
        Assert.Equal("Modules.Tareas.Contracts", detectada.Objeto);
    }

    [Fact]
    public void Un_proyecto_huerfano_sin_motivo_dispara_una_desviacion()
    {
        var (manifiesto, grafo) = Reales();
        var sinMotivo = manifiesto with
        {
            Proyectos =
            [
                .. manifiesto.Proyectos.Select(p =>
                    p.EsHuerfano ? p with { Motivo = null } : p),
            ],
        };

        // El motivo es lo que hace que un huérfano sea una decisión visible y no un
        // proyecto que quedó ahí. Sin él, la fila solo dice que nadie lo referencia,
        // que es lo que ya se veía sin manifiesto.
        var desviaciones = ComparadorDeAristas.Comparar(sinMotivo, grafo);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.HuerfanoSinMotivo);
        Assert.Equal("Modules.Asistente.Contracts", detectada.Objeto);
    }

    [Fact]
    public void Un_proyecto_que_ninguna_arista_alcanza_declarado_activo_es_incoherente()
    {
        var (manifiesto, grafo) = Reales();
        var comoActivo = manifiesto with
        {
            Proyectos =
            [
                .. manifiesto.Proyectos.Select(p =>
                    p.EsHuerfano ? p with { Estado = "activo", Motivo = null } : p),
            ],
        };

        var desviaciones = ComparadorDeAristas.Comparar(comoActivo, grafo);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.EstadoDeProyectoIncoherente);
        Assert.Equal("Modules.Asistente.Contracts", detectada.Objeto);
    }

    // --------------------------------------------- las tres direcciones, contra el repo

    [Fact]
    public void El_manifiesto_no_se_desvia_del_grafo_real_de_backend_src()
    {
        var (manifiesto, grafo) = Reales();

        // El test que hace falsable al registro. Las tres direcciones a la vez sobre
        // el manifiesto y los .csproj de verdad: si alguien agrega una referencia sin
        // fila, borra una referencia que el manifiesto declara, o suma un proyecto,
        // esto se pone en rojo.
        var desviaciones = ComparadorDeAristas.Comparar(manifiesto, grafo);

        Assert.True(desviaciones.Count == 0, Describir(desviaciones));
    }

    // ------------------------------------------------------------------------ apoyo

    private static (ManifiestoDeAristas Manifiesto, GrafoDeProyectos Grafo) Reales() =>
        (ManifiestoDeAristas.Cargar(), LectorDeAristas.LeerBackendSrc());

    private static string Describir(IReadOnlyCollection<DesviacionDeArista> desviaciones) =>
        desviaciones.Count == 0
            ? string.Empty
            : $"{desviaciones.Count} desviación(es):\n" + string.Join("\n", desviaciones);

    private static void EscribirProyecto(string raiz, string subdirectorio, string nombre)
    {
        var directorio = Path.Combine(raiz, subdirectorio);
        Directory.CreateDirectory(directorio);
        File.WriteAllText(
            Path.Combine(directorio, $"{nombre}.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
    }
}
