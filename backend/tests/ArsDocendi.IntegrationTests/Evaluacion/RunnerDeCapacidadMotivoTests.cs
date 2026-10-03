using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using ArsDocendi.Evaluacion.Nucleo.Runner;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>
/// Verifica que el eje de capacidad reporte el motivo del rechazo como un dato
/// informativo, sin que altere el desenlace, la puntuación ni el gate de
/// regresión (design.md D10 de asistente-rechazos-dinamicos, tarea 8.2).
/// </summary>
public sealed class RunnerDeCapacidadMotivoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "eval_capacidad_motivo")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly SelloDeIdentidad Sello = new("p", "d", "f");

    [Fact]
    public async Task Un_motivo_declarado_distinto_del_aceptable_sigue_siendo_abstencion_correcta()
    {
        await SembrarAsync();
        var item = new ItemDeCapacidad(
            "cap-motivo-1", "¿alguna pregunta rara?", CategoriaDeItem.NoContestable, ActorDeItem.Global,
            SqlReferencia: null, OrdenImporta: false, MotivosAceptables: ["otro_sistema"]);

        var resultado = await EvaluarAsync(
            item, new ProveedorGuionado(ProveedorGuionado.NoContestableConMotivo("no_cubierto")));

        // El desenlace mide si se abstuvo, no si adivinó el motivo: un motivo
        // equivocado sigue siendo una abstención correcta.
        Assert.Equal(DesenlaceDeItem.AbstencionCorrecta, resultado.Desenlace);
        Assert.Equal("no_cubierto", resultado.MotivoDeclarado);
        Assert.False(resultado.MotivoDeAcuerdo);
    }

    [Fact]
    public async Task Un_motivo_declarado_coincidente_queda_de_acuerdo()
    {
        await SembrarAsync();
        var item = new ItemDeCapacidad(
            "cap-motivo-2", "¿alguna pregunta rara?", CategoriaDeItem.NoContestable, ActorDeItem.Global,
            SqlReferencia: null, OrdenImporta: false, MotivosAceptables: ["muy_general"]);

        var resultado = await EvaluarAsync(
            item, new ProveedorGuionado(ProveedorGuionado.NoContestableConMotivo("muy_general")));

        Assert.Equal(DesenlaceDeItem.AbstencionCorrecta, resultado.Desenlace);
        Assert.True(resultado.MotivoDeAcuerdo);
    }

    [Fact]
    public void Sin_motivos_aceptables_declarados_no_hay_nada_que_comparar()
    {
        var resultado = new ResultadoDeItem(
            "cap-x", CategoriaDeItem.NoContestable, DesenlaceDeItem.AbstencionCorrecta, "Se abstuvo.",
            "no_cubierto", MotivosAceptables: null);

        Assert.Null(resultado.MotivoDeAcuerdo);
    }

    [Fact]
    public void El_gate_no_ve_regresion_entre_corridas_que_solo_difieren_en_el_motivo()
    {
        var conMotivoA = new ResultadoDeItem(
            "cap-motivo-3", CategoriaDeItem.NoContestable, DesenlaceDeItem.AbstencionCorrecta, "x",
            "no_cubierto", ["otro_sistema"]);
        var conMotivoB = conMotivoA with { MotivoDeclarado = "fuera_de_tema" };

        var linea = LineaDeBase.De(new Reporte("capacidad", Sello, [conMotivoA], new Dictionary<string, int>()));
        var veredicto = GateDeRegresion.Comparar(
            new Reporte("capacidad", Sello, [conMotivoB], new Dictionary<string, int>()), linea);

        Assert.True(veredicto.Pasa);
        Assert.Empty(veredicto.Regresiones);
    }

    [Fact]
    public void El_reporte_incluye_la_seccion_informativa_del_motivo()
    {
        var resultado = new ResultadoDeItem(
            "cap-x", CategoriaDeItem.NoContestable, DesenlaceDeItem.AbstencionCorrecta, "Se abstuvo.",
            "no_cubierto", ["otro_sistema"]);

        var reporte = new Reporte("capacidad", Sello, [resultado], new Dictionary<string, int>());
        var texto = reporte.Renderizar();

        Assert.Contains("Motivo del rechazo (informativo)", texto, StringComparison.Ordinal);
        Assert.Contains("cap-x", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void El_reporte_de_otro_eje_no_muestra_la_seccion()
    {
        var resultado = new ResultadoDeItem(
            "dlg-1", "consulta_simple", DesenlaceDeItem.TraduccionCorrecta, "ok");

        var reporte = new Reporte("dialogo", Sello, [resultado], new Dictionary<string, int>());

        Assert.DoesNotContain(
            "Motivo del rechazo (informativo)", reporte.Renderizar(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ apoyo

    private async Task<ResultadoDeItem> EvaluarAsync(ItemDeCapacidad item, ProveedorGuionado proveedor)
    {
        var (basica, conDatosPersonales) = CadenasDeLectura();
        var opciones = Options.Create(new OpcionesAsistente());
        var contador = new ContadorDeLlamadasDelTurno(64);
        var conTecho = new ProveedorConTechoDeLlamadas(proveedor, contador);
        var ejecutor = new EjecutorDeConsulta(Apertura, ClasificadorDeSensibilidad(), opciones);

        CarrilSql Carril() => new(
            new GeneradorDeSql(
                new ProveedorDeEsquema(Apertura),
                new SelectorDeEjemplos(),
                conTecho,
                new FechaDeReferenciaFija(new DateOnly(2026, 3, 2)),
                Options.Create(new OpcionesAsistente()),
                NullLogger<GeneradorDeSql>.Instance),
            ejecutor,
            new ConsultorDeAlcance(Apertura),
            new RedactorDeRespuesta(conTecho, Options.Create(new OpcionesAsistente())),
            new ConsultorDeCobertura(Apertura),
            new BuscadorDeMenciones(Apertura),
            contador,
            new CatalogoDeCapacidades(
                Apertura,
                new ConsultorDeAlcance(Apertura),
                new SelectorDeEjemplos(),
                new CacheDeCapacidades(),
                new DisponibilidadDelModeloReal(
                    new AccesoAlAsistenteFalso(),
                    new CuotaDeActorFalsa(0, TimeProvider.System),
                    new PresupuestoOrganizacionalFalso(0),
                    new DisponibilidadDelModuloFalsa(),
                    new BreakerDelProveedor(
                        Options.Create(new OpcionesAsistente()), TimeProvider.System,
                        NullLogger<BreakerDelProveedor>.Instance)),
                new DisponibilidadDelModuloFalsa(),
                new CuotaDeActorFalsa(0, TimeProvider.System),
                new ConsultasIdentityFalsa(),
                NullLogger<CatalogoDeCapacidades>.Instance),
            NullLogger<CarrilSql>.Instance);

        var runner = new RunnerDeCapacidad(Carril, ejecutor, new ActoresFijos(), proveedor);

        return await runner.EvaluarAsync(item, TestContext.Current.CancellationToken);
    }

    private sealed class ActoresFijos : IResolutorDeActores
    {
        public Guid Resolver(string actor) => Secretaria;
    }
}
