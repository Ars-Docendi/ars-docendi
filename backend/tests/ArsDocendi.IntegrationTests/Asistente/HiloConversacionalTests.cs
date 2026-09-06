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

    // ------------------------------------- la consulta que respondió el turno

    // EL HILO GUARDA LA CONSULTA, NUNCA LAS FILAS. La consulta se puede guardar
    // porque sus literales salen de la pregunta del usuario —que el hilo ya
    // guardaba—; ninguna fila leída de la base aparece en ella. Ver la enmienda a
    // D1 en `asistente-capa-conversacional/design.md`.

    [Fact]
    public void Un_turno_respondido_anota_la_consulta_que_lo_respondio()
    {
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);

        hilo.Agregar("¿Qué materias hay?", Inicio, "SELECT 1");

        Assert.Equal("SELECT 1", Assert.Single(hilo.Turnos).SqlEjecutado);
    }

    [Fact]
    public void Un_turno_sin_filas_no_anota_ninguna_consulta()
    {
        // Nulo no es «no se generó consulta»: es «no hay nada que continuar».
        // Ofrecerle al modelo una consulta que no encontró nada lo invita a
        // repetirla, y el turno siguiente hereda el error del anterior.
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);

        hilo.Agregar("¿Qué materias hay?", Inicio);

        Assert.Null(Assert.Single(hilo.Turnos).SqlEjecutado);
        Assert.Empty(hilo.ConsultasVigentes(tope: 4));
    }

    [Fact]
    public void Las_consultas_vigentes_van_de_la_mas_vieja_a_la_mas_reciente()
    {
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);

        hilo.Agregar("primera", Inicio, "SELECT 1");
        hilo.Agregar("segunda", Inicio, "SELECT 2");

        Assert.Equal(["SELECT 1", "SELECT 2"], hilo.ConsultasVigentes(tope: 4));
    }

    [Fact]
    public void Un_turno_vacio_en_el_medio_no_corta_el_arrastre()
    {
        // El turno sin filas no aporta consulta, pero tampoco invalida las de
        // antes: la conversación sigue siendo la misma.
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);

        hilo.Agregar("primera", Inicio, "SELECT 1");
        hilo.Agregar("no encontró nada", Inicio);
        hilo.Agregar("tercera", Inicio, "SELECT 3");

        Assert.Equal(["SELECT 1", "SELECT 3"], hilo.ConsultasVigentes(tope: 4));
    }

    [Fact]
    public void El_pivote_suelta_las_consultas_igual_que_suelta_las_preguntas()
    {
        // No hay una segunda regla de recorte: `ConsultasVigentes` se deriva de
        // `HistorialVigente`. Si alguien la reimplementara aparte, este test es el
        // que se rompe cuando las dos se desincronicen.
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);
        hilo.Agregar("antes del pivote", Inicio, "SELECT 1");

        hilo.SoltarElTema();

        Assert.Empty(hilo.ConsultasVigentes(tope: 4));
    }

    [Fact]
    public void El_tope_acota_cuantas_consultas_viajan()
    {
        var hilo = new HiloConversacional(Guid.NewGuid(), Ana);

        for (var indice = 1; indice <= 5; indice++)
        {
            hilo.Agregar($"pregunta {indice}", Inicio, $"SELECT {indice}");
        }

        // Las más recientes, no las primeras: lo que se continúa es lo último.
        Assert.Equal(["SELECT 3", "SELECT 4", "SELECT 5"], hilo.ConsultasVigentes(tope: 3));
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
