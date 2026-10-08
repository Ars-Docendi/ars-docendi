using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Application;

/// <summary>
/// El carril del plan compilado: puerta, muestras, acuerdo, entidades, compilación,
/// ejecución y plantilla (change <c>asistente-plan-compilado</c>, prototipo).
/// </summary>
/// <remarks>
/// <b>Nulo significa «no es mío»</b>: la pregunta sigue por el carril SQL. Todo lo
/// demás —respuesta, aclaración o abstención— termina el turno acá. La asimetría es
/// la del diseño: una pregunta que parecía del plan y no se pudo expresar con
/// seguridad no se le pasa al carril menos preciso; se aclara o se abstiene.
/// </remarks>
public sealed class CarrilDelPlan
{
    private readonly GeneradorDePlan _generador;
    private readonly ResolutorDeEntidadesDelPlan _resolutor;
    private readonly IEjecutorDeConsulta _ejecutor;
    private readonly IFechaDeReferencia _fecha;
    private readonly ContadorDeLlamadasDelTurno _contador;
    private readonly IOptions<OpcionesAsistente> _opciones;
    private readonly ILogger<CarrilDelPlan> _log;

    // Interno a propósito: sus piezas son internas del módulo. El contenedor lo
    // arma con una fábrica (ModuleExtensions) y los tests con este constructor.
    internal CarrilDelPlan(
        GeneradorDePlan generador,
        ResolutorDeEntidadesDelPlan resolutor,
        IEjecutorDeConsulta ejecutor,
        IFechaDeReferencia fecha,
        ContadorDeLlamadasDelTurno contador,
        IOptions<OpcionesAsistente> opciones,
        ILogger<CarrilDelPlan> log)
    {
        _generador = generador;
        _resolutor = resolutor;
        _ejecutor = ejecutor;
        _fecha = fecha;
        _contador = contador;
        _opciones = opciones;
        _log = log;
    }

    /// <summary>Categoría del registro analítico para un turno respondido por el plan.</summary>
    public const string CategoriaRespondida = "plan_compilado";

    public const string CategoriaAclaracion = "plan_compilado_aclaracion";

    public const string CategoriaAbstencion = "plan_compilado_abstencion";

    public const string TextoSinSeguridad =
        "No pude interpretar la pregunta con la seguridad que hace falta para responderla. "
        + "Probá decirla de otra forma, nombrando el cargo, la carrera o la materia como figuran en el sistema.";

    public const string TextoSinAcuerdo =
        "La pregunta admite más de una lectura y no quiero elegir por vos. ¿Cuál de estas es?";

    public const string TextoConfirmacion =
        "Antes de responder quiero confirmar que la entendí bien. ¿Es esta la pregunta?";

    public const string TextoAntiguedad =
        "«Antigüedad» puede contarse de dos formas en el sistema. ¿Cuál querés usar?";

    internal async Task<ResultadoDelTurno?> ResponderAsync(
        Guid actor, string pregunta, PerfilDelActor perfil, CancellationToken ct)
    {
        var texto = new TextoDeLaPregunta(pregunta);

        switch (PuertaDelPlan.Evaluar(texto))
        {
            case DecisionDeLaPuerta.NoAplica:
                return null;

            case DecisionDeLaPuerta.AclararAntiguedad:
                return Aclaracion(TextoAntiguedad, PuertaDelPlan.OpcionesDeAntiguedad(pregunta));
        }

        var carreras = (await _resolutor.CarrerasAsync(actor, ct)).Select(c => c.Nombre).ToList();

        var primera = await _generador.GenerarAsync(pregunta, 0m, ct);
        if (primera is { Expresable: false })
        {
            _log.LogInformation("El plan declaró la pregunta no expresable; sigue el carril SQL.");
            return null;
        }

        var veredicto = primera is null
            ? VeredictoDelPlan.Invalido("La salida no es un plan.")
            : ValidadorDePlan.Validar(primera, texto, carreras);

        if (!veredicto.EsValido)
        {
            _log.LogInformation("La primera muestra del plan es inválida: {Motivo}", veredicto.Motivo);
            return Abstencion(TextoSinSeguridad);
        }

        var plan = veredicto.Plan!;
        var interpretaciones = new Dictionary<string, PlanValidado>(StringComparer.Ordinal)
        {
            [plan.Canonico()] = plan,
        };
        var invalidas = 0;

        for (var muestra = 1; muestra < _opciones.Value.MuestrasDelPlan; muestra++)
        {
            var otra = await _generador.GenerarAsync(pregunta, _opciones.Value.TemperaturaDeMuestrasDelPlan, ct);
            var suVeredicto = otra is null
                ? VeredictoDelPlan.Invalido("La salida no es un plan.")
                : ValidadorDePlan.Validar(otra, texto, carreras);

            if (suVeredicto.Plan is { } valido)
            {
                interpretaciones.TryAdd(valido.Canonico(), valido);
            }
            else
            {
                invalidas++;
                _log.LogInformation("Una muestra del plan es inválida: {Motivo}", suVeredicto.Motivo);
            }
        }

        if (interpretaciones.Count > 1 || invalidas > 0)
        {
            _log.LogInformation(
                "Las muestras del plan no coinciden: {Distintas} lecturas válidas y {Invalidas} inválidas.",
                interpretaciones.Count, invalidas);

            return Aclaracion(
                interpretaciones.Count > 1 ? TextoSinAcuerdo : TextoConfirmacion,
                [.. interpretaciones.Values.Select(lectura =>
            {
                var explicita = RedaccionDelPlan.PreguntaExplicita(lectura);
                return new OpcionDeAclaracion(explicita.Trim('¿', '?'), explicita);
            })]);
        }

        return await EjecutarAsync(actor, plan, perfil, ct);
    }

