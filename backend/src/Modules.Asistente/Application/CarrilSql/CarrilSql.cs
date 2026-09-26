using Microsoft.Extensions.Logging;

namespace Modules.Asistente.Application;

/// <summary>
/// Compone el carril SQL: de la pregunta en español a la respuesta redactada.
/// </summary>
/// <remarks>
/// Dos llamadas al modelo por turno —generación y redacción— y seis piezas
/// deterministas alrededor. La asimetría es deliberada: cada pieza determinista
/// que se agrega al medio es una pieza que no puede alucinar.
///
/// No es un endpoint. El <c>POST /api/asistente/consultas</c>, el contrato de
/// respuesta y la <c>Idempotency-Key</c> llegan con la épica de superficie de
/// usuario; construirlos ahora obligaría a inventar el contrato dos veces, una
/// provisional y otra cuando estén los cuatro estados, el catálogo de capacidades
/// y el hilo conversacional.
/// </remarks>
public sealed class CarrilSql(
    GeneradorDeSql generador,
    IEjecutorDeConsulta ejecutor,
    IPerfilDelActor perfiles,
    RedactorDeRespuesta redactor,
    IConsultorDeCobertura cobertura,
    IBuscadorDeMenciones buscadorDeMenciones,
    ContadorDeLlamadasDelTurno contador,
    ILogger<CarrilSql> log)
{
    /// <summary>Responde una pregunta acotada al actor.</summary>
    /// <param name="actor">
    /// Identificador de <c>identity.users</c> del usuario autenticado. Lo resuelve
    /// quien llama desde la identidad de la sesión: ningún dato enviado por el
    /// cliente lo determina.
    /// </param>
    /// <param name="mensaje">Lo que escribió el usuario.</param>
    /// <param name="preguntaInterpretada">
    /// La pregunta autocontenida, cuando el turno viene de un seguimiento y hubo
    /// que resolver una anáfora. Nula mientras no exista la capa conversacional.
    /// </param>
    /// <param name="consultasAnteriores">
    /// Las consultas de los turnos anteriores del segmento vigente, para que un
    /// seguimiento se resuelva editándolas en vez de rehaciéndolas. Vacío o nulo en
    /// un primer turno y después de un pivote.
    /// </param>
    /// <param name="mencionesNuevas">
    /// Las menciones de ESTE turno, ya revalidadas contra el alcance actual del
    /// actor (design.md D10/D11 de asistente-rediseno-v3). Vacío o nulo si el
    /// turno no trae ninguna.
    /// </param>
    /// <param name="referenciasHeredadas">
    /// Los marcadores <c>$refN</c> —con su tipo e id— que el segmento ya
    /// traía, de turnos anteriores del mismo segmento (ver
    /// <see cref="HiloConversacional.ReferenciasVigentes"/>), para que un
    /// seguimiento que edita o anida una consulta anterior pueda reusar su
    /// marcador sin que el validador lo vea como no declarado.
    /// </param>
    public async Task<ResultadoDelTurno> ResponderAsync(
        Guid actor,
        string mensaje,
        string? preguntaInterpretada,
        CancellationToken ct,
        IReadOnlyList<string>? consultasAnteriores = null,
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? mencionesNuevas = null,
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referenciasHeredadas = null)
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
                mencionesNuevas, referenciasHeredadas);
        }
        catch (ConsultaSinPrivilegio)
        {
            // La defensa de más abajo hizo lo suyo: el actor pidió una columna que
            // su rol no puede leer y el motor rechazó la consulta. Sin este catch,
            // la excepción escapaba del turno entero y llegaba cruda a quien
            // llamara — con el nombre de la tabla adentro del mensaje.
            //
            // Se resuelve como abstención y no como error: para quien pregunta,
            // «no tenés acceso a eso» es una respuesta, no una falla.
            log.LogWarning(
                "El motor rechazó la lectura por falta de privilegio del rol del asistente.");
            return SinDatos(aMostrar, PoliticaDeAbstencion.TextoSinAccesoALosDatos);
        }
        catch (ConsultaRechazadaPorElMotor excepcion)
        {
            // Cualquier otro rechazo del motor: SQL que el validador dejó pasar y
            // no ejecuta, un tipo incompatible, un timeout de sentencia. El mensaje
            // crudo nombra tablas y columnas, así que va al registro y no a la
            // respuesta.
            log.LogWarning(
                excepcion, "El motor rechazó la consulta generada ({Estado}).", excepcion.Estado);
            return SinDatos(aMostrar, PoliticaDeAbstencion.TextoErrorAlConsultar);
        }
        catch (TechoDeLlamadasSuperado)
        {
            // El turno pidió más llamadas de las que su techo permite. No es un
            // error del usuario ni algo que reintentar sirva.
            log.LogWarning("El turno del asistente agotó su techo de llamadas al modelo.");
            return Degradado(aMostrar);
        }
        catch (ProveedorNoDisponible)
        {
            // El breaker cortó el paso. No es un fallo de este turno: es el sistema
            // no gastando una llamada contra un proveedor que ya sabe caído.
            log.LogInformation("El corte al proveedor del modelo sigue abierto.");
            return Degradado(aMostrar);
        }
        catch (TimeoutDelProveedor excepcion)
        {
            log.LogWarning(excepcion, "El proveedor del modelo agotó el tiempo de la llamada.");
            return Degradado(aMostrar);
        }
        catch (HttpRequestException excepcion)
        {
            log.LogWarning(excepcion, "El proveedor del modelo no respondió.");
            return Degradado(aMostrar);
        }
        catch (TaskCanceledException excepcion) when (!ct.IsCancellationRequested)
        {
            // Cancelación por timeout del cliente HTTP, no por el token del
            // request: el usuario sigue esperando, el proveedor no contestó.
            log.LogWarning(excepcion, "El proveedor del modelo agotó su tiempo de respuesta.");
            return Degradado(aMostrar);
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
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referenciasHeredadas = null)
    {
        // NUMERADAS UNA SOLA VEZ, ACÁ: la misma asignación se usa para generar,
        // validar, ejecutar y —en el reintento— volver a generar. Numerarlas de
        // nuevo en el reintento les cambiaría el marcador a mitad de turno sin
        // ningún motivo.
        var menciones = MarcadoresDeReferencias.Asignar(mencionesNuevas ?? [], consultasAnteriores);

        // LAS HEREDADAS SE REVALIDAN CONTRA EL ALCANCE ACTUAL, ACÁ Y ANTES DE
        // DECLARAR NADA (design.md D11 de asistente-rediseno-v3). Vienen de
        // turnos anteriores del mismo hilo EN VIVO —no de la revalidación que ya
        // hace el controller para las menciones nuevas de este turno, ni de la
        // que hace `HistorialController.Reejecutar` al reusar un turno
        // persistido—: nada volvió a preguntarle a `IBuscadorDeMenciones` por
        // ellas desde que se ligaron, y `identity.materias` no tiene RLS propia
        // —sólo el filtro explícito de `asistente_materias_visibles()`—, así que
        // un actor cuyo alcance se achicó a mitad de conversación seguiría
        // pudiendo bindear una materia que ya no alcanza. Una que ya no resuelve
        // se descarta EN SILENCIO —no queda declarada ni ligable—: si la consulta
        // generada la usa igual, el validador la rechaza como marcador no
        // declarado, la misma abstención que cualquier marcador inventado, así
        // que el turno nunca llega a ejecutar contra la entidad que el actor ya
        // no ve.
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

        // LO ÚNICO QUE EL EJECUTOR NECESITA ES EL ID: el tipo sólo hacía falta
        // para declarar y validar el marcador, y para lo que persiste el turno
        // (`todasLasReferencias`, más abajo). Ligarlo como parámetro no exige
        // saber de qué entidad vino.
        var bindingsDeEjecucion = todasLasReferencias.ToDictionary(
            par => par.Key, par => par.Value.Id, StringComparer.Ordinal);

        var generacion = await generador.GenerarAsync(
            pregunta, perfil.VeDatosPersonales, ct, consultasAnteriores, menciones);

        if (!generacion.EsContestable || generacion.Sql is null)
        {
            // Corta acá: sin consulta no hay nada que ejecutar y no hay nada que
            // redactar, así que la segunda llamada no se hace.
            //
            // La categoría es la de la generación y no la constante: es lo único
            // que distingue, en el registro analítico y en el evaluador, una
            // abstención de una generación cortada por el techo de tokens. El
            // usuario ve lo mismo en las dos.
            return NoContestable(
                generacion, aMostrar, PoliticaDeAbstencion.TextoNoContestable, generacion.Categoria);
        }

        var veredicto = ValidadorDeSql.Validar(generacion.Sql, declarados, requeridos);
        if (!veredicto.EsValida)
        {
            // El motivo va al registro, no a la respuesta: nombra construcciones
            // de SQL y quien lee la respuesta es el usuario final. Un marcador
            // ignorado o inventado termina exactamente por este mismo camino: el
            // turno se abstiene, nunca ejecuta contra la entidad equivocada
            // (design.md D11).
            log.LogWarning(
                "El validador rechazó la consulta generada: {Motivo}", veredicto.Motivo);

            return NoContestable(
                generacion, aMostrar, PoliticaDeAbstencion.TextoRechazadaPorValidador,
                GeneracionDeSql.CategoriaNoContestable);
        }

        var resultado = await ejecutor.EjecutarAsync(
            generacion.Sql, actor, perfil.VeDatosPersonales, ct, bindingsDeEjecucion);

        // EL ALCANCE ES DEL TURNO, NO DEL ACTOR, y por eso se calcula acá: recién
        // con la consulta generada se sabe qué dominios tocó. La detección de portal
        // sale de `CoberturaDelPortal`, la MISMA que alimenta la declaración de
        // cobertura — dos detectores del mismo hecho es cómo una respuesta declara
        // cobertura de portal y a la vez afirma que no hay datos.
        var alcanzaTodo = PoliticaDeAbstencion.AlcanzaTodo(
            perfil, CoberturaDelPortal.TablasQueToca(generacion.Sql).Count > 0);

        if (resultado.EstaVacio && PoliticaDeAbstencion.ConvieneReintentar(resultado, alcanzaTodo))
        {
            (generacion, resultado) = await ReintentarAsync(
                actor, pregunta, generacion, resultado, perfil, consultasAnteriores, ct,
                menciones, declarados, requeridos, bindingsDeEjecucion);

            // El reintento pudo cambiar la consulta, y con ella los dominios que
            // toca: una segunda generación que agrega portal cambia el alcance del
            // turno. Sin este recálculo, el texto se decidiría con la forma de una
            // consulta que ya no es la que respondió.
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

        // Lo que de verdad viaja al turno es el ÚNICO diccionario que se
        // construyó arriba: heredado + lo de este turno, sea cual sea la
        // generación que terminó respondiendo (la del reintento reusa el mismo
        // conjunto de marcadores que la original, así que no hay que recalcular
        // nada). Vacío se anota nulo, igual que `SqlEjecutado`: no hay nada para
        // que un seguimiento reuse.
        var referenciasEjecutadas = todasLasReferencias.Count > 0
            ? (IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>)todasLasReferencias
            : null;

        return await RedactadoAsync(
            mensaje, generacion, aMostrar, resultado, perfil, alcanzaTodo,
            await CoberturaAsync(generacion, actor, ct), ct, referenciasEjecutadas);
    }

    /// <summary>
    /// Revalida cada referencia heredada del segmento contra el alcance ACTUAL
    /// del actor, y descarta la que ya no resuelve.
    /// </summary>
    /// <remarks>
    /// Las menciones NUEVAS de este turno ya llegan revalidadas —el controller
    /// las resolvió antes del candado (design.md D11)—; ésta es la comprobación
    /// que faltaba para las que el hilo trae de turnos anteriores, y sin ella un
    /// actor cuyo alcance se achicó a mitad de conversación seguiría pudiendo
    /// ejecutar contra una entidad que ya no ve —<c>identity.materias</c> no
    /// tiene RLS propia, así que nada más lo hubiera frenado—.
    /// </remarks>
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

            // Si ya no resuelve, se descarta sin dejar rastro: no entra a
            // `declarados` ni a los bindings. Ver el comentario del sitio de
            // llamada para qué pasa si la consulta generada la usa igual.
        }

        return vigentes;
    }

    /// <summary>
    /// Vuelve a generar una vez, cuando el vacío no puede explicarse por el
    /// alcance.
    /// </summary>
    /// <remarks>
    /// Si la segunda generación no es contestable o su consulta no valida, se
    /// conserva el resultado de la primera: un reintento peor que el original no
    /// tiene por qué reemplazarlo. Se le pasan las MISMAS menciones —ya
    /// numeradas— que a la primera generación: si el turno tenía una mención, el
    /// reintento sigue teniendo que respetarla, no sólo el primer intento.
    /// </remarks>
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
        IReadOnlyDictionary<string, Guid> bindingsDeEjecucion)
    {
        contador.MarcarReintento();

        var segunda = await generador.GenerarAsync(
            pregunta, perfil.VeDatosPersonales, ct, consultasAnteriores, menciones);

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

    /// <summary>
    /// Cuánta gente cargó los datos de portal que la consulta tocó.
    /// </summary>
    /// <remarks>
    /// Devuelve vacío —y no consulta nada— cuando la pregunta no tocó portal, que es
    /// el caso mayoritario. La consulta extra se paga sólo en los turnos que la
    /// necesitan.
    ///
    /// <b>Un fallo acá no puede tumbar el turno.</b> La cobertura es contexto que
    /// mejora la respuesta, no la respuesta: si el conteo falla, se responde igual y
    /// sin el denominador, que es peor que tenerlo y muchísimo mejor que un error.
    /// </remarks>
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
        // LA FRONTERA DE SALIDA. Lo que va al modelo es el resultado enmascarado;
        // lo que vuelve al llamador son las filas reales. Cambiar el orden de estas
        // dos líneas, o pasarle `resultado` al redactor, manda datos personales al
        // proveedor sin que nada falle.
        var paraElModelo = Enmascarador.Enmascarar(resultado);
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
            // La ÚNICA rama que la anota, y por eso la anota acá y no arriba: es la
            // única en la que hubo filas. `generacion` ya es la del reintento cuando
            // hubo reintento, así que ésta es la consulta que de verdad respondió.
            SqlEjecutado: generacion.Sql,
            // Respondida: this turn's analytic row gets an application-generated id,
            // and this is that same id, handed to the client once so it can later
            // submit feedback for exactly this row.
            ClaveDeRetroalimentacion: Guid.NewGuid(),
            ReferenciasEjecutadas: referenciasEjecutadas);
    }

    /// <summary>
    /// Resuelve el resultado vacío <b>sin llamar al modelo</b>.
    /// </summary>
    /// <remarks>
    /// Con cero filas no hay nada que narrar, así que la segunda llamada no
    /// aportaría información y sí podría inventarla. Resolverlo acá hace que la
    /// distinción entre «no hay» y «no podés verlo» sea mecánica en lugar de
    /// depender de que el modelo respete una instrucción del prompt.
    /// </remarks>
    private ResultadoDelTurno Vacio(
        GeneracionDeSql generacion,
        string? aMostrar,
        PerfilDelActor perfil,
        bool alcanzaTodo,
        IReadOnlyList<CoberturaDeUnDato> cobertura) =>
        new(EstadoDelTurno.Respondida,
            PoliticaDeAbstencion.TextoDeResultadoVacio(
                alcanzaTodo, CoberturaDelPortal.LaQueExplicaElVacio(cobertura)),
            // El razonamiento se escribió ANTES de ejecutar, así que puede estar
            // prometiendo filas que no salieron. Sin esta línea el turno afirma dos
            // cosas incompatibles y gana la que suena informada.
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
            ClaveDeRetroalimentacion: Guid.NewGuid());

    private ResultadoDelTurno NoContestable(
        GeneracionDeSql generacion,
        string? aMostrar,
        string texto,
        string categoria) =>
        new(EstadoDelTurno.NoContestable,
            texto,
            generacion.Razonamiento,
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            categoria,
            contador.Llamadas);

    /// <summary>
    /// Un turno que termina sin filas y sin haber llegado a la redacción.
    /// </summary>
    /// <remarks>
    /// No consume la segunda llamada al modelo: no hay nada que narrar, y pedirle
    /// que narre una falla es pedirle que invente una explicación.
    /// </remarks>
    private ResultadoDelTurno SinDatos(string? aMostrar, string texto) =>
        new(EstadoDelTurno.NoContestable,
            texto,
            Razonamiento: string.Empty,
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            contador.Llamadas);

    /// <summary>
    /// La consulta generada, solo si el actor puede verla.
    /// </summary>
    /// <remarks>
    /// El chequeo se hace <b>acá</b>, al armar la respuesta, y no en el borde HTTP.
    /// Puesto arriba, cualquier camino nuevo que devolviera un resultado tendría que
    /// acordarse de tapar el campo; puesto acá, el único lugar donde el campo se
    /// llena es el único lugar donde se decide.
    /// </remarks>
    private static string? LaConsulta(GeneracionDeSql generacion, PerfilDelActor perfil) =>
        perfil.VeLaConsulta ? generacion.Sql : null;

    /// <summary>
    /// Servicio degradado. <b>Sin sugerencias</b>: la pregunta no tiene nada de
    /// malo, así que proponerle otra al usuario le sugeriría que el problema es
    /// suyo.
    /// </summary>
    private ResultadoDelTurno Degradado(string? aMostrar) =>
        new(EstadoDelTurno.ServicioDegradado,
            PoliticaDeAbstencion.TextoServicioDegradado,
            Razonamiento: string.Empty,
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            contador.Llamadas);
}
