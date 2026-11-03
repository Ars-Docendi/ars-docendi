using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Application;

/// <summary>Compone el carril SQL: de la pregunta en español a la respuesta redactada.</summary>
public sealed class CarrilSql(
    GeneradorDeSql generador,
    IEjecutorDeConsulta ejecutor,
    IPerfilDelActor perfiles,
    RedactorDeRespuesta redactor,
    IConsultorDeCobertura cobertura,
    IBuscadorDeMenciones buscadorDeMenciones,
    ContadorDeLlamadasDelTurno contador,
    ICatalogoDeCapacidades capacidades,
    IOptions<OpcionesAsistente> opciones,
    ILogger<CarrilSql> log,
    IEnmascaradoDelTurno enmascarado,
    CarrilDelPlan? plan = null)
{
    /// <summary>Responde una pregunta acotada al actor.</summary>
    public async Task<ResultadoDelTurno> ResponderAsync(
        Guid actor,
        string mensaje,
        string? preguntaInterpretada,
        CancellationToken ct,
        IReadOnlyList<string>? consultasAnteriores = null,
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? mencionesNuevas = null,
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referenciasHeredadas = null,
        int rechazosPrevios = 0,
        IReadOnlyList<string>? preguntasAnteriores = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);

        var pregunta = string.IsNullOrWhiteSpace(preguntaInterpretada)
            ? mensaje
            : preguntaInterpretada.Trim();

        // Solo se devuelve cuando difiere: repetir el mensaje del usuario como
        // «así lo interpreté» es ruido (RF-10).
        var aMostrar = string.Equals(pregunta, mensaje, StringComparison.Ordinal) ? null : pregunta;

        try
        {
            var perfil = await perfiles.ObtenerAsync(actor, ct);
            return await ResolverAsync(
                actor, mensaje, pregunta, aMostrar, perfil, consultasAnteriores, ct,
                mencionesNuevas, referenciasHeredadas, rechazosPrevios, preguntasAnteriores);
        }
        catch (ConsultaSinPrivilegio)
        {
            // La defensa de más abajo hizo lo suyo: el actor pidió una columna que su rol no puede
            // leer y el motor rechazó la consulta.
            log.LogWarning(
                "El motor rechazó la lectura por falta de privilegio del rol del asistente.");
            return SinDatos(
                aMostrar, PoliticaDeAbstencion.TextoSinAccesoALosDatos, CausaDelTurno.SinPrivilegioDeDatos);
        }
        catch (ConsultaRechazadaPorElMotor excepcion)
        {
            // Cualquier otro rechazo del motor: SQL que el validador dejó pasar y no ejecuta, un
            // tipo incompatible, un timeout de sentencia.
            log.LogWarning(
                excepcion, "El motor rechazó la consulta generada ({Estado}).", excepcion.Estado);
            return SinDatos(
                aMostrar, PoliticaDeAbstencion.TextoErrorAlConsultar, CausasDelTurno.DeMotor(excepcion.Estado));
        }
        catch (TechoDeLlamadasSuperado)
        {
            // El turno pidió más llamadas de las que su techo permite. No es un
            // error del usuario ni algo que reintentar sirva.
            log.LogWarning("El turno del asistente agotó su techo de llamadas al modelo.");
            return Degradado(aMostrar, CausaDelTurno.TechoDeLlamadas);
        }
        catch (ProveedorNoDisponible)
        {
            // El breaker cortó el paso. No es un fallo de este turno: es el sistema
            // no gastando una llamada contra un proveedor que ya sabe caído.
            log.LogInformation("El corte al proveedor del modelo sigue abierto.");
            return Degradado(aMostrar, CausaDelTurno.ProveedorNoDisponible);
        }
        catch (ProveedorSaturado excepcion)
        {
            // La compuerta no dio lugar a tiempo (asistente-proveedor-local, D4) o el servicio
            // compartido está lleno (asistente-proveedor-ollama, D4).
            log.LogInformation("{Motivo}", excepcion.Message);
            return Degradado(aMostrar, CausaDelTurno.ProveedorSaturado, PoliticaDeAbstencion.TextoProveedorSaturado);
        }
        catch (TimeoutDelProveedor excepcion)
        {
            log.LogWarning(excepcion, "El proveedor del modelo agotó el tiempo de la llamada.");
            return Degradado(aMostrar, CausaDelTurno.TimeoutProveedor);
        }
        catch (HttpRequestException excepcion)
        {
            log.LogWarning(excepcion, "El proveedor del modelo no respondió.");
            return Degradado(aMostrar, CausasDelTurno.DeHttp(excepcion.StatusCode));
        }
        catch (TaskCanceledException excepcion) when (!ct.IsCancellationRequested)
        {
            // Cancelación por timeout del cliente HTTP, no por el token del
            // request: el usuario sigue esperando, el proveedor no contestó.
            log.LogWarning(excepcion, "El proveedor del modelo agotó su tiempo de respuesta.");
            return Degradado(aMostrar, CausaDelTurno.TimeoutProveedor);
        }
    }

    private async Task<ResultadoDelTurno> ResolverAsync(
        Guid actor,
        string mensaje,
        string pregunta,
        string? aMostrar,
        PerfilDelActor perfil,
        IReadOnlyList<string>? consultasAnteriores,
        CancellationToken ct,
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? mencionesNuevas = null,
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referenciasHeredadas = null,
        int rechazosPrevios = 0,
        IReadOnlyList<string>? preguntasAnteriores = null)
    {
        // NUMERADAS UNA SOLA VEZ, ACÁ: la misma asignación se usa para generar, validar, ejecutar y
        // —en el reintento— volver a generar.
        var menciones = MarcadoresDeReferencias.Asignar(mencionesNuevas ?? [], consultasAnteriores);

        // LAS HEREDADAS SE REVALIDAN CONTRA EL ALCANCE ACTUAL, ACÁ Y ANTES DE DECLARAR NADA
        // (design.md D11 de asistente-rediseno-v3).
        var heredadasVigentes = await FiltrarHeredadasVigentesAsync(actor, referenciasHeredadas, ct);

        var declarados = new HashSet<string>(heredadasVigentes.Keys, StringComparer.Ordinal);
        var todasLasReferencias = new Dictionary<string, (TipoDeMencion Tipo, Guid Id)>(
            heredadasVigentes, StringComparer.Ordinal);

        foreach (var (marcador, tipo, entidad) in menciones)
        {
            declarados.Add(marcador);
            todasLasReferencias[marcador] = (tipo, entidad.Id);
        }

        var requeridos = menciones.Select(m => m.Marcador).ToHashSet(StringComparer.Ordinal);

        // LO ÚNICO QUE EL EJECUTOR NECESITA ES EL ID: el tipo sólo hacía falta para declarar y
        // validar el marcador, y para lo que persiste el turno (`todasLasReferencias`, más abajo).
        var bindingsDeEjecucion = todasLasReferencias.ToDictionary(
            par => par.Key, par => par.Value.Id, StringComparer.Ordinal);

        // CACHÉ DE CONSULTAS GENERADAS (asistente-optimizaciones-modelo-local, D6): sólo para una
        // pregunta SIN CONTEXTO —sin consultas ni preguntas anteriores, sin menciones nuevas ni
        // heredadas—, que es la única cuya consulta depende nada más que de la pregunta, el rol y
        // el día.
        var sinContexto = consultasAnteriores is not { Count: > 0 }
            && preguntasAnteriores is not { Count: > 0 }
            && menciones.Count == 0
            && heredadasVigentes.Count == 0;

        // EL PLAN COMPILADO (asistente-plan-compilado, D1), sólo para una pregunta sin contexto: el
        // prototipo no edita un plan en un seguimiento.
        if (plan is not null && opciones.Value.PlanCompilado && sinContexto)
        {
            var delPlan = await plan.ResponderAsync(actor, pregunta, perfil, ct);
            if (delPlan is not null)
            {
                return delPlan;
            }
        }

        var deCache = sinContexto ? generador.BuscarEnCache(pregunta, perfil.VeDatosPersonales) : null;
        if (deCache is not null)
        {
            log.LogInformation("La consulta de esta pregunta salió de la caché de consultas generadas.");
        }

        var generacion = deCache ?? await generador.GenerarAsync(
            pregunta, perfil.VeDatosPersonales, ct, consultasAnteriores, menciones, preguntasAnteriores);

        // LA PREGUNTA QUE RESOLVIÓ LA GENERACIÓN (D7): con la reescritura dentro
        // de la generación, «así lo interpreté» sale de acá y no del reescritor.
        if (aMostrar is null
            && generacion.PreguntaInterpretada is { } resueltaPorLaGeneracion
            && !string.Equals(resueltaPorLaGeneracion, mensaje, StringComparison.Ordinal))
        {
            aMostrar = resueltaPorLaGeneracion;
        }

        if (!generacion.EsContestable || generacion.Sql is null)
        {
            // Corta acá: sin consulta no hay nada que ejecutar y no hay nada que redactar, así que
            // la segunda llamada no se hace.
            var motivo = generacion.Motivo ?? MotivoDeRechazo.NoCubierto;
            var termino = TerminoDelRechazo.Validar(generacion.TerminoCandidato, mensaje);
            var areas = await AreasDelRechazoAsync(actor, ct);
            var texto = PlantillasDeRechazo.Texto(motivo, termino, areas, rechazosPrevios);

            log.LogInformation(
                "El carril SQL rechazó la pregunta ({Motivo}).", MotivosDeRechazo.ValorDeCable(motivo));

            // `generacion.Causa` is null for the model's own declination (not a cause) and set for
            // a truncated or unintelligible generation, where the model decided nothing.
            return NoContestable(generacion, aMostrar, texto, generacion.Categoria, generacion.Causa);
        }

        var veredicto = ValidadorDeSql.Validar(generacion.Sql, declarados, requeridos);
        if (!veredicto.EsValida)
        {
            // El motivo va al registro, no a la respuesta: nombra construcciones de SQL y quien lee
            // la respuesta es el usuario final.
            log.LogWarning(
                "El validador rechazó la consulta generada: {Motivo}", veredicto.Motivo);

            return NoContestable(
                generacion, aMostrar, PoliticaDeAbstencion.TextoRechazadaPorValidador,
                GeneracionDeSql.CategoriaNoContestable, CausaDelTurno.ValidadorRechazo);
        }

        ResultadoDeConsulta resultado;
        try
        {
            resultado = await ejecutor.EjecutarAsync(
                generacion.Sql, actor, perfil.VeDatosPersonales, ct, bindingsDeEjecucion);
        }
        catch (ConsultaRechazadaPorElMotor rechazo)
            when (generador.ReparaConsultaFallida && ErrorDelMotorSaneado.EsReparable(rechazo.Estado))
        {
            // UNA RONDA DE REPARACIÓN (D4). Si la corrección no es contestable, no valida o vuelve
            // a fallar, el turno sigue por el camino de siempre: el rechazo original —o el nuevo—
            // llega al catch de arriba con su texto de error al consultar.
            var reparacion = await RepararAsync(
                actor, pregunta, generacion, rechazo, perfil, consultasAnteriores, ct,
                menciones, declarados, requeridos, bindingsDeEjecucion, preguntasAnteriores);

            if (reparacion is null)
            {
                throw;
            }

            (generacion, resultado) = reparacion.Value;
        }

        // EL ALCANCE ES DEL TURNO, NO DEL ACTOR, y por eso se calcula acá: recién con la consulta
        // generada se sabe qué dominios tocó.
        var alcanzaTodo = PoliticaDeAbstencion.AlcanzaTodo(
            perfil, CoberturaDelPortal.TablasQueToca(generacion.Sql).Count > 0);

        if (resultado.EstaVacio
            && generador.ReintentaConsultaVacia
            && PoliticaDeAbstencion.ConvieneReintentar(resultado, alcanzaTodo))
        {
            (generacion, resultado) = await ReintentarAsync(
                actor, pregunta, generacion, resultado, perfil, consultasAnteriores, ct,
                menciones, declarados, requeridos, bindingsDeEjecucion, preguntasAnteriores);

            // El reintento pudo cambiar la consulta, y con ella los dominios que toca: una segunda
            // generación que agrega portal cambia el alcance del turno.
            alcanzaTodo = PoliticaDeAbstencion.AlcanzaTodo(
                perfil, CoberturaDelPortal.TablasQueToca(generacion.Sql).Count > 0);
        }

        if (resultado.EstaVacio)
        {
            // SIN FILAS, SIN BINDINGS A PROPÓSITO: igual que `SqlEjecutado` queda
            // nulo acá abajo, los bindings tampoco se anotan — no hay ninguna
            // consulta que un seguimiento pueda editar o anidar.
            return Vacio(
                generacion, aMostrar, perfil, alcanzaTodo, await CoberturaAsync(generacion, actor, ct));
        }

        // Validó y trajo filas: recién ahora la traducción vale como para que otro la reutilice
        // (D6).
        if (sinContexto && deCache is null)
        {
            generador.Recordar(pregunta, perfil.VeDatosPersonales, generacion);
        }

        // Lo que de verdad viaja al turno es el ÚNICO diccionario que se construyó arriba: heredado
        // + lo de este turno, sea cual sea la generación que terminó respondiendo (la del reintento
        // reusa el mismo conjunto de marcadores que la original, así que no hay que recalcular
        // nada).
        var referenciasEjecutadas = todasLasReferencias.Count > 0
            ? (IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>)todasLasReferencias
            : null;

        return await RedactadoAsync(
            mensaje, generacion, aMostrar, resultado, perfil, alcanzaTodo,
            await CoberturaAsync(generacion, actor, ct), ct, referenciasEjecutadas);
    }

    /// <summary>
    /// Revalida cada referencia heredada del segmento contra el alcance ACTUAL del actor, y
    /// descarta la que ya no resuelve.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>> FiltrarHeredadasVigentesAsync(
        Guid actor,
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referenciasHeredadas,
        CancellationToken ct)
    {
        if (referenciasHeredadas is null || referenciasHeredadas.Count == 0)
        {
            return new Dictionary<string, (TipoDeMencion Tipo, Guid Id)>();
        }

        var vigentes = new Dictionary<string, (TipoDeMencion Tipo, Guid Id)>(StringComparer.Ordinal);

        foreach (var (marcador, referencia) in referenciasHeredadas)
        {
            var resuelta = await buscadorDeMenciones.ResolverAsync(actor, referencia.Tipo, referencia.Id, ct);
            if (resuelta is not null)
            {
                vigentes[marcador] = referencia;
            }

            // Si ya no resuelve, se descarta sin dejar rastro: no entra a `declarados` ni a los
            // bindings.
        }

        return vigentes;
    }

    /// <summary>
    /// Vuelve a generar una vez, cuando el vacío no puede explicarse por el alcance.
    /// </summary>
    private async Task<(GeneracionDeSql, ResultadoDeConsulta)> ReintentarAsync(
        Guid actor,
        string pregunta,
        GeneracionDeSql original,
        ResultadoDeConsulta resultadoOriginal,
        PerfilDelActor perfil,
        IReadOnlyList<string>? consultasAnteriores,
        CancellationToken ct,
        IReadOnlyList<(string Marcador, TipoDeMencion Tipo, ResultadoDeMencion Entidad)> menciones,
        IReadOnlySet<string> declarados,
        IReadOnlySet<string> requeridos,
        IReadOnlyDictionary<string, Guid> bindingsDeEjecucion,
        IReadOnlyList<string>? preguntasAnteriores = null)
    {
        contador.MarcarReintento();

        // CON CONTEXTO (D4): decirle qué consulta no trajo filas en vez de
        // repetir el prompt idéntico, que a temperatura 0 devuelve la misma.
        (string, string)? intentoAnterior = generador.ReintentaConContexto && original.Sql is { } anterior
            ? (anterior, ProblemaDeConsultaVacia)
            : null;

        var segunda = await generador.GenerarAsync(
            pregunta, perfil.VeDatosPersonales, ct, consultasAnteriores, menciones,
            preguntasAnteriores, intentoAnterior, esSegundaGeneracion: true);

        if (!segunda.EsContestable
            || segunda.Sql is null
            || !ValidadorDeSql.Validar(segunda.Sql, declarados, requeridos).EsValida)
        {
            return (original, resultadoOriginal);
        }

        var resultado = await ejecutor.EjecutarAsync(
            segunda.Sql, actor, perfil.VeDatosPersonales, ct, bindingsDeEjecucion);

        return resultado.EstaVacio ? (original, resultadoOriginal) : (segunda, resultado);
    }

    /// <summary>Lo que se le cuenta al modelo de una consulta que vino vacía (D4).</summary>
    internal const string ProblemaDeConsultaVacia =
        "Se ejecutó sin errores pero no devolvió ninguna fila. Puede que un filtro sea demasiado "
        + "estricto, que compare por igualdad un texto que el usuario escribió distinto, o que "
        + "use la tabla o la columna equivocada.";

    /// <summary>
    /// La ronda de reparación de una consulta que el motor rechazó (D4), o nulo si la corrección no
    /// sirve.
    /// </summary>
    private async Task<(GeneracionDeSql, ResultadoDeConsulta)?> RepararAsync(
        Guid actor,
        string pregunta,
        GeneracionDeSql original,
        ConsultaRechazadaPorElMotor rechazo,
        PerfilDelActor perfil,
        IReadOnlyList<string>? consultasAnteriores,
        CancellationToken ct,
        IReadOnlyList<(string Marcador, TipoDeMencion Tipo, ResultadoDeMencion Entidad)> menciones,
        IReadOnlySet<string> declarados,
        IReadOnlySet<string> requeridos,
        IReadOnlyDictionary<string, Guid> bindingsDeEjecucion,
        IReadOnlyList<string>? preguntasAnteriores)
    {
        contador.MarcarReintento();

        log.LogInformation(
            "El motor rechazó la consulta generada ({Estado}); se pide una corrección.", rechazo.Estado);

        var problema = ErrorDelMotorSaneado.Problema(original.Sql!, rechazo.Estado, rechazo.DetalleDelMotor);
        var corregida = await generador.GenerarAsync(
            pregunta, perfil.VeDatosPersonales, ct, consultasAnteriores, menciones,
            preguntasAnteriores, (original.Sql!, problema), esSegundaGeneracion: true);

        if (!corregida.EsContestable
            || corregida.Sql is null
            || !ValidadorDeSql.Validar(corregida.Sql, declarados, requeridos).EsValida)
        {
            return null;
        }

        // Si la corrección también falla, esa excepción sigue su camino: no hay
        // segunda ronda.
        var resultado = await ejecutor.EjecutarAsync(
            corregida.Sql, actor, perfil.VeDatosPersonales, ct, bindingsDeEjecucion);

        return (corregida with { PreguntaInterpretada = corregida.PreguntaInterpretada ?? original.PreguntaInterpretada },
            resultado);
    }

    /// <summary>Cuánta gente cargó los datos de portal que la consulta tocó.</summary>
    private async Task<IReadOnlyList<CoberturaDeUnDato>> CoberturaAsync(
        GeneracionDeSql generacion, Guid actor, CancellationToken ct)
    {
        var tablas = CoberturaDelPortal.TablasQueToca(generacion.Sql);

        if (tablas.Count == 0)
        {
            return [];
        }

        try
        {
            return await cobertura.ObtenerAsync(tablas, actor, ct);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            log.LogWarning(
                excepcion, "No se pudo calcular la cobertura de portal; se responde sin ella.");
            return [];
        }
    }

    private async Task<ResultadoDelTurno> RedactadoAsync(
        string mensaje,
        GeneracionDeSql generacion,
        string? aMostrar,
        ResultadoDeConsulta resultado,
        PerfilDelActor perfil,
        bool alcanzaTodo,
        IReadOnlyList<CoberturaDeUnDato> cobertura,
        CancellationToken ct,
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referenciasEjecutadas = null)
    {
        // LA FRONTERA DE SALIDA. Lo que va al modelo es el resultado enmascarado; lo que vuelve al
        // llamador son las filas reales.
        var sinEnmascarar = await enmascarado.RedaccionSinEnmascararAsync(ct);
        var paraElModelo = Enmascarador.Enmascarar(
            resultado,
            sinEnmascarar
                ? ModoDeEnmascarado.SoloTextoLibre
                : ModoDeEnmascarado.Todo);
        var texto = await redactor.RedactarAsync(
            mensaje, paraElModelo, alcanzaTodo, cobertura, ct);

        return new ResultadoDelTurno(
            EstadoDelTurno.Respondida,
            texto,
            generacion.Razonamiento,
            aMostrar,
            resultado.Columnas,
            resultado.Filas,
            resultado.Truncado,
            [.. Enumerable.Range(0, resultado.Columnas.Count).Select(resultado.SensibilidadDe)],
            generacion.Categoria,
            contador.Llamadas,
            Sql: LaConsulta(generacion, perfil),
            // La ÚNICA rama que la anota, y por eso la anota acá y no arriba: es la única en la que
            // hubo filas.
            SqlEjecutado: generacion.Sql,
            // Respondida: this turn's analytic row gets an application-generated id,
            // and this is that same id, handed to the client once so it can later
            // submit feedback for exactly this row.
            ClaveDeRetroalimentacion: Guid.NewGuid(),
            ReferenciasEjecutadas: referenciasEjecutadas);
    }

    /// <summary>Resuelve el resultado vacío <b>sin llamar al modelo</b>.</summary>
    private ResultadoDelTurno Vacio(
        GeneracionDeSql generacion,
        string? aMostrar,
        PerfilDelActor perfil,
        bool alcanzaTodo,
        IReadOnlyList<CoberturaDeUnDato> cobertura) =>
        new(EstadoDelTurno.Respondida,
            PoliticaDeAbstencion.TextoDeResultadoVacio(
                alcanzaTodo, CoberturaDelPortal.LaQueExplicaElVacio(cobertura)),
            // El razonamiento se escribió ANTES de ejecutar, así que puede estar prometiendo filas
            // que no salieron.
            PoliticaDeAbstencion.RazonamientoDeResultadoVacio(generacion.Razonamiento),
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            generacion.Categoria,
            contador.Llamadas,
            Sql: LaConsulta(generacion, perfil),
            // Respondida too (zero rows is still an answer), so it gets a token the
            // same way RedactadoAsync's branch does.
            ClaveDeRetroalimentacion: Guid.NewGuid(),
            // Still Respondida: an empty result is often the correct answer. The cause
            // is only a signal that lets the quality view tell it apart.
            Causa: CausaDelTurno.ResultadoVacio);

    private ResultadoDelTurno NoContestable(
        GeneracionDeSql generacion,
        string? aMostrar,
        string texto,
        string categoria,
        CausaDelTurno? causa) =>
        new(EstadoDelTurno.NoContestable,
            texto,
            generacion.Razonamiento,
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            categoria,
            contador.Llamadas,
            // `generacion.Motivo` es no nulo únicamente en la rama del rechazo declarado por el
            // modelo (ver `ResolverAsync`): el rechazo del validador reusa este mismo constructor
            // con una `generacion` cuyo `EsContestable` era verdadero, así que ahí siempre es
            // `null` — el motivo del registro operativo nunca lo pone el validador.
            MotivoDeRechazo: generacion.Motivo,
            Causa: causa);

    /// <summary>
    /// Las áreas consultables del actor, ya renderizadas para un rechazo, o <c>null</c> si el
    /// catálogo no se pudo leer.
    /// </summary>
    private async Task<string?> AreasDelRechazoAsync(Guid actor, CancellationToken ct)
    {
        try
        {
            var capacidadesDelActor = await capacidades.ObtenerAsync(actor, ct);
            return EtiquetasDeAreas.Nombrar(capacidadesDelActor.Cubre);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            log.LogWarning(
                excepcion, "No se pudo leer el catálogo de capacidades; el rechazo se responde sin áreas.");
            return null;
        }
    }

    /// <summary>Un turno que termina sin filas y sin haber llegado a la redacción.</summary>
    private ResultadoDelTurno SinDatos(string? aMostrar, string texto, CausaDelTurno causa) =>
        new(EstadoDelTurno.NoContestable,
            texto,
            Razonamiento: string.Empty,
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            contador.Llamadas,
            Causa: causa);

    /// <summary>La consulta generada, solo si el actor puede verla.</summary>
    private static string? LaConsulta(GeneracionDeSql generacion, PerfilDelActor perfil) =>
        perfil.VeLaConsulta ? generacion.Sql : null;

    /// <summary>
    /// Servicio degradado. <b>Sin sugerencias</b>: la pregunta no tiene nada de malo, así que
    /// proponerle otra al usuario le sugeriría que el problema es suyo.
    /// </summary>
    private ResultadoDelTurno Degradado(
        string? aMostrar,
        CausaDelTurno causa,
        string texto = PoliticaDeAbstencion.TextoServicioDegradado) =>
        new(EstadoDelTurno.ServicioDegradado,
            texto,
            Razonamiento: string.Empty,
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            contador.Llamadas,
            Causa: causa);
}