    private async Task<ResultadoDelTurno> EjecutarAsync(
        Guid actor, PlanValidado plan, PerfilDelActor perfil, CancellationToken ct)
    {
        var resolucion = await _resolutor.ResolverAsync(actor, plan, ct);

        if (resolucion.Fallida is { } fallida)
        {
            var tipo = fallida.Campo.Nombre;

            if (resolucion.Candidatos.Count == 0)
            {
                return Abstencion(
                    $"No encontré la {tipo} «{fallida.NombreOriginal}» entre las que podés consultar.");
            }

            return Aclaracion(
                $"Hay más de una {tipo} que coincide con «{fallida.NombreOriginal}». ¿Cuál es?",
                [.. resolucion.Candidatos.Select(candidato => new OpcionDeAclaracion(
                    candidato.Carrera is null ? candidato.Nombre : $"{candidato.Nombre} ({candidato.Carrera})",
                    RedaccionDelPlan.PreguntaExplicita(Reemplazar(plan, fallida, candidato))))]);
        }

        var consulta = CompiladorDePlan.Compilar(plan, resolucion.Entidades, _fecha.Hoy());
        var marcadores = consulta.Bindings.Keys.ToHashSet(StringComparer.Ordinal);

        var veredicto = ValidadorDeSql.Validar(consulta.Sql, marcadores, marcadores);
        if (!veredicto.EsValida)
        {
            // Defensa en profundidad: el compilador no debería producir nunca algo
            // que el validador rechace. Si pasa, es un bug y no se ejecuta.
            _log.LogError("El validador rechazó una consulta compilada: {Motivo}", veredicto.Motivo);
            return Abstencion(TextoSinSeguridad);
        }

        // Siempre con el rol básico: el plan nunca lee columnas sensibles.
        var resultado = await _ejecutor.EjecutarAsync(
            consulta.Sql, actor, conDatosPersonales: false, ct, consulta.Bindings);

        if (plan.Medida == CatalogoDelPlan.Porcentaje
            && resultado.Filas.Count == 1
            && Convert.ToInt64(resultado.Filas[0][0], System.Globalization.CultureInfo.InvariantCulture)
                > Convert.ToInt64(resultado.Filas[0][1], System.Globalization.CultureInfo.InvariantCulture))
        {
            _log.LogError("La consulta compilada devolvió un numerador mayor que su denominador.");
            return Abstencion(TextoSinSeguridad);
        }

        var alcanzaTodo = PoliticaDeAbstencion.AlcanzaTodo(perfil, plan.TocaPortal);

        return new ResultadoDelTurno(
            EstadoDelTurno.Respondida,
            RedaccionDelPlan.Responder(plan, resultado, resolucion.Entidades, alcanzaTodo),
            $"Plan compilado: {plan.Canonico()}",
            RedaccionDelPlan.PreguntaExplicita(plan, resolucion.Entidades),
            resultado.Columnas,
            resultado.Filas,
            resultado.Truncado,
            [.. Enumerable.Range(0, resultado.Columnas.Count).Select(resultado.SensibilidadDe)],
            CategoriaRespondida,
            _contador.Llamadas,
            Sql: perfil.VeLaConsulta ? consulta.Sql : null,
            ClaveDeRetroalimentacion: Guid.NewGuid());
    }

    /// <summary>
    /// El plan con la entidad ambigua reemplazada por un candidato: con otro nombre, o
    /// con la carrera que la desambigua agregada a la misma lista.
    /// </summary>
    private static PlanValidado Reemplazar(PlanValidado plan, CondicionValidada fallida, CandidatoDeEntidad candidato)
    {
        CondicionValidada Cambiar(CondicionValidada condicion) => condicion.Indice == fallida.Indice
            ? condicion with
            {
                Nombre = TextoDeLaPregunta.Normalizar(candidato.Nombre),
                NombreOriginal = candidato.Nombre,
            }
            : condicion;

        IReadOnlyList<CondicionValidada> ConCarrera(IReadOnlyList<CondicionValidada> lista)
        {
            var cambiada = lista.Select(Cambiar).ToList();

            if (candidato.Carrera is not null && lista.Any(c => c.Indice == fallida.Indice))
            {
                cambiada.Add(new CondicionValidada(
                    plan.Todas.Max(c => c.Indice) + 1,
                    CatalogoDelPlan.Campos[CatalogoDelPlan.Carrera],
                    "=",
                    null,
                    null,
                    TextoDeLaPregunta.Normalizar(candidato.Carrera),
                    candidato.Carrera));
            }

            return cambiada;
        }

        return plan with { Filtros = ConCarrera(plan.Filtros), Condiciones = ConCarrera(plan.Condiciones) };
    }

    private ResultadoDelTurno Aclaracion(string texto, IReadOnlyList<OpcionDeAclaracion> opcionesDeAclaracion) =>
        new(EstadoDelTurno.NecesitaAclaracion,
            texto,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            CategoriaAclaracion,
            _contador.Llamadas,
            Opciones: opcionesDeAclaracion);

    private ResultadoDelTurno Abstencion(string texto) =>
        new(EstadoDelTurno.NoContestable,
            texto,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            CategoriaAbstencion,
            _contador.Llamadas);
}
