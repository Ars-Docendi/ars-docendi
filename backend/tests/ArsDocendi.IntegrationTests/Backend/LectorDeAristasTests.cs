namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// El lado del código de la verificación: qué aristas hay de verdad en los
/// <c>.csproj</c>. Sin esto, el manifiesto sería una segunda tabla que tampoco
/// se compara con nada.
/// </summary>
public sealed class LectorDeAristasTests
{
    [Fact]
    public void El_barrido_encuentra_todos_los_proyectos_de_backend_src()
    {
        var grafo = LectorDeAristas.LeerBackendSrc();

        // Trece hoy: el Host, Shared, el núcleo del evaluador, los cinco módulos y
        // sus cinco Contracts. El número está escrito a propósito: si mañana el glob
        // deja de alcanzar una carpeta, esto se pone en rojo en vez de mirar de menos.
        Assert.Equal(13, grafo.Proyectos.Count);
        Assert.Contains("ArsDocendi.Evaluacion.Nucleo", grafo.Proyectos);
        Assert.Contains("Modules.Asistente.Contracts", grafo.Proyectos);
    }

    [Fact]
    public void El_barrido_encuentra_la_arista_que_el_glob_de_los_modulos_no_alcanza()
    {
        var grafo = LectorDeAristas.LeerBackendSrc();

        // `ArquitecturaIdentityTests` barre `Modules.*.csproj`, así que esta arista
        // —un proyecto que no es módulo referenciando el INTERNO de uno— le queda
        // fuera del alcance. Es la desviación que motivó el manifiesto.
        Assert.Contains(
            grafo.Aristas,
            arista => arista is { Origen: "ArsDocendi.Evaluacion.Nucleo", Destino: "Modules.Asistente" });
    }

    [Fact]
    public void Un_barrido_sin_ningun_csproj_falla_en_vez_de_devolver_cero_aristas()
    {
        var vacio = Path.Combine(Path.GetTempPath(), $"aristas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(vacio);

        try
        {
            // Un verificador que no distingue «no hay aristas sin declarar» de «no
            // miré ningún proyecto» pasa en verde justo el día que deja de mirar.
            var error = Assert.Throws<InvalidOperationException>(() => LectorDeAristas.Leer(vacio));
            Assert.Contains(vacio, error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(vacio, recursive: true);
        }
    }
}
