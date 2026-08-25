using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el hilo conversacional y su almacén.
/// </summary>
/// <remarks>
/// Todo en memoria, con un reloj movible: verificar la expiración contra el reloj
/// real sería un test lento o mentiroso, y probablemente las dos cosas.
/// </remarks>
public sealed class HiloConversacionalTests
{
    private static readonly Guid Ana = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Luis = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly DateTimeOffset Inicio = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------- propiedad

    [Fact]
    public void El_actor_que_lo_abrio_lo_recupera()
    {
        var (almacen, _) = Almacen();

        var abierto = almacen.Resolver(null, Ana);
        abierto.Agregar("¿Qué carreras hay?", Inicio);

        var recuperado = almacen.Resolver(abierto.Id, Ana);

        Assert.Equal(abierto.Id, recuperado.Id);
        Assert.Single(recuperado.Turnos);
    }

    [Fact]
    public void Un_hilo_ajeno_se_rechaza()
    {
        // Falla en vez de devolver uno nuevo en silencio: o es un error del
        // cliente, o es alguien probando identificadores. Las dos se quieren ver.
        var (almacen, _) = Almacen();
        var deAna = almacen.Resolver(null, Ana);
        deAna.Agregar("¿Qué carreras hay?", Inicio);

        var excepcion = Assert.Throws<HiloAjeno>(() => almacen.Resolver(deAna.Id, Luis));

        Assert.Equal(deAna.Id, excepcion.Hilo);
    }

    [Fact]
    public void Un_identificador_inexistente_arranca_un_hilo_nuevo()
    {
        var (almacen, _) = Almacen();

        var resuelto = almacen.Resolver(Guid.NewGuid(), Ana);

        Assert.Empty(resuelto.Turnos);
        Assert.Equal(Ana, resuelto.Actor);
    }

    // ------------------------------------------------------------ expiración

    [Fact]
    public void Un_hilo_inactivo_deja_de_resolverse()
    {
        var (almacen, reloj) = Almacen(vigenciaMinutos: 120);
        var abierto = almacen.Resolver(null, Ana);
        abierto.Agregar("¿Qué carreras hay?", Inicio);

        reloj.Avanzar(TimeSpan.FromMinutes(121));

        Assert.Empty(almacen.Resolver(abierto.Id, Ana).Turnos);
    }

    [Fact]
    public void Un_turno_nuevo_renueva_la_vigencia()
    {
        var (almacen, reloj) = Almacen(vigenciaMinutos: 120);
        var abierto = almacen.Resolver(null, Ana);
        abierto.Agregar("¿Qué carreras hay?", Inicio);

        reloj.Avanzar(TimeSpan.FromMinutes(100));
        var mismo = almacen.Resolver(abierto.Id, Ana);
        mismo.Agregar("¿Y qué materias?", reloj.GetUtcNow());

        reloj.Avanzar(TimeSpan.FromMinutes(100));

        Assert.Equal(2, almacen.Resolver(abierto.Id, Ana).Turnos.Count);
    }

    [Fact]
    public void Los_hilos_vencidos_se_purgan()
    {
        var (almacen, reloj) = Almacen(vigenciaMinutos: 60);
        almacen.Resolver(null, Ana);
        almacen.Resolver(null, Luis);

        reloj.Avanzar(TimeSpan.FromMinutes(61));
        almacen.Resolver(null, Ana);

        // Los dos viejos salieron y quedó solo el recién creado.
        Assert.Equal(1, almacen.Vivos);
    }

    // -------------------------------------------------------------- recorte

    [Fact]
    public void El_historial_vigente_respeta_el_tope()
    {
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);
        for (var indice = 1; indice <= 6; indice++)
        {
            hilo.Agregar($"pregunta {indice}", Inicio);
        }

        var vigente = hilo.HistorialVigente(tope: 4);

        Assert.Equal(4, vigente.Count);
        Assert.Equal("pregunta 3", vigente[0].Pregunta);
        Assert.Equal("pregunta 6", vigente[^1].Pregunta);
    }

    [Fact]
    public void Al_soltar_el_tema_el_historial_vigente_queda_vacio()
    {
        // El ancla es el inicio del SEGMENTO, no el turno cero. Anclar para siempre
        // el primero arrastra contexto muerto de temas ya soltados.
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);
        hilo.Agregar("¿Qué docentes dan Álgebra?", Inicio);
        hilo.Agregar("¿Y en Sistemas?", Inicio);

        hilo.SoltarElTema();

        Assert.Empty(hilo.HistorialVigente(tope: 4));

        // No se borró nada: el historial completo sigue ahí.
        Assert.Equal(2, hilo.Turnos.Count);
    }

    [Fact]
    public void Despues_de_soltar_el_tema_el_segmento_nuevo_acumula()
    {
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);
        hilo.Agregar("viejo 1", Inicio);
        hilo.Agregar("viejo 2", Inicio);
        hilo.SoltarElTema();
        hilo.Agregar("nuevo 1", Inicio);

        var vigente = hilo.HistorialVigente(tope: 4);

        Assert.Single(vigente);
        Assert.Equal("nuevo 1", vigente[0].Pregunta);
    }

    [Fact]
    public void Un_tope_de_cero_devuelve_historial_vacio()
    {
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);
        hilo.Agregar("pregunta", Inicio);

        Assert.Empty(hilo.HistorialVigente(tope: 0));
    }

    // ------------------------------------------------------------ aclaración

    [Fact]
    public void La_aclaracion_pendiente_se_deja_y_se_cierra()
    {
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);
        var aclaracion = new Aclaracion("Álgebra", "¿Quién da Álgebra?", [new("A", "a"), new("B", "b")]);

        hilo.Pendiente(aclaracion);
        Assert.NotNull(hilo.AclaracionPendiente);

        hilo.CerrarAclaracion();
        Assert.Null(hilo.AclaracionPendiente);
    }

    [Fact]
    public void La_aclaracion_cuenta_sus_reintentos_y_se_agota()
    {
        var aclaracion = new Aclaracion("Álgebra", "¿Quién da Álgebra?", [new("A", "a"), new("B", "b")]);

        Assert.False(aclaracion.Agotada(maximo: 2));
        aclaracion.Fallo();
        Assert.False(aclaracion.Agotada(maximo: 2));
        aclaracion.Fallo();
        Assert.True(aclaracion.Agotada(maximo: 2));
    }

    [Fact]
    public void El_texto_del_menu_numera_las_opciones()
    {
        var aclaracion = new Aclaracion(
            "Análisis Matemático",
            "¿Quién da Análisis Matemático?",
            [new("Ingeniería en Informática", "x"), new("Ingeniería Industrial", "y")]);

        var texto = aclaracion.Texto();

        Assert.Contains("1. Ingeniería en Informática", texto, StringComparison.Ordinal);
        Assert.Contains("2. Ingeniería Industrial", texto, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ apoyo

    private static (AlmacenDeHilosEnMemoria Almacen, RelojFijo Reloj) Almacen(
        int vigenciaMinutos = 120)
    {
        var reloj = new RelojFijo(Inicio);
        var opciones = Options.Create(
            new OpcionesAsistente { VigenciaDelHiloMinutos = vigenciaMinutos });

        return (new AlmacenDeHilosEnMemoria(opciones, reloj), reloj);
    }
}
