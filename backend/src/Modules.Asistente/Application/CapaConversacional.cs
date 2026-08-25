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

        var conversacion = hilos.Resolver(hilo, actor);
        var valores = opciones.Value;

        // 1 — CARRIL SIN DATOS. Se saltea entero si hay una aclaración pendiente:
        // con un menú abierto, un «gracias» le robaría la respuesta al menú y la
        // aclaración quedaría colgada.
        if (conversacion.AclaracionPendiente is null)
        {
            var intencion = EnrutadorSocial.Clasificar(mensaje);
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

        // 4 — REESCRITURA. Única llamada al modelo de esta capa.
        var interpretada = await reescritor.ReescribirAsync(pregunta, historial, ct);

        // 5 — AMBIGÜEDAD. Después del reescritor a propósito: «¿y en Análisis
        // Matemático?» no contiene ninguna entidad ambigua hasta que se la
        // reescribe, y la reescrita sí.
        var aclaracion = DetectorDeAmbiguedad.Detectar(interpretada, catalogo);
        if (aclaracion is not null)
        {
            conversacion.Pendiente(aclaracion);
            return NecesitaAclaracion(conversacion, aclaracion, interpretada, mensaje);
        }

        // 6 — CARRIL SQL.
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
                conversacion.Id));
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

    /// <summary>Un turno del carril sin datos: cero llamadas al modelo.</summary>
    private static ResultadoDelTurno SinDatos(HiloConversacional conversacion, string texto) =>
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
            conversacion.Id);
}
