using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Application;

/// <summary>Convierte una serie de consultas sueltas en una conversación.</summary>
public sealed class CapaConversacional(
    IAlmacenDeHilos hilos,
    IIndiceDeEntidades indice,
    ReescritorDePreguntas reescritor,
    CarrilSql carril,
    ICatalogoDeCapacidades capacidades,
    EnrutadorDeDominio enrutador,
    IProveedorDeModelo proveedor,
    IRegistroDelTurno registro,
    IRegistroDeHistorial historial,
    IValidezDeRetroalimentacion validezDeRetroalimentacion,
    IDisponibilidadDelModelo disponibilidad,
    IDisponibilidadDelModulo disponibilidadDelModulo,
    IConsultasIdentity identidad,
    ICuotaDelActor cuota,
    IPresupuestoOrganizacional presupuestoOrganizacional,
    ICandadoDelTurno candadoDelTurno,
    IResolutorDeVinculos vinculos,
    IProveedorDeFichas fichas,
    ContadorDeLlamadasDelTurno contador,
    DecisionSombraDelTurno decisionSombra,
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj,
    ILogger<CapaConversacional> log)
{
    /// <summary>Responde un turno dentro de un hilo.</summary>
    public async Task<ResultadoDelTurno> ResponderAsync(
        Guid actor,
        Guid? hilo,
        string mensaje,
        CancellationToken ct,
        string? claveDelCliente = null,
        string? reemplaza = null,
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? mencionesNuevas = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);

        // EL CANDADO DEL TURNO. Primerísimo, antes de abrir el presupuesto y de resolver el hilo
        // (design.md D5 de asistente-administracion-de-uso): es el chequeo más barato de todos y el
        // más probable de rechazar al instante, así que quien pierde la carrera no paga el resto.
        var candado = await candadoDelTurno.IntentarAsync(actor, ct);
        if (candado is null)
        {
            var conversacionRechazada = hilos.Resolver(hilo, actor);
            var turnoRechazado = FabricasDelResultado.Degradado(
                conversacionRechazada, PoliticaDeAbstencion.TextoTurnoConcurrente,
                CausaDelTurno.TurnoConcurrente);

            // REEMPLAZO + CANDADO RECHAZADO (design.md D9, punto 4): «el rechazo del candado no
            // cambia nada».
            if (reemplaza is null)
            {
                await RegistrarAsync(
                    actor, conversacionRechazada, mensaje, turnoRechazado, reloj.GetUtcNow(), ct,
                    claveDelCliente, conversacionRechazada.InicioDeSegmento,
                    conversacionRechazada.AclaracionPendiente, mencionesNuevas: mencionesNuevas);
            }

            return turnoRechazado with
            {
                CupoRestante = await cuota.CupoRestanteAsync(actor, ct),
                Conversacion = conversacionRechazada.HiloHistorico,
            };
        }

        var valores = opciones.Value;

        // LA COTA DEL TURNO. Una sola, punta a punta, encadenada al token del request.
        using var presupuesto = PresupuestoDelTurno.Abrir(
            ct, TimeSpan.FromSeconds(valores.PresupuestoDelTurnoSegundos), reloj);

        var conversacion = hilos.Resolver(hilo, actor);

        // VALIDACIÓN DEL OBJETIVO DE REEMPLAZO (design.md D9, punto 1): después del candado, y
        // antes de sacar nada.
        TurnoSacado? reemplazado = null;
        if (reemplaza is not null)
        {
            var ultimo = conversacion.UltimoRegistrado;
            if (ultimo is null || !EsElObjetivoDelReemplazo(ultimo, reemplaza))
            {
                await candado.DisposeAsync();
                throw new ReemplazoInvalido(reemplaza);
            }

            // SACA EL TURNO VIEJO ANTES DE RESOLVER (design.md D9, punto 2): la pregunta nueva se
            // resuelve contra la MISMA `conversacion`, ya sin su último turno —si había entrado al
            // contexto SQL— y con el segmento/aclaración que tenía antes de él — la vista sin el
            // turno reemplazado.
            reemplazado = conversacion.QuitarUltimoParaReemplazo();
        }

        // SNAPSHOT DE «ANTES DE ESTE TURNO» (design.md D9). Se captura ACÁ — después de sacar el
        // turno reemplazado, si lo hay, así que ve la vista ya restaurada— y no adentro de
        // `ResolverAsync`, porque el `catch` de abajo (presupuesto vencido) nunca llega a invocarlo
        // y necesita el mismo valor: es lo que describe el contexto que ESTE turno tuvo, para que
        // un reemplazo futuro pueda restaurarlo.
        var inicioDeSegmentoAntes = conversacion.InicioDeSegmento;
        var aclaracionAntes = conversacion.AclaracionPendiente;

        var arranco = reloj.GetUtcNow();
        ResultadoDelTurno resultado;

        try
        {
            var turno = await ResolverAsync(
                actor, conversacion, mensaje, claveDelCliente,
                inicioDeSegmentoAntes, aclaracionAntes, valores, presupuesto.Token, mencionesNuevas);

            if (turno.ClaveDeRetroalimentacion is { } token)
            {
                // Minted right here and not inside IRegistroDelTurno's fire-and-forget write: the
                // token has to be live before the response carrying it is even built, and writing
                // to this in-memory store can't fail the turn either way.
                validezDeRetroalimentacion.Registrar(token, reloj.GetUtcNow());
            }

            // EL LADO DEL ACTOR DE D3. Este evento nombra al actor —como ya hace la cuota— y nunca
            // el token de retroalimentación: el endpoint de retroalimentación es el que nombra el
            // token, y nunca el actor.
            log.LogInformation("Turno del asistente resuelto para el actor {ActorId}.", actor);

            await RegistrarAsync(
                actor, conversacion, mensaje, turno, arranco, CancellationToken.None,
                claveDelCliente, inicioDeSegmentoAntes, aclaracionAntes, reemplazado, mencionesNuevas);

            resultado = turno;
        }
        catch (OperationCanceledException) when (presupuesto.Vencio)
        {
            // Se acabó el tiempo del turno. Para quien preguntó, «no llegué a
            // tiempo» es una respuesta; una cancelación cruda no lo es.
            log.LogWarning(
                "El turno del asistente agotó su presupuesto de {Segundos}s.",
                valores.PresupuestoDelTurnoSegundos);

            var turno = FabricasDelResultado.Degradado(
                conversacion, PoliticaDeAbstencion.TextoServicioDegradado, CausaDelTurno.PresupuestoDelTurno);
            await RegistrarAsync(
                actor, conversacion, mensaje, turno, arranco, CancellationToken.None,
                claveDelCliente, inicioDeSegmentoAntes, aclaracionAntes, reemplazado, mencionesNuevas);

            resultado = turno;
        }
        catch (Exception excepcion) when (!presupuesto.Vencio)
        {
            // El turno se cayó. La cuota ya lo cobra en el `finally` de abajo, así que sin esta
            // rama el actor pagaba llamadas que no aparecían en ningún lado: el registro operativo
            // —la única fuente para «cuántas veces falló»— sub-contaba justo las fallas duras.
            log.LogError(
                excepcion, "El turno del asistente terminó en una excepción no prevista.");

            // FALLO EN UN REEMPLAZO: no cambia nada (design.md D9, punto 4). Se repone el turno
            // viejo ANTES de registrar el fallo, así que el hilo queda exactamente como estaba
            // cuando el actor pidió el reemplazo.
            if (reemplazado is { } saliente)
            {
                conversacion.ReponerTrasFallo(saliente);
            }

            await RegistrarAsync(
                actor, conversacion, mensaje, FabricasDelResultado.Caido(conversacion, contador.Llamadas),
                arranco, CancellationToken.None, claveDelCliente, inicioDeSegmentoAntes, aclaracionAntes,
                mencionesNuevas: mencionesNuevas);

            throw;
        }
        finally
        {
            // EL CANDADO SE LIBERA ACÁ, en el mismo `finally` que ya cobraba la cuota — no uno
            // nuevo (design.md D5/D4: mismos puntos de salida).
            await candado.DisposeAsync();

            // Se anota en `finally` para que un turno que se cayó a la mitad pague igual las
            // llamadas que llegó a emitir.
            if (contador.Llamadas > 0)
            {
                await cuota.AnotarAsync(actor, CancellationToken.None);

                // Mismo guard y mismo sitio que la cuota, por el mismo D4: si
                // no hubo llamada al modelo, no hay tokens que costear.
                await presupuestoOrganizacional.AcumularAsync(
                    proveedor.Nombre,
                    reloj.GetUtcNow(),
                    contador.TokensDeEntrada,
                    contador.TokensDeSalida,
                    contador.TokensDeCache,
                    CancellationToken.None);
            }
        }

        // FUERA del try/finally, a propósito (tarea 7.2): el cupo restante tiene que reflejar el
        // cobro que el `finally` de arriba ACABA de hacer, no el valor de antes.
        return resultado with
        {
            CupoRestante = await cuota.CupoRestanteAsync(actor, ct),
            Conversacion = conversacion.HiloHistorico,
        };
    }

    /// <summary>Manda el turno a los dos registros, ya partido en lo que va a cada uno.</summary>
    private async Task RegistrarAsync(
        Guid actor,
        HiloConversacional conversacion,
        string mensaje,
        ResultadoDelTurno turno,
        DateTimeOffset arranco,
        CancellationToken ct,
        string? claveDelCliente,
        int inicioDeSegmentoAntes,
        Aclaracion? aclaracionAntes,
        TurnoSacado? reemplazado = null,
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? mencionesNuevas = null)
    {
        var ahora = reloj.GetUtcNow();

        // On a Respondida turn, ClaveDeRetroalimentacion already IS this row's analytic id — the
        // exact one already handed to the client.
        var analiticoId = turno.ClaveDeRetroalimentacion ?? Guid.NewGuid();

        await registro.RegistrarAsync(
            new TurnoParaRegistrar(
                actor,
                analiticoId,
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
                decisionSombra.Intencion,
                turno.MotivoDeRechazo,
                turno.Causa,
                // The same resolved adapter name as `Proveedor` above, also on turns
                // that never called a model (asistente-metricas-de-calidad, D3).
                proveedor.Nombre),
            ct);

        // HISTORIAL PROPIO (asistente-historial-conversaciones). Se excluye EXACTAMENTE Fallo, y es
        // la ÚNICA exclusión — no hay opt-out por conversación (design.md D2): un turno caído nunca
        // produjo cuerpo HTTP (el mapeo del estado revienta si se le pide un nombre público), así
        // que no hay nada coherente para mostrar en una conversación retomada.
        if (turno.Estado != EstadoDelTurno.Fallo)
        {
            // MARCA ESTE TURNO COMO EL ÚLTIMO REGISTRADO, SEA CUAL SEA SU CARRIL (design.md D9): es
            // lo que deja disponible para un reemplazo FUTURO, sin importar si este turno también
            // entró al contexto SQL.
            conversacion.MarcarUltimoRegistrado(
                claveDelCliente,
                turnoHistoricoId: null,
                turno.ClaveDeRetroalimentacion,
                inicioDeSegmentoAntes,
                aclaracionAntes);

            var paraHistorial = new TurnoParaHistorial(
                actor,
                turno.PreguntaInterpretada ?? mensaje,
                turno.SqlEjecutado,
                turno.Estado,
                ahora,
                ReferenciasParaElHistorial(turno.ReferenciasEjecutadas, mencionesNuevas));

            // REEMPLAZO CON DESENLACE REGISTRABLE (design.md D9, punto 3): se revoca el token viejo
            // y el historial reemplaza su fila, en vez de agregar una nueva.
            if (reemplazado is not null)
            {
                if (reemplazado.Identidad.ClaveDeRetroalimentacion is { } tokenViejo)
                {
                    validezDeRetroalimentacion.Revocar(tokenViejo);

                    // IMPLICIT DISSATISFACTION (asistente-metricas-de-calidad, D4). The token is
                    // the replaced turn's analytic id ONLY for a Respondida turn: every other state
                    // generates an id nobody keeps, and a restart empties the in-memory thread.
                    await registro.MarcarReemplazadoAsync(tokenViejo, ct);
                }

                await historial.ReemplazarUltimoTurnoAsync(conversacion, paraHistorial, ct);
            }
            else
            {
                await historial.RegistrarTurnoAsync(conversacion, paraHistorial, ct);
            }
        }
    }

    /// <summary>
    /// Lo que se persiste en <c>asistente.turno_historico.referencias</c> (decisión 15 del PO,
    /// 2026-09-26, design.md D11 de asistente-rediseno-v3).
    /// </summary>
    private static IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? ReferenciasParaElHistorial(
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referenciasEjecutadas,
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? mencionesNuevas)
    {
        if (referenciasEjecutadas is { Count: > 0 })
        {
            return referenciasEjecutadas;
        }

        if (mencionesNuevas is not { Count: > 0 })
        {
            return null;
        }

        return MarcadoresDeReferencias.Asignar(mencionesNuevas, consultasAnteriores: null)
            .ToDictionary(m => m.Marcador, m => (m.Tipo, m.Entidad.Id));
    }

    /// <summary>
    /// Si <paramref name="reemplaza"/> nombra al último turno registrado: su <c>Idempotency-Key</c>
    /// si es de esta sesión, o su <c>turno_historico.id</c> si es de una conversación reanudada
    /// (design.md D9 de asistente-rediseno-v3).
    /// </summary>
    private static bool EsElObjetivoDelReemplazo(IdentidadDelUltimoRegistrado ultimo, string reemplaza) =>
        string.Equals(ultimo.ClaveDelCliente, reemplaza, StringComparison.Ordinal)
        || (ultimo.TurnoHistoricoId is { } id
            && Guid.TryParse(reemplaza, out var idPedido)
            && id == idPedido);

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

    /// <param name="inicioDeSegmentoAntes">
    /// El segmento que el hilo tenía justo ANTES de este turno —capturado por
    /// quien llama, en <c>ResponderAsync</c>, y no acá: el <c>catch</c> del
    /// presupuesto vencido nunca invoca este método y necesita el mismo
    /// valor—. Viaja al turno que agrega el carril SQL (design.md D9).
    /// </param>
    /// <param name="aclaracionAntes">Mismo motivo que <paramref name="inicioDeSegmentoAntes"/>.</param>
    private async Task<ResultadoDelTurno> ResolverAsync(
        Guid actor,
        HiloConversacional conversacion,
        string mensaje,
        string? claveDelCliente,
        int inicioDeSegmentoAntes,
        Aclaracion? aclaracionAntes,
        OpcionesAsistente valores,
        CancellationToken ct,
        IReadOnlyList<(TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? mencionesNuevas = null)
    {
        // EL VEREDICTO SOBRE EL MODELO, resuelto una sola vez y ANTES del pipeline.
        var motivo = await disponibilidad.ConsultarAsync(actor, ct);

        // EL BYPASS DE MANTENIMIENTO ES CASO POR CASO Y VIVE ACÁ, no en el puerto de almacenamiento
        // (design.md D8): un admin con asistente.administrar puede seguir usando el asistente en
        // mantenimiento —para verificar que la recuperación funcionó antes de reabrirlo a todos—,
        // pero NO está exento de su propio cupo ni del tope organizacional ni de la exclusión de
        // turno concurrente: sólo se ignora el motivo Mantenimiento, y sólo ese.
        if (motivo == MotivoSinModelo.Mantenimiento)
        {
            var permisos = await identidad.ObtenerCodigosDePermisosAsync(actor, ct);
            if (permisos.Contains(Permisos.AsistenteAdministrar))
            {
                motivo = MotivoSinModelo.Ninguno;
            }
        }

        // SIN ACCESO CORTA ACÁ, antes de TODO el pipeline (asistente-acceso-granular,
        // design.md D4): a diferencia del resto de los motivos, no es que falte el
        // modelo — es que este actor no puede usar el asistente, y los pasos que
        // no necesitan proveedor (el menú de aclaración sale de una consulta a la
        // base) tampoco le corresponden.
        if (motivo == MotivoSinModelo.SinAcceso)
        {
            return FabricasDelResultado.Degradado(
                conversacion, PoliticaDeAbstencion.TextoSinAcceso, CausaDelTurno.AccesoRevocado);
        }

        var hayModelo = motivo == MotivoSinModelo.Ninguno;

        if (!hayModelo)
        {
            log.LogInformation(
                "El turno corre sin modelo disponible ({Motivo}).", motivo);
        }

        // 1 — CARRIL SIN DATOS. Se saltea entero si hay una aclaración pendiente: con un menú
        // abierto, un «gracias» le robaría la respuesta al menú y la aclaración quedaría colgada.
        if (conversacion.AclaracionPendiente is null)
        {
            var intencion = EnrutadorSocial.Clasificar(mensaje);

            // La meta-pregunta se responde con el catálogo REAL y no con un texto fijo.
            if (intencion == IntencionSocial.Meta)
            {
                var puede = await capacidades.ObtenerAsync(actor, ct);

                return FabricasDelResultado.SinDatos(conversacion, RedaccionDeCapacidades.Texto(puede));
            }

            if (intencion != IntencionSocial.Ninguna)
            {
                return FabricasDelResultado.SinDatos(conversacion, EnrutadorSocial.Responder(intencion));
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

        // 3 — CAMBIO DE TEMA. Al marcarlo se suelta el segmento, así que el paso siguiente
        // encuentra el historial vigente vacío y NO llama al reescritor.
        var historial = conversacion.HistorialVigente(valores.TopeDeTurnosDelHistorial);
        var pivote = DetectorDeCambioDeTema.EsPivote(pregunta, historial, catalogo);

        if (pivote)
        {
            log.LogInformation("El turno cambió de tema: se suelta el segmento anterior.");
            conversacion.SoltarElTema();
            historial = [];
        }

        // 4 — REESCRITURA. Única llamada al modelo de esta capa, y por eso el único paso de acá que
        // se saltea sin modelo.
        var reescribeLaGeneracion = valores.ReescrituraEnLaGeneracion;
        var interpretada = hayModelo && !reescribeLaGeneracion
            ? await ReescribirOCrudaAsync(pregunta, historial, ct)
            : pregunta;

        // 5 — ENRUTADOR DE DOMINIO, EN MODO SOMBRA. Va acá y no en otro lado: después del
        // reescritor porque «¿y el de Pérez?» no tiene slot que resolver hasta que se resuelve la
        // anáfora, y antes del detector de ambigüedad porque una pregunta con todos sus slots
        // únicos no es ambigua.
        var determinista = await enrutador.DecidirAsync(interpretada, ct);

        if (determinista is not null)
        {
            // Al portador y no al resultado del turno: el registro se escribe afuera del pipeline,
            // también en las dos ramas de `catch`, donde no hay ningún `ResultadoDelTurno` del que
            // leerla.
            decisionSombra.Anotar(determinista.Intencion.Nombre);

            log.LogInformation(
                "El carril determinista habría resuelto este turno con {Intencion}; "
                + "sigue por SQL porque todavía no hay a dónde enrutar.",
                determinista.Intencion.Nombre);
        }

        // 6 — AMBIGÜEDAD. Después del reescritor a propósito: «¿y en Análisis Matemático?» no
        // contiene ninguna entidad ambigua hasta que se la reescribe, y la reescrita sí.
        var etiquetasReferenciadas = mencionesNuevas is { Count: > 0 }
            ? mencionesNuevas.Select(m => MarcadoresDeReferencias.Etiqueta(m.Tipo, m.Entidad)).ToArray()
            : null;

        var aclaracion = DetectorDeAmbiguedad.Detectar(interpretada, catalogo, etiquetasReferenciadas);
        if (aclaracion is not null)
        {
            conversacion.Pendiente(aclaracion);
            return FabricasDelResultado.NecesitaAclaracion(conversacion, aclaracion, interpretada, mensaje);
        }

        // 7 — CARRIL SQL. Es el único paso que no puede resolverse sin modelo: sin generación no
        // hay consulta, y sin consulta no hay nada que ejecutar.
        if (!hayModelo)
        {
            return FabricasDelResultado.Degradado(
                conversacion,
                await TextoSinModeloAsync(actor, motivo, ct),
                CausasDelTurno.DeMotivoSinModelo(motivo));
        }

        var aMostrar = string.Equals(interpretada, mensaje, StringComparison.Ordinal)
            ? null
            : interpretada;

        // Del SEGMENTO VIGENTE y con el mismo tope que el historial de preguntas.
        var consultasAnteriores = conversacion.ConsultasVigentes(
            valores.TopeDeTurnosDelHistorial);

        // LOS MARCADORES QUE EL SEGMENTO YA TRAÍA (design.md D11), del mismo recorte que
        // `consultasAnteriores`: para que un seguimiento que edita o anida una consulta anterior
        // pueda reusar su marcador sin que el validador lo vea como no declarado.
        var referenciasHeredadas = conversacion.ReferenciasVigentes(valores.TopeDeTurnosDelHistorial);

        // EL CONTEO ES DE TODA LA CONVERSACIÓN (design.md D5 de asistente-rechazos-dinamicos): se
        // lee del hilo, nunca del cliente — el pedido sólo trae el id del hilo, y cualquier conteo
        // en el cuerpo sería forjable y le dejaría al cliente fijar la primera variante para
        // siempre.
        IReadOnlyList<string>? preguntasAnteriores = reescribeLaGeneracion && historial.Count > 0
            ? [.. historial.Select(turno => turno.Pregunta)]
            : null;

        var resultado = await carril.ResponderAsync(
            actor, mensaje, aMostrar, ct, consultasAnteriores, mencionesNuevas, referenciasHeredadas,
            conversacion.RechazosPrevios(), preguntasAnteriores);

        // Lo que queda en el hilo es la pregunta RESUELTA: con la reescritura en la generación es
        // la que devolvió el modelo, y es la que el próximo seguimiento tiene que continuar.
        if (reescribeLaGeneracion && resultado.PreguntaInterpretada is { } resuelta)
        {
            interpretada = resuelta;
        }

        // La consulta que respondió, no la que se generó: con reintento el carril ya dejó en
        // SqlEjecutado la segunda.
        conversacion.Agregar(
            interpretada,
            reloj.GetUtcNow(),
            resultado.SqlEjecutado,
            claveDelCliente: claveDelCliente,
            inicioDeSegmentoAntes: inicioDeSegmentoAntes,
            aclaracionPendienteAntes: aclaracionAntes,
            huboAclaracionAntes: true,
            referencias: resultado.ReferenciasEjecutadas,
            estado: resultado.Estado);

        // EL PLAN COMPILADO PUEDE TERMINAR EN UNA ACLARACIÓN CON OPCIONES
        // (asistente-plan-compilado, D5): queda pendiente igual que la del detector, para que la
        // respuesta del usuario se reconozca como elección.
        if (resultado is { Estado: EstadoDelTurno.NecesitaAclaracion, Opciones.Count: > 0 })
        {
            conversacion.Pendiente(new Aclaracion("la pregunta", interpretada, resultado.Opciones));
        }

        // En el pivote la pregunta interpretada se devuelve SIEMPRE, aunque
        // coincida con el mensaje: es la señal de que el asistente soltó el tema
        // anterior, y sin ella el usuario no tiene forma de saberlo.
        var cerrado = await CierreDelTurno.CompletarAsync(
            resultado,
            CierreDelTurno.TextoDelRechazo(resultado, historial, interpretada),
            vinculos,
            fichas,
            log,
            ct);

        return cerrado with
        {
            Hilo = conversacion.Id,
            PreguntaInterpretada = pivote
                ? interpretada
                : resultado.PreguntaInterpretada,
        };
    }

    /// <summary>Resuelve la respuesta del usuario a un menú abierto.</summary>
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

        return (mensaje, FabricasDelResultado.NecesitaAclaracion(
            conversacion, pendiente, pendiente.PreguntaOriginal, mensaje));
    }

    /// <summary>El texto de la degradación, que distingue las dos causas.</summary>
    private async Task<string> TextoSinModeloAsync(Guid actor, MotivoSinModelo motivo, CancellationToken ct) =>
        motivo switch
        {
            MotivoSinModelo.CuotaAgotada =>
                PoliticaDeAbstencion.TextoCuotaAgotada(await disponibilidad.CupoVuelveAAsync(actor, ct)),
            MotivoSinModelo.TopeOrganizacionalAgotado => PoliticaDeAbstencion.TextoTopeOrganizacionalAgotado,
            MotivoSinModelo.SinAcceso => PoliticaDeAbstencion.TextoSinAcceso,
            MotivoSinModelo.Mantenimiento =>
                PoliticaDeAbstencion.TextoMantenimiento((await disponibilidadDelModulo.ConsultarAsync(ct)).Razon),
            _ => PoliticaDeAbstencion.TextoServicioDegradado,
        };

    /// <summary>
    /// La reescritura, o la pregunta tal cual si el proveedor falla (asistente-proveedor-local,
    /// design.md D5).
    /// </summary>
    private async Task<string> ReescribirOCrudaAsync(
        string pregunta, IReadOnlyList<TurnoDelHilo> turnosAnteriores, CancellationToken ct)
    {
        try
        {
            return await reescritor.ReescribirAsync(pregunta, turnosAnteriores, ct);
        }
        catch (Exception excepcion) when (excepcion is ProveedorSaturado
            or ProveedorNoDisponible
            or TimeoutDelProveedor
            or HttpRequestException)
        {
            log.LogWarning(
                excepcion, "No se pudo reescribir el seguimiento; se sigue con la pregunta tal cual.");
            return pregunta;
        }
    }
}
