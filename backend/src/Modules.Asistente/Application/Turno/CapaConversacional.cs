using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
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
    /// <param name="ct">El token del request.</param>
    /// <param name="claveDelCliente">
    /// La <c>Idempotency-Key</c> de este turno. Se guarda en el turno que
    /// resulte (<see cref="TurnoDelHilo.ClaveDelCliente"/>), para que un
    /// reemplazo futuro pueda nombrarlo (design.md D9 de asistente-rediseno-v3).
    /// </param>
    /// <param name="reemplaza">
    /// El identificador del turno que este turno reemplaza —la
    /// <c>Idempotency-Key</c> de un turno vivo, o el <c>turno_historico.id</c>
    /// de uno reanudado—, o <c>null</c> para un turno nuevo cualquiera.
    /// </param>
    /// <exception cref="HiloAjeno">Si el hilo pertenece a otro actor.</exception>
    /// <exception cref="ReemplazoInvalido">
    /// Si <paramref name="reemplaza"/> no nombra el último turno vigente del
    /// hilo del actor. No cambia nada: ni el hilo, ni el historial, ni el cupo.
    /// </exception>
    public async Task<ResultadoDelTurno> ResponderAsync(
        Guid actor,
        Guid? hilo,
        string mensaje,
        CancellationToken ct,
        string? claveDelCliente = null,
        string? reemplaza = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);

        // EL CANDADO DEL TURNO. Primerísimo, antes de abrir el presupuesto y de
        // resolver el hilo (design.md D5 de asistente-administracion-de-uso): es
        // el chequeo más barato de todos y el más probable de rechazar al
        // instante, así que quien pierde la carrera no paga el resto.
        var candado = await candadoDelTurno.IntentarAsync(actor, ct);
        if (candado is null)
        {
            var conversacionRechazada = hilos.Resolver(hilo, actor);
            var turnoRechazado = FabricasDelResultado.Degradado(
                conversacionRechazada, PoliticaDeAbstencion.TextoTurnoConcurrente);

            // REEMPLAZO + CANDADO RECHAZADO (design.md D9, punto 4): «el
            // rechazo del candado no cambia nada». Un turno nuevo cualquiera
            // sigue anotando el rechazo en el historial como siempre —y por
            // eso lo marca como último registrado, igual que cualquier otro
            // turno registrable—; un reemplazo NO lo anota, para que el turno
            // viejo quede intacto.
            if (reemplaza is null)
            {
                await RegistrarAsync(
                    actor, conversacionRechazada, mensaje, turnoRechazado, reloj.GetUtcNow(), ct,
                    claveDelCliente, conversacionRechazada.InicioDeSegmento,
                    conversacionRechazada.AclaracionPendiente);
            }

            return turnoRechazado with
            {
                CupoRestante = await cuota.CupoRestanteAsync(actor, ct),
                Conversacion = conversacionRechazada.HiloHistorico,
            };
        }

        var valores = opciones.Value;

        // LA COTA DEL TURNO. Una sola, punta a punta, encadenada al token del
        // request. No es la suma de los timeouts de las etapas: cuatro llamadas de
        // diez segundos son cuarenta segundos de espera y cada una habría respetado
        // su límite.
        using var presupuesto = PresupuestoDelTurno.Abrir(
            ct, TimeSpan.FromSeconds(valores.PresupuestoDelTurnoSegundos), reloj);

        var conversacion = hilos.Resolver(hilo, actor);

        // VALIDACIÓN DEL OBJETIVO DE REEMPLAZO (design.md D9, punto 1): después
        // del candado, y antes de sacar nada. El objetivo se chequea contra
        // `UltimoRegistrado` —el último turno que se registró al historial,
        // SEA CUAL SEA SU CARRIL— y no contra el contexto SQL: un saludo, una
        // meta-pregunta, un menú de aclaración o una degradación pre-SQL
        // nunca entran a ese contexto, pero siguen siendo «la última
        // pregunta». Un objetivo que no coincide —o un hilo vencido, que
        // resolvió acá arriba en uno nuevo y vacío— es 409 y no cambia nada:
        // no cobra cupo (no hubo llamada al modelo) y suelta el candado
        // explícitamente, porque salir acá se salta el `finally` de abajo.
        TurnoSacado? reemplazado = null;
        if (reemplaza is not null)
        {
            var ultimo = conversacion.UltimoRegistrado;
            if (ultimo is null || !EsElObjetivoDelReemplazo(ultimo, reemplaza))
            {
                await candado.DisposeAsync();
                throw new ReemplazoInvalido(reemplaza);
            }

            // SACA EL TURNO VIEJO ANTES DE RESOLVER (design.md D9, punto 2): la
            // pregunta nueva se resuelve contra la MISMA `conversacion`, ya sin
            // su último turno —si había entrado al contexto SQL— y con el
            // segmento/aclaración que tenía antes de él — la vista sin el
            // turno reemplazado.
            reemplazado = conversacion.QuitarUltimoParaReemplazo();
        }

        // SNAPSHOT DE «ANTES DE ESTE TURNO» (design.md D9). Se captura ACÁ —
        // después de sacar el turno reemplazado, si lo hay, así que ve la
        // vista ya restaurada— y no adentro de `ResolverAsync`, porque el
        // `catch` de abajo (presupuesto vencido) nunca llega a invocarlo y
        // necesita el mismo valor: es lo que describe el contexto que ESTE
        // turno tuvo, para que un reemplazo futuro pueda restaurarlo.
        var inicioDeSegmentoAntes = conversacion.InicioDeSegmento;
        var aclaracionAntes = conversacion.AclaracionPendiente;

        var arranco = reloj.GetUtcNow();
        ResultadoDelTurno resultado;

        try
        {
            var turno = await ResolverAsync(
                actor, conversacion, mensaje, claveDelCliente,
                inicioDeSegmentoAntes, aclaracionAntes, valores, presupuesto.Token);

            if (turno.ClaveDeRetroalimentacion is { } token)
            {
                // Minted right here and not inside IRegistroDelTurno's
                // fire-and-forget write: the token has to be live before the
                // response carrying it is even built, and writing to this
                // in-memory store can't fail the turn either way.
                validezDeRetroalimentacion.Registrar(token, reloj.GetUtcNow());
            }

            // EL LADO DEL ACTOR DE D3. Este evento nombra al actor —como ya hace la
            // cuota— y nunca el token de retroalimentación: el endpoint de
            // retroalimentación es el que nombra el token, y nunca el actor. Que
            // los dos campos nunca aparezcan juntos en un mismo evento es lo que
            // impide que el logging reabra el cruce que TD-012 cierra.
            log.LogInformation("Turno del asistente resuelto para el actor {ActorId}.", actor);

            await RegistrarAsync(
                actor, conversacion, mensaje, turno, arranco, ct,
                claveDelCliente, inicioDeSegmentoAntes, aclaracionAntes, reemplazado);

            resultado = turno;
        }
        catch (OperationCanceledException) when (presupuesto.Vencio)
        {
            // Se acabó el tiempo del turno. Para quien preguntó, «no llegué a
            // tiempo» es una respuesta; una cancelación cruda no lo es.
            log.LogWarning(
                "El turno del asistente agotó su presupuesto de {Segundos}s.",
                valores.PresupuestoDelTurnoSegundos);

            var turno = FabricasDelResultado.Degradado(conversacion, PoliticaDeAbstencion.TextoServicioDegradado);
            await RegistrarAsync(
                actor, conversacion, mensaje, turno, arranco, ct,
                claveDelCliente, inicioDeSegmentoAntes, aclaracionAntes, reemplazado);

            resultado = turno;
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

            // FALLO EN UN REEMPLAZO: no cambia nada (design.md D9, punto 4). Se
            // repone el turno viejo ANTES de registrar el fallo, así que el
            // hilo queda exactamente como estaba cuando el actor pidió el
            // reemplazo.
            if (reemplazado is { } saliente)
            {
                conversacion.ReponerTrasFallo(saliente);
            }

            await RegistrarAsync(
                actor, conversacion, mensaje, FabricasDelResultado.Caido(conversacion, contador.Llamadas),
                arranco, ct, claveDelCliente, inicioDeSegmentoAntes, aclaracionAntes);

            throw;
        }
        finally
        {
            // EL CANDADO SE LIBERA ACÁ, en el mismo `finally` que ya cobraba la
            // cuota — no uno nuevo (design.md D5/D4: mismos puntos de salida). Se
            // libera SIEMPRE, incluso si el turno se cayó o el presupuesto venció
            // (los dos `catch` de arriba también terminan acá), porque
            // CandadoDelTurno.DisposeAsync usa CancellationToken.None por dentro:
            // el candado tiene que soltarse aunque el token de este turno ya esté
            // cancelado.
            await candado.DisposeAsync();

            // Se anota en `finally` para que un turno que se cayó a la mitad pague
            // igual las llamadas que llegó a emitir. Cobrar solo los turnos que
            // terminan bien haría del fallo una forma de consultar gratis.
            //
            // El guard de `contador.Llamadas > 0` es EL sitio donde vive design.md
            // D4 ("no llamó al modelo, no paga"): ICuotaDelActor.AnotarAsync ya no
            // recibe cuántas llamadas hizo el turno —se derivó de
            // registro_operativo, que ya distingue por sí solo cuánto costó cada
            // fila (asistente-administracion-de-uso)—, así que decidir SI corresponde
            // cobrar es responsabilidad de este sitio de llamada, no de la
            // implementación de la cuota.
            if (contador.Llamadas > 0)
            {
                await cuota.AnotarAsync(actor, ct);

                // Mismo guard y mismo sitio que la cuota, por el mismo D4: si
                // no hubo llamada al modelo, no hay tokens que costear.
                await presupuestoOrganizacional.AcumularAsync(
                    proveedor.Nombre,
                    reloj.GetUtcNow(),
                    contador.TokensDeEntrada,
                    contador.TokensDeSalida,
                    contador.TokensDeCache,
                    ct);
            }
        }

        // FUERA del try/finally, a propósito (tarea 7.2): el cupo restante
        // tiene que reflejar el cobro que el `finally` de arriba ACABA de
        // hacer, no el valor de antes. Adjuntarlo adentro del `finally` no
        // alcanzaría — el valor de retorno de un `try` con `return` ya queda
        // fijado antes de que el `finally` corra.
        //
        // `conversacion.HiloHistorico` SE LEE ACÁ, DESPUÉS de `RegistrarAsync`
        // (design.md D13 de asistente-rediseno-v3): esa llamada es la que lo
        // fija la primera vez que el hilo escribe una fila del historial, así
        // que leerlo antes vería siempre `null` en el primer turno de una
        // conversación nueva.
        return resultado with
        {
            CupoRestante = await cuota.CupoRestanteAsync(actor, ct),
            Conversacion = conversacion.HiloHistorico,
        };
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
    /// <param name="claveDelCliente">
    /// La <c>Idempotency-Key</c> de este turno. Queda como la identidad de
    /// <see cref="HiloConversacional.UltimoRegistrado"/> si el turno se
    /// registra, para que un reemplazo futuro pueda nombrarlo.
    /// </param>
    /// <param name="inicioDeSegmentoAntes">
    /// El segmento que el hilo tenía justo ANTES de resolver este turno. Ver
    /// <see cref="HiloConversacional.MarcarUltimoRegistrado"/>.
    /// </param>
    /// <param name="aclaracionAntes">Mismo motivo que <paramref name="inicioDeSegmentoAntes"/>.</param>
    /// <param name="reemplazado">
    /// El turno que este resultado reemplaza, o <c>null</c> para un turno nuevo
    /// cualquiera (design.md D9 de asistente-rediseno-v3). Con un turno
    /// reemplazado: se revoca su token de retroalimentación y el historial
    /// reemplaza su fila en vez de agregar una nueva.
    /// </param>
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
        TurnoSacado? reemplazado = null)
    {
        var ahora = reloj.GetUtcNow();

        // On a Respondida turn, ClaveDeRetroalimentacion already IS this row's
        // analytic id — the exact one already handed to the client. Anything else
        // never surfaces a token, so any application-generated id works: nothing
        // outside this write will ever need it.
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
                decisionSombra.Intencion),
            ct);

        // HISTORIAL PROPIO (asistente-historial-conversaciones). Se excluye
        // EXACTAMENTE Fallo, y es la ÚNICA exclusión — no hay opt-out por
        // conversación (design.md D2): un turno caído nunca produjo cuerpo
        // HTTP (el mapeo del estado revienta si se le pide un nombre público),
        // así que no hay nada coherente para mostrar en una conversación
        // retomada.
        //
        // La pregunta que se persiste es la INTERPRETADA —la misma que
        // `HiloConversacional.Agregar` ya recibe—, no el mensaje crudo: es lo
        // que Reanudar necesita para volver a poblar `HistorialVigente` con
        // turnos autocontenidos, igual que el hilo en memoria ya los guarda.
        // `PreguntaInterpretada` viene nula cuando coincide con el mensaje
        // (RF-10), así que el mensaje es el valor correcto en ese caso.
        if (turno.Estado != EstadoDelTurno.Fallo)
        {
            // MARCA ESTE TURNO COMO EL ÚLTIMO REGISTRADO, SEA CUAL SEA SU
            // CARRIL (design.md D9): es lo que deja disponible para un
            // reemplazo FUTURO, sin importar si este turno también entró al
            // contexto SQL. `TurnoHistoricoId` queda en null a propósito: un
            // turno vivo no se identifica por ahí —ver
            // `EsElObjetivoDelReemplazo`—, y el reemplazo en la base apunta a
            // «la última fila de esta conversación» sin necesitar guardar su id.
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
                ahora);

            // REEMPLAZO CON DESENLACE REGISTRABLE (design.md D9, punto 3): se
            // revoca el token viejo y el historial reemplaza su fila, en vez de
            // agregar una nueva. Nunca llega acá con `turno.Estado == Fallo`
            // —ese caso restaura el turno viejo antes de llamar y no lo pasa—,
            // así que un `reemplazado` no nulo siempre es un reemplazo que se
            // concreta.
            if (reemplazado is not null)
            {
                if (reemplazado.Identidad.ClaveDeRetroalimentacion is { } tokenViejo)
                {
                    validezDeRetroalimentacion.Revocar(tokenViejo);
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
    /// Si <paramref name="reemplaza"/> nombra al último turno registrado: su
    /// <c>Idempotency-Key</c> si es de esta sesión, o su
    /// <c>turno_historico.id</c> si es de una conversación reanudada
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
        CancellationToken ct)
    {
        // EL VEREDICTO SOBRE EL MODELO, resuelto una sola vez y ANTES del pipeline.
        // No corta el turno: los cinco pasos que no necesitan proveedor siguen
        // corriendo. Tratarlo como excepción apagaría el saludo a cero tokens y el
        // menú de aclaración justo cuando son lo único que queda en pie.
        var motivo = await disponibilidad.ConsultarAsync(actor, ct);

        // EL BYPASS DE MANTENIMIENTO ES CASO POR CASO Y VIVE ACÁ, no en el
        // puerto de almacenamiento (design.md D8): un admin con
        // asistente.administrar puede seguir usando el asistente en
        // mantenimiento —para verificar que la recuperación funcionó antes
        // de reabrirlo a todos—, pero NO está exento de su propio cupo ni del
        // tope organizacional ni de la exclusión de turno concurrente: sólo
        // se ignora el motivo Mantenimiento, y sólo ese.
        if (motivo == MotivoSinModelo.Mantenimiento)
        {
            var permisos = await identidad.ObtenerCodigosDePermisosAsync(actor, ct);
            if (permisos.Contains(Permisos.AsistenteAdministrar))
            {
                motivo = MotivoSinModelo.Ninguno;
            }
        }

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
            return FabricasDelResultado.NecesitaAclaracion(conversacion, aclaracion, interpretada, mensaje);
        }

        // 7 — CARRIL SQL. Es el único paso que no puede resolverse sin modelo: sin
        // generación no hay consulta, y sin consulta no hay nada que ejecutar. Se
        // corta ACÁ y no antes, para que todo lo anterior haya tenido su chance.
        if (!hayModelo)
        {
            return FabricasDelResultado.Degradado(conversacion, await TextoSinModeloAsync(actor, motivo, ct));
        }

        var aMostrar = string.Equals(interpretada, mensaje, StringComparison.Ordinal)
            ? null
            : interpretada;

        // Del SEGMENTO VIGENTE y con el mismo tope que el historial de preguntas.
        // Se deriva de `HistorialVigente`, así que el pivote suelta las consultas
        // por el mismo mecanismo con que suelta las preguntas: no hay una segunda
        // regla de recorte que pueda quedar desincronizada de la primera.
        var consultasAnteriores = conversacion.ConsultasVigentes(
            valores.TopeDeTurnosDelHistorial);

        var resultado = await carril.ResponderAsync(
            actor, mensaje, aMostrar, ct, consultasAnteriores);

        // La consulta que respondió, no la que se generó: con reintento el carril ya
        // dejó en SqlEjecutado la segunda. Un turno sin filas la trae nula, y ahí se
        // anota nula a propósito — ver TurnoDelHilo.
        conversacion.Agregar(
            interpretada,
            reloj.GetUtcNow(),
            resultado.SqlEjecutado,
            claveDelCliente: claveDelCliente,
            inicioDeSegmentoAntes: inicioDeSegmentoAntes,
            aclaracionPendienteAntes: aclaracionAntes,
            huboAclaracionAntes: true);

        // En el pivote la pregunta interpretada se devuelve SIEMPRE, aunque
        // coincida con el mensaje: es la señal de que el asistente soltó el tema
        // anterior, y sin ella el usuario no tiene forma de saberlo.
        return resultado with
        {
            Hilo = conversacion.Id,
            PreguntaInterpretada = pivote
                ? interpretada
                : resultado.PreguntaInterpretada,
            Respuesta = CierreDelTurno.TextoDelRechazo(resultado, historial, interpretada),
            Vinculos = await CierreDelTurno.VinculosAsync(resultado, vinculos, log, ct),
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

        return (mensaje, FabricasDelResultado.NecesitaAclaracion(
            conversacion, pendiente, pendiente.PreguntaOriginal, mensaje));
    }

    /// <summary>
    /// El texto de la degradación, que distingue las dos causas.
    /// </summary>
    /// <remarks>
    /// Distinguirlas no es cosmético. Con la cuota agotada el sistema <b>sabe</b>
    /// cuándo vuelve el cupo; con el proveedor caído no lo sabe nadie. Decir «probá
    /// en unos minutos» en el primer caso manda a reintentar a ciegas contra algo
    /// que no se destraba hasta una hora fija.
    /// </remarks>
    private async Task<string> TextoSinModeloAsync(Guid actor, MotivoSinModelo motivo, CancellationToken ct) =>
        motivo switch
        {
            MotivoSinModelo.CuotaAgotada =>
                PoliticaDeAbstencion.TextoCuotaAgotada(await disponibilidad.CupoVuelveAAsync(actor, ct)),
            MotivoSinModelo.TopeOrganizacionalAgotado => PoliticaDeAbstencion.TextoTopeOrganizacionalAgotado,
            MotivoSinModelo.Mantenimiento =>
                PoliticaDeAbstencion.TextoMantenimiento((await disponibilidadDelModulo.ConsultarAsync(ct)).Razon),
            _ => PoliticaDeAbstencion.TextoServicioDegradado,
        };

}
