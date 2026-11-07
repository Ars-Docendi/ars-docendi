using System.Net;
using System.Net.Http.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Tareas.Application;

namespace ArsDocendi.IntegrationTests.Tareas;

public sealed class VinculoHijasTareasHttpTests(PostgresFixture postgres) : TareasHttpBase(postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<TareaDto> Leer(HttpClient cliente, TareaDto tarea) =>
        (await cliente.GetFromJsonAsync<TareaDto>($"/api/tareas/{tarea.Id}", Ct))!;

    private static async Task<HttpResponseMessage> Vincular(HttpClient cliente, TareaDto padre, TareaDto hija)
    {
        var actual = await Leer(cliente, hija);
        return await cliente.PostAsJsonAsync($"/api/tareas/{padre.Id}/hijas", new { tareaId = hija.Id, version = actual.Version }, Ct);
    }

    private static async Task<HttpResponseMessage> Desvincular(HttpClient cliente, TareaDto padre, TareaDto hija)
    {
        var actual = await Leer(cliente, hija);
        return await cliente.DeleteAsync($"/api/tareas/{padre.Id}/hijas/{hija.Id}?version={actual.Version}", Ct);
    }

    [Fact]
    public async Task Vincular_hereda_el_proyecto_con_sus_descendientes_y_actualiza_el_avance()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var padre = await Crear(cliente, NuevaTarea(Docente) with { ProyectoId = ProyectoTesting }, Ct);
        var existente = await Crear(cliente, NuevaTarea(Docente), Ct);
        var nieta = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = existente.Id }, Ct);
        await Esperar(cliente.PatchVersionadoAsync($"/api/tareas/{nieta.Id}/avance", new { porcentajeAvance = 80 }, Ct), HttpStatusCode.OK);

        using var respuesta = await Vincular(cliente, padre, existente);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var padreActual = await ReadAsync<TareaDto>(respuesta, Ct);
        Assert.Equal(80, padreActual.PorcentajeAvance);
        Assert.True(padreActual.AvanceCalculado);
        Assert.Contains(padreActual.Historial, e => e.Detalle == $"Vinculó como hija la tarea N° {existente.Numero}");
        var hijaActual = await Leer(cliente, existente);
        Assert.Equal(padre.Id, hijaActual.TareaPadreId);
        Assert.Equal(ProyectoTesting, hijaActual.ProyectoId);
        Assert.Equal(ProyectoTesting, (await Leer(cliente, nieta)).ProyectoId);
    }

    [Fact]
    public async Task Desvincular_conserva_el_proyecto_y_recalcula_al_padre()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var padre = await Crear(cliente, NuevaTarea(Docente) with { ProyectoId = ProyectoTesting }, Ct);
        var queda = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = padre.Id }, Ct);
        var sale = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = padre.Id }, Ct);
        await Esperar(cliente.PatchVersionadoAsync($"/api/tareas/{sale.Id}/avance", new { porcentajeAvance = 100 }, Ct), HttpStatusCode.OK);
        Assert.Equal(50, (await Leer(cliente, padre)).PorcentajeAvance);

        using var respuesta = await Desvincular(cliente, padre, sale);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(0, (await ReadAsync<TareaDto>(respuesta, Ct)).PorcentajeAvance);
        var liberada = await Leer(cliente, sale);
        Assert.Null(liberada.TareaPadreId);
        Assert.Equal(ProyectoTesting, liberada.ProyectoId);
        using var ajena = await Desvincular(cliente, padre, sale);
        Assert.Equal(HttpStatusCode.NotFound, ajena.StatusCode);
        Assert.Equal(padre.Id, (await Leer(cliente, queda)).TareaPadreId);
    }

    [Fact]
    public async Task Vincular_rechaza_ciclos_otra_madre_canceladas_y_a_si_misma()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var raiz = await Crear(cliente, NuevaTarea(Docente), Ct);
        var hija = await Crear(cliente, NuevaTarea(Docente) with { TareaPadreId = raiz.Id }, Ct);
        var otra = await Crear(cliente, NuevaTarea(Docente), Ct);
        var cancelada = await Crear(cliente, NuevaTarea(Docente), Ct);
        await Cambiar(cliente, $"/api/tareas/{cancelada.Id}", new { estado = "cancelada" }, Ct);

        using var ciclo = await Vincular(cliente, hija, raiz);
        using var conPadre = await Vincular(cliente, otra, hija);
        using var deCancelada = await Vincular(cliente, otra, cancelada);
        using var aSiMisma = await Vincular(cliente, otra, otra);

        Assert.All([ciclo, conPadre, deCancelada, aSiMisma],
            r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));
        Assert.Null((await Leer(cliente, raiz)).TareaPadreId);
        Assert.Equal(raiz.Id, (await Leer(cliente, hija)).TareaPadreId);
    }

    [Fact]
    public async Task Solo_la_autoridad_creadora_del_padre_vincula_y_la_version_debe_coincidir()
    {
        await SembrarAsync(Ct);
        using var host = CrearHost();
        using var autoridad = Cliente(host, Decanato, "decanato");
        using var otraAutoridad = Cliente(host, Secretaria, "secretaria");
        using var docente = Cliente(host, Docente, "docente");
        var padre = await Crear(autoridad, NuevaTarea(Docente), Ct);
        var hija = await Crear(autoridad, NuevaTarea(Docente), Ct);

        using var ajena = await Vincular(otraAutoridad, padre, hija);
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);
        using var versionVieja = await autoridad.PostAsJsonAsync($"/api/tareas/{padre.Id}/hijas", new { tareaId = hija.Id, version = hija.Version + 1 }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, versionVieja.StatusCode);
        using var sinPermiso = await docente.PostAsJsonAsync($"/api/tareas/{padre.Id}/hijas", new { tareaId = hija.Id, version = hija.Version }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);
        Assert.Null((await Leer(autoridad, hija)).TareaPadreId);
    }
}
