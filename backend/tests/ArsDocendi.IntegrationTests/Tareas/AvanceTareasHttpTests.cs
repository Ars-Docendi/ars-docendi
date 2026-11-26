using System.Net;
using System.Net.Http.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Tareas.Application;

namespace ArsDocendi.IntegrationTests.Tareas;

public sealed class AvanceTareasHttpTests(PostgresFixture postgres) : TareasHttpBase(postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task Avance(HttpClient cliente, TareaDto tarea, int porcentaje) =>
        Esperar(cliente.PatchVersionadoAsync($"/api/tareas/{tarea.Id}/avance", new { porcentajeAvance = porcentaje }, Ct), HttpStatusCode.OK);

    private static async Task<TareaDto> Leer(HttpClient cliente, TareaDto tarea) =>
        (await cliente.GetFromJsonAsync<TareaDto>($"/api/tareas/{tarea.Id}", Ct))!;

    [Fact]
    public async Task El_padre_promedia_a_sus_hijas_y_propaga_por_la_jerarquia()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var abuela = await Crear(cliente, NuevaTarea(Docente), Ct);
        await Avance(cliente, abuela, 30);

        // La primera hija reemplaza el valor manual por el promedio.
        var madre = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = abuela.Id }, Ct);
        var tia = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = abuela.Id }, Ct);
        var abuelaConHijas = await Leer(cliente, abuela);
        Assert.Equal(0, abuelaConHijas.PorcentajeAvance);
        Assert.True(abuelaConHijas.AvanceCalculado);

        await Avance(cliente, tia, 25);
        Assert.Equal(13, (await Leer(cliente, abuela)).PorcentajeAvance); // 12,5 redondea hacia arriba.

        var nieta = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = madre.Id }, Ct);
        await Avance(cliente, nieta, 100);
        Assert.Equal(100, (await Leer(cliente, madre)).PorcentajeAvance);
        var abuelaFinal = await Leer(cliente, abuela);
        Assert.Equal(63, abuelaFinal.PorcentajeAvance);
        Assert.Contains(abuelaFinal.Historial, e => e.Accion == "editar_avance" && e.Detalle == "63% (por sus tareas hijas)");
        Assert.False((await Leer(cliente, nieta)).AvanceCalculado);
    }

    [Fact]
    public async Task Una_tarea_con_hijas_vigentes_no_se_edita_a_mano_y_las_canceladas_no_cuentan()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var padre = await Crear(cliente, NuevaTarea(Docente), Ct);
        var completa = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = padre.Id }, Ct);
        var pendiente = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = padre.Id }, Ct);
        await Avance(cliente, completa, 100);
        Assert.Equal(50, (await Leer(cliente, padre)).PorcentajeAvance);

        await Esperar(
            cliente.PatchVersionadoAsync($"/api/tareas/{padre.Id}/avance", new { porcentajeAvance = 10 }, Ct),
            HttpStatusCode.UnprocessableEntity);

        await Cambiar(cliente, $"/api/tareas/{pendiente.Id}", new { estado = "cancelada" }, Ct);
        Assert.Equal(100, (await Leer(cliente, padre)).PorcentajeAvance);

        // Cancelada también la última hija, el avance vuelve a ser manual.
        await Cambiar(cliente, $"/api/tareas/{completa.Id}", new { estado = "cancelada" }, Ct);
        var sinHijasVigentes = await Leer(cliente, padre);
        Assert.False(sinHijasVigentes.AvanceCalculado);
        await Avance(cliente, padre, 40);
        Assert.Equal(40, (await Leer(cliente, padre)).PorcentajeAvance);
    }

    [Fact]
    public async Task Una_tarea_cerrada_no_se_recalcula_hasta_reabrirse()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var padre = await Crear(cliente, NuevaTarea(Docente), Ct);
        var hija = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = padre.Id }, Ct);
        await Avance(cliente, hija, 20);

        var resuelta = await Cambiar(cliente, $"/api/tareas/{padre.Id}", new { estado = "resuelta", solucion = "Listo" }, Ct);
        Assert.Equal(100, resuelta.PorcentajeAvance);

        await Avance(cliente, hija, 40);
        Assert.Equal(100, (await Leer(cliente, padre)).PorcentajeAvance);

        var reabierta = await Cambiar(cliente, $"/api/tareas/{padre.Id}", new { estado = "en_curso" }, Ct);
        Assert.Equal(40, reabierta.PorcentajeAvance);
    }

    [Fact]
    public async Task Resolver_una_hija_completa_su_avance_y_actualiza_al_padre_sin_cambiar_su_estado()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var padre = await Crear(cliente, NuevaTarea(Docente), Ct);
        var hija = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = padre.Id }, Ct);
        await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = padre.Id }, Ct);
        var estadoPadre = (await Leer(cliente, padre)).Estado;

        var resuelta = await Cambiar(cliente, $"/api/tareas/{hija.Id}", new { estado = "resuelta", solucion = "Listo" }, Ct);

        Assert.Equal(100, resuelta.PorcentajeAvance);
        Assert.Contains(resuelta.Historial, e => e.Accion == "editar_avance" && e.Detalle == "100%");
        var padreActual = await Leer(cliente, padre);
        Assert.Equal(50, padreActual.PorcentajeAvance);
        Assert.Equal(estadoPadre, padreActual.Estado);
    }
}
