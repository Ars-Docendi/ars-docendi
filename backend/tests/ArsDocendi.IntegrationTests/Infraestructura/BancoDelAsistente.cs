using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Arma la capa conversacional completa con un proveedor guionado.
/// </summary>
/// <remarks>
/// Reproduce los alcances de la registración real, y esa es la única razón por la
/// que existe en vez de tres líneas en cada test: el contador de llamadas es <b>por
/// turno</b> y el breaker, la cuota y el almacén de hilos son <b>del proceso</b>.
/// Un banco que compartiera el contador entre turnos haría que el segundo turno
/// heredara el conteo del primero, y la cuota —que se cobra con ese número— cobraría
/// de más acumulativamente.
///
/// Por eso <see cref="Capa"/> es una fábrica: cada llamada arma el turno de nuevo,
/// como hace el contenedor con un request.
/// </remarks>
internal sealed class BancoDelAsistente
{
    public required Func<CapaConversacional> Fabrica { get; init; }

    /// <summary>El proveedor guionado, con el registro de lo que se le pidió.</summary>
    public required ProveedorGuionado Proveedor { get; init; }

    /// <summary>El almacén de hilos, compartido entre turnos.</summary>
    public required IAlmacenDeHilos Hilos { get; init; }

    /// <summary>El breaker, compartido entre turnos.</summary>
    public required BreakerDelProveedor Breaker { get; init; }

    /// <summary>La cuota, compartida entre turnos.</summary>
    public required ICuotaDelActor Cuota { get; init; }

    /// <summary>La configuración con la que se armó.</summary>
    public required OpcionesAsistente Opciones { get; init; }

    /// <summary>El registro del turno, sea el de memoria o el real.</summary>
    public required IRegistroDelTurno Registro { get; init; }

    /// <summary>Un turno nuevo, con su propio contador de llamadas.</summary>
    public CapaConversacional Capa() => Fabrica();

    /// <summary>Arma el banco contra una base de prueba.</summary>
    public static BancoDelAsistente Armar(
        CadenaSoloLectura basica,
        CadenaSoloLecturaPii conDatosPersonales,
        IClasificadorDeSensibilidad clasificador,
        OpcionesAsistente? configuracion = null,
        IAlmacenDeHilos? hilos = null,
        TimeProvider? reloj = null,
        ProveedorGuionado? proveedor = null,
        IRegistroDelTurno? registro = null,
        Func<IProveedorDeModelo, IProveedorDeModelo>? envolver = null,
        params string[] guion)
    {
        var valores = configuracion ?? new OpcionesAsistente();
        var opciones = Options.Create(valores);
        var elReloj = reloj ?? TimeProvider.System;
        var elProveedor = proveedor ?? new ProveedorGuionado(guion);

        // El envoltorio permite meter un medidor entre el guionado y el pipeline. El
        // eje social lo necesita: afirma «cero tokens de entrada», y ese número sale
        // del transporte, no del resultado del turno.
        var visto = envolver is null ? (IProveedorDeModelo)elProveedor : envolver(elProveedor);

        var breaker = new BreakerDelProveedor(
            opciones, elReloj, NullLogger<BreakerDelProveedor>.Instance);

        var cuota = new CuotaEnMemoria(opciones, elReloj);
        var disponibilidad = new DisponibilidadDelModeloReal(cuota, breaker);
        var losHilos = hilos ?? new AlmacenDeHilosEnMemoria(opciones, elReloj);
        var elRegistro = registro ?? new RegistroEnMemoria();

        // El índice se comparte entre turnos, igual que en producción: es un caché,
        // y uno por turno no cachearía nada.
        var indice = new IndiceDeEntidades(basica);

        // El catálogo de capacidades también se comparte: cachea por rol, y uno por
        // turno no cachearía nada.
        var catalogo = new CatalogoDeCapacidades(
            basica,
            conDatosPersonales,
            new ConsultorDeAlcance(basica),
            new SelectorDeEjemplos(),
            new CacheDeCapacidades(),
            NullLogger<CatalogoDeCapacidades>.Instance);

        // El enrutador de dominio se comparte por el mismo motivo que el índice: su
        // catálogo del dominio es un caché, y uno por turno no cachearía nada. Está
        // en MODO SOMBRA —decide y el turno sigue por SQL igual—, así que agregarlo
        // acá no cambia ninguna respuesta esperada de los tests que ya existían.
        var enrutador = new EnrutadorDeDominio(
            new ResolutorDeIntenciones(
                CatalogoDeIntenciones.Cargar(),
                new CatalogoDelDominioReal(indice, basica)),
            NullLogger<EnrutadorDeDominio>.Instance);

        return new BancoDelAsistente
        {
            Proveedor = elProveedor,
            Hilos = losHilos,
            Breaker = breaker,
            Cuota = cuota,
            Opciones = valores,
            Registro = elRegistro,
            Fabrica = () =>
            {
                var contador = new ContadorDeLlamadasDelTurno(valores.MaximoDeLlamadasPorTurno);

                // Por turno, igual que el contador: es la decisión de ESTE turno la
                // que tiene que llegar a su fila del registro.
                var decisionSombra = new DecisionSombraDelTurno();

                var conBreaker = new ProveedorConBreaker(
                    visto,
                    breaker,
                    TimeSpan.FromSeconds(valores.TimeoutDeLlamadaSegundos),
                    elReloj);

                var conTecho = new ProveedorConTechoDeLlamadas(conBreaker, contador);

                var carril = new CarrilSql(
                    new GeneradorDeSql(
                        new ProveedorDeEsquema(basica, conDatosPersonales),
                        new SelectorDeEjemplos(),
                        conTecho,
                        new FechaDeReferenciaFija(new DateOnly(2026, 8, 25)),
                        opciones,
                        NullLogger<GeneradorDeSql>.Instance),
                    new EjecutorDeConsulta(basica, conDatosPersonales, clasificador, opciones),
                    new ConsultorDeAlcance(basica),
                    new RedactorDeRespuesta(conTecho, Options.Create(new OpcionesAsistente())),
                    new SelectorDeEjemplos(),
                    contador,
                    NullLogger<CarrilSql>.Instance);

                return new CapaConversacional(
                    losHilos,
                    indice,
                    new ReescritorDePreguntas(conTecho, Options.Create(new OpcionesAsistente())),
                    carril,
                    new SelectorDeEjemplos(),
                    catalogo,
                    enrutador,
                    conTecho,
                    elRegistro,
                    disponibilidad,
                    cuota,
                    contador,
                    decisionSombra,
                    opciones,
                    elReloj,
                    NullLogger<CapaConversacional>.Instance);
            },
        };
    }
}
