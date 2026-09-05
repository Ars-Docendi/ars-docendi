using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Application;

/// <summary>
/// Convierte una serie de consultas sueltas en una conversación.
/// </summary>
/// <remarks>
/// Va <b>encima</b> del carril SQL y no adentro. Esa separación es lo que deja
/// intactos el prefijo cacheado, el validador y los datasets:
/// <see cref="CarrilSql.ResponderAsync"/> ya aceptaba una pregunta autocontenida, y
/// lo que esta capa hace es calcularla.
///
/// El orden del pipeline no es de conveniencia: cada posición tiene un motivo, y
/// están anotados en cada paso.
///
/// Casi todo cuesta cero tokens. La única llamada al modelo que agrega es el
/// reescritor, y solo cuando hay historial vigente.
/// </remarks>
public sealed class CapaConversacional(
    IAlmacenDeHilos hilos,
    IIndiceDeEntidades indice,
    ReescritorDePreguntas reescritor,
    CarrilSql carril,
    ISelectorDeEjemplos ejemplos,
    ICatalogoDeCapacidades capacidades,
    EnrutadorDeDominio enrutador,
    IProveedorDeModelo proveedor,
    IRegistroDelTurno registro,
    IDisponibilidadDelModelo disponibilidad,
    ICuotaDelActor cuota,
    ContadorDeLlamadasDelTurno contador,
    DecisionSombraDelTurno decisionSombra,
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj,
    ILogger<CapaConversacional> log)
{
    /// <summary>Responde un turno dentro de un hilo.</summary>
    /// <param name="actor">El usuario autenticado.</param>
    /// <param name="hilo">
    /// El hilo que trajo el cliente. Nulo en el primer turno; uno vencido o
    /// inexistente arranca uno nuevo sin error.
    /// </param>
    /// <param name="mensaje">Lo que escribió el usuario.</param>
    /// <exception cref="HiloAjeno">Si el hilo pertenece a otro actor.</exception>
    public async Task<ResultadoDelTurno> ResponderAsync(
        Guid actor, Guid? hilo, string mensaje, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);

        var valores = opciones.Value;

        // LA COTA DEL TURNO. Una sola, punta a punta, encadenada al token del
        // request. No es la suma de los timeouts de las etapas: cuatro llamadas de
        // diez segundos son cuarenta segundos de espera y cada una habría respetado
        // su límite.
        using var presupuesto = PresupuestoDelTurno.Abrir(
            ct, TimeSpan.FromSeconds(valores.PresupuestoDelTurnoSegundos), reloj);

        var conversacion = hilos.Resolver(hilo, actor);
        var arranco = reloj.GetUtcNow();

        try
        {
            var turno = await ResolverAsync(
                actor, conversacion, mensaje, valores, presupuesto.Token);

            await RegistrarAsync(actor, mensaje, turno, arranco, ct);

            return turno;
        }
        catch (OperationCanceledException) when (presupuesto.Vencio)
        {
            // Se acabó el tiempo del turno. Para quien preguntó, «no llegué a
            // tiempo» es una respuesta; una cancelación cruda no lo es.
            log.LogWarning(
                "El turno del asistente agotó su presupuesto de {Segundos}s.",
                valores.PresupuestoDelTurnoSegundos);

            var turno = Degradado(conversacion, PoliticaDeAbstencion.TextoServicioDegradado);
            await RegistrarAsync(actor, mensaje, turno, arranco, ct);

            return turno;
        }
        catch (Exception excepcion) when (!presupuesto.Vencio)
        {
            // El turno se cayó. La cuota ya lo cobra en el `finally` de abajo, así
            // que sin esta rama el actor pagaba llamadas que no aparecían en ningún
            // lado: el registro operativo —la única fuente para «cuántas veces
            // falló»— sub-contaba justo las fallas duras.
            //
            // Se registra y se relanza: quien llamó sigue viendo la excepción y el
            // contrato HTTP no cambia. La fila no es una respuesta, es telemetría.
            log.LogError(
                excepcion, "El turno del asistente terminó en una excepción no prevista.");

            await RegistrarAsync(actor, mensaje, Caido(conversacion), arranco, ct);

            throw;
        }
        finally
        {
            // Se anota en `finally` para que un turno que se cayó a la mitad pague
            // igual las llamadas que llegó a emitir. Cobrar solo los turnos que
            // terminan bien haría del fallo una forma de consultar gratis.
            cuota.Anotar(actor, contador.Llamadas);
        }
    }

    /// <summary>
    /// Manda el turno a los dos registros, ya partido en lo que va a cada uno.
    /// </summary>
    /// <remarks>
    /// Se registra <b>lo que escribió el usuario</b> y no la pregunta interpretada:
    /// el registro analítico existe para saber cómo pregunta la gente, y guardar la
    /// versión reescrita mediría al reescritor en lugar de a los usuarios.
    ///
    /// Va con el token del request y no con el del presupuesto: si el turno se cortó
    /// por tiempo, el registro de ese corte es justamente lo que hay que conservar.
    /// </remarks>
    private Task RegistrarAsync(
        Guid actor,
        string mensaje,
        ResultadoDelTurno turno,
        DateTimeOffset arranco,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();

        return registro.RegistrarAsync(
            new TurnoParaRegistrar(
                actor,
                ahora,
                CarrilDe(turno),
                turno.Estado,
                turno.LlamadasAlModelo,
                contador.TokensDeEntrada,
                contador.TokensDeSalida,
                contador.TokensDeCache,
                (int)Math.Clamp((ahora - arranco).TotalMilliseconds, 0, int.MaxValue),
                contador.HuboReintento,
                turno.Truncado,
                mensaje,
                turno.Categoria,
                proveedor.Nombre,
                decisionSombra.Intencion),
            ct);
    }

    private static CarrilDelTurno CarrilDe(ResultadoDelTurno turno) => turno.Estado switch
    {
        // Va primero y no se deriva de las llamadas: un turno que se cayó sin llegar
        // a pedirle nada al modelo pasaría por «sin datos», que es el carril de los
        // saludos, y quedaría contado como un turno resuelto gratis.
        EstadoDelTurno.Fallo => CarrilDelTurno.Fallo,
        EstadoDelTurno.ServicioDegradado => CarrilDelTurno.Degradado,
        EstadoDelTurno.NecesitaAclaracion => CarrilDelTurno.Aclaracion,
        _ when turno.LlamadasAlModelo == 0 => CarrilDelTurno.SinDatos,
        _ => CarrilDelTurno.Sql,
    };

    private async Task<ResultadoDelTurno> ResolverAsync(
        Guid actor,
        HiloConversacional conversacion,
        string mensaje,
        OpcionesAsistente valores,
        CancellationToken ct)
    {
        // EL VEREDICTO SOBRE EL MODELO, resuelto una sola vez y ANTES del pipeline.
        // No corta el turno: los cinco pasos que no necesitan proveedor siguen
        // corriendo. Tratarlo como excepción apagaría el saludo a cero tokens y el
        // menú de aclaración justo cuando son lo único que queda en pie.
        var motivo = disponibilidad.Consultar(actor);
        var hayModelo = motivo == MotivoSinModelo.Ninguno;

        if (!hayModelo)
        {
            log.LogInformation(
                "El turno corre sin modelo disponible ({Motivo}).", motivo);
        }

        // 1 — CARRIL SIN DATOS. Se saltea entero si hay una aclaración pendiente:
        // con un menú abierto, un «gracias» le robaría la respuesta al menú y la
        // aclaración quedaría colgada.
        if (conversacion.AclaracionPendiente is null)
        {
            var intencion = EnrutadorSocial.Clasificar(mensaje);

            // La meta-pregunta se responde con el catálogo REAL y no con un texto
            // fijo. Un texto escrito a mano es una promesa sobre capacidades que
            // nadie verifica, y se desactualiza en silencio con cada GRANT.
            //
            // Sigue costando cero tokens: el catálogo sale de la base y del catálogo
            // de ejemplos, no del modelo.
            if (intencion == IntencionSocial.Meta)
            {
                var puede = await capacidades.ObtenerAsync(actor, ct);

                return SinDatos(
                    conversacion,
                    RedaccionDeCapacidades.Texto(puede),
                    puede.Ejemplos);
            }

            if (intencion != IntencionSocial.Ninguna)
            {
                return SinDatos(conversacion, EnrutadorSocial.Responder(intencion));
            }
        }

        // 2 — RESPUESTA A UNA ACLARACIÓN. Corre antes del reescritor y le entrega
        // la etiqueta canónica, no el «2» que el usuario tipeó.
        var pregunta = mensaje;
        if (conversacion.AclaracionPendiente is { } pendiente)
        {
            var resuelto = ResolverAclaracion(conversacion, pendiente, mensaje, valores);
            if (resuelto.Corte is { } corte)
            {
                return corte;
            }

            pregunta = resuelto.Pregunta;
        }

        var catalogo = await indice.ObtenerAsync(ct);

        // 3 — CAMBIO DE TEMA. Al marcarlo se suelta el segmento, así que el paso
        // siguiente encuentra el historial vigente vacío y NO llama al reescritor.
        // El pivote se fuerza acá; no se le pide al modelo que ignore nada.
        var historial = conversacion.HistorialVigente(valores.TopeDeTurnosDelHistorial);
        var pivote = DetectorDeCambioDeTema.EsPivote(pregunta, historial, catalogo);

        if (pivote)
        {
            log.LogInformation("El turno cambió de tema: se suelta el segmento anterior.");
            conversacion.SoltarElTema();
            historial = [];
        }

        // 4 — REESCRITURA. Única llamada al modelo de esta capa, y por eso el único
        // paso de acá que se saltea sin modelo. Sin él la pregunta sigue cruda: un
        // seguimiento con anáfora va a resolver peor, pero un turno autocontenido
        // —que es la mayoría— no pierde nada.
        var interpretada = hayModelo
            ? await reescritor.ReescribirAsync(pregunta, historial, ct)
            : pregunta;

        // 5 — ENRUTADOR DE DOMINIO, EN MODO SOMBRA. Va acá y no en otro lado: después
        // del reescritor porque «¿y el de Pérez?» no tiene slot que resolver hasta
        // que se resuelve la anáfora, y antes del detector de ambigüedad porque una
        // pregunta con todos sus slots únicos no es ambigua.
        //
        // LA DECISIÓN SE TOMA Y NO SE USA, A PROPÓSITO. No hay a dónde enrutar: los
        // adaptadores de respuesta y los edges hacia Modules.<X>.Contracts todavía no
        // existen, y los edges necesitan que el equipo apruebe el checklist de cinco
        // pasos del repositorio.
        //
        // Está cableado igual porque ese pedido de aprobación se fundamenta con un
        // número —qué proporción del tráfico real captura un catálogo de cinco
        // intenciones, y cuántas veces se equivoca— y ese número no existe si la
        // decisión no se toma nunca. No cambia ninguna respuesta, así que no puede
        // romper nada, y se saca borrando estas líneas.
        //
        // QUIEN VENGA A CONECTARLO: hace falta ARS-46 (edges) y los adaptadores de
        // respuesta. No alcanza con cambiar este `if`.
        var determinista = await enrutador.DecidirAsync(interpretada, ct);

        if (determinista is not null)
        {
            // Al portador y no al resultado del turno: el registro se escribe afuera
            // del pipeline, también en las dos ramas de `catch`, donde no hay ningún
            // `ResultadoDelTurno` del que leerla. Y un turno que se cae después de
            // acá conserva la decisión en su fila, igual que conserva las llamadas.
            decisionSombra.Anotar(determinista.Intencion.Nombre);

            log.LogInformation(
                "El carril determinista habría resuelto este turno con {Intencion}; "
                + "sigue por SQL porque todavía no hay a dónde enrutar.",
                determinista.Intencion.Nombre);
        }

        // 6 — AMBIGÜEDAD. Después del reescritor a propósito: «¿y en Análisis
        // Matemático?» no contiene ninguna entidad ambigua hasta que se la
        // reescribe, y la reescrita sí.
        var aclaracion = DetectorDeAmbiguedad.Detectar(interpretada, catalogo);
        if (aclaracion is not null)
        {
            conversacion.Pendiente(aclaracion);
            return NecesitaAclaracion(conversacion, aclaracion, interpretada, mensaje);
        }

        // 7 — CARRIL SQL. Es el único paso que no puede resolverse sin modelo: sin
        // generación no hay consulta, y sin consulta no hay nada que ejecutar. Se
        // corta ACÁ y no antes, para que todo lo anterior haya tenido su chance.
        if (!hayModelo)
        {
            return Degradado(conversacion, TextoSinModelo(actor, motivo));
        }

        var aMostrar = string.Equals(interpretada, mensaje, StringComparison.Ordinal)
            ? null
            : interpretada;

        var resultado = await carril.ResponderAsync(actor, mensaje, aMostrar, ct);

        conversacion.Agregar(interpretada, reloj.GetUtcNow());

        // En el pivote la pregunta interpretada se devuelve SIEMPRE, aunque
        // coincida con el mensaje: es la señal de que el asistente soltó el tema
        // anterior, y sin ella el usuario no tiene forma de saberlo.
        return resultado with
        {
            Hilo = conversacion.Id,
            PreguntaInterpretada = pivote
                ? interpretada
                : resultado.PreguntaInterpretada,
        };
    }

    /// <summary>
    /// Resuelve la respuesta del usuario a un menú abierto.
    /// </summary>
    /// <returns>
    /// La pregunta desambiguada, o el resultado con el que el turno termina cuando
    /// no se reconoció.
    /// </returns>
    private (string Pregunta, ResultadoDelTurno? Corte) ResolverAclaracion(
        HiloConversacional conversacion,
        Aclaracion pendiente,
        string mensaje,
        OpcionesAsistente valores)
    {
        var reconocida = ReconocedorDeAclaracion.Reconocer(mensaje, pendiente);

        if (reconocida is { Estado: Reconocimiento.Elegida, Opcion: { } opcion })
        {
            conversacion.CerrarAclaracion();
            return (opcion.PreguntaResuelta, null);
        }

        pendiente.Fallo();

        if (pendiente.Agotada(valores.MaximoDeIntentosDeAclaracion))
        {
            // Salida definida. Sin ella, una respuesta que nunca se reconoce deja
            // el menú abierto para siempre y el hilo deja de aceptar preguntas.
            conversacion.CerrarAclaracion();

            return (mensaje, new ResultadoDelTurno(
                EstadoDelTurno.NoContestable,
                "No pude determinar a cuál te referías. Volvé a hacer la pregunta "
                + "nombrando la carrera o el nombre completo de la persona.",
                Razonamiento: string.Empty,
                PreguntaInterpretada: null,
                [],
                [],
                Truncado: false,
                [],
                GeneracionDeSql.CategoriaNoContestable,
                LlamadasAlModelo: 0,
                conversacion.Id,
                Sugerencias: Sugerencias.Para(pendiente.PreguntaOriginal, ejemplos)));
        }

        return (mensaje, NecesitaAclaracion(
            conversacion, pendiente, pendiente.PreguntaOriginal, mensaje));
    }

    private static ResultadoDelTurno NecesitaAclaracion(
        HiloConversacional conversacion,
        Aclaracion aclaracion,
        string interpretada,
        string mensaje) =>
        new(EstadoDelTurno.NecesitaAclaracion,
            aclaracion.Texto(),
            Razonamiento: string.Empty,
            string.Equals(interpretada, mensaje, StringComparison.Ordinal) ? null : interpretada,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            LlamadasAlModelo: 0,
            conversacion.Id,
            aclaracion.Opciones);

    /// <summary>
    /// El texto de la degradación, que distingue las dos causas.
    /// </summary>
    /// <remarks>
    /// Distinguirlas no es cosmético. Con la cuota agotada el sistema <b>sabe</b>
    /// cuándo vuelve el cupo; con el proveedor caído no lo sabe nadie. Decir «probá
    /// en unos minutos» en el primer caso manda a reintentar a ciegas contra algo
    /// que no se destraba hasta una hora fija.
    /// </remarks>
    private string TextoSinModelo(Guid actor, MotivoSinModelo motivo) =>
        motivo == MotivoSinModelo.CuotaAgotada
            ? PoliticaDeAbstencion.TextoCuotaAgotada(disponibilidad.CupoVuelveA(actor))
            : PoliticaDeAbstencion.TextoServicioDegradado;

    /// <summary>Un turno que termina sin modelo: cero llamadas al proveedor.</summary>
    /// <summary>El turno que se cayó, sólo para el registro.</summary>
    /// <remarks>
    /// No lo ve nadie: se construye para pasar por el mismo camino de registro que
    /// los demás y que la separación en dos filas siga ocurriendo en un solo lugar.
    /// El texto va vacío a propósito — un turno caído no tiene respuesta, y poner
    /// una haría que el registro sugiriera que el usuario leyó algo.
    /// </remarks>
    private ResultadoDelTurno Caido(HiloConversacional conversacion) =>
        new(EstadoDelTurno.Fallo,
            string.Empty,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            CategoriaDelFallo,
            // Las que alcanzó a emitir, que son exactamente las que la cuota le va a
            // cobrar al actor en el `finally`. Si el registro dijera cero, las dos
            // fuentes discreparían justo en el caso que se está registrando.
            contador.Llamadas,
            conversacion.Id);

    /// <summary>Categoría con que el registro analítico marca un turno caído.</summary>
    internal const string CategoriaDelFallo = "fallo";

    private static ResultadoDelTurno Degradado(HiloConversacional conversacion, string texto) =>
        new(EstadoDelTurno.ServicioDegradado,
            texto,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            LlamadasAlModelo: 0,
            conversacion.Id);

    /// <summary>Un turno del carril sin datos: cero llamadas al modelo.</summary>
    /// <remarks>
    /// Las sugerencias viajan acá aunque el turno esté respondido, y no es una
    /// contradicción con el rechazo cooperativo: las sugerencias no bloquean. Son
    /// los ejemplos ejecutables que acompañan a la meta-pregunta, y es lo que hace
    /// que «¿qué podés hacer?» termine en algo clicable en vez de en un párrafo.
    /// </remarks>
    private static ResultadoDelTurno SinDatos(
        HiloConversacional conversacion,
        string texto,
        IReadOnlyList<string>? sugerencias = null) =>
        new(EstadoDelTurno.Respondida,
            texto,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            LlamadasAlModelo: 0,
            conversacion.Id,
            Sugerencias: sugerencias);
}
