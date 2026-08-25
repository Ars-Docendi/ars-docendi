using Microsoft.Extensions.Logging;
using Npgsql;

namespace Modules.Asistente.Application;

/// <summary>
/// Compone el carril SQL: de la pregunta en español a la respuesta redactada.
/// </summary>
/// <remarks>
/// Dos llamadas al modelo por turno —generación y redacción— y siete piezas
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
    ContadorDeLlamadasDelTurno contador,
    ILogger<CarrilSql> log)
{
    /// <summary>
    /// SQLSTATE de PostgreSQL para falta de privilegio (<c>insufficient_privilege</c>).
    /// </summary>
    private const string PrivilegioDenegado = "42501";

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
    public async Task<ResultadoDelTurno> ResponderAsync(
        Guid actor,
        string mensaje,
        string? preguntaInterpretada,
        CancellationToken ct)
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
            return await ResolverAsync(actor, mensaje, pregunta, aMostrar, perfil, ct);
        }
        catch (PostgresException excepcion) when (excepcion.SqlState == PrivilegioDenegado)
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
        catch (PostgresException excepcion)
        {
            // Cualquier otro rechazo del motor: SQL que el validador dejó pasar y
            // no ejecuta, un tipo incompatible, un timeout de sentencia. El mensaje
            // crudo nombra tablas y columnas, así que va al registro y no a la
            // respuesta.
            log.LogWarning(
                excepcion, "El motor rechazó la consulta generada ({Estado}).", excepcion.SqlState);
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
        CancellationToken ct)
    {
        var generacion = await generador.GenerarAsync(pregunta, perfil.VeDatosPersonales, ct);

        if (!generacion.EsContestable || generacion.Sql is null)
        {
            // Corta acá: sin consulta no hay nada que ejecutar y no hay nada que
            // redactar, así que la segunda llamada no se hace.
            return NoContestable(generacion, aMostrar, PoliticaDeAbstencion.TextoNoContestable);
        }

        var veredicto = ValidadorDeSql.Validar(generacion.Sql);
        if (!veredicto.EsValida)
        {
            // El motivo va al registro, no a la respuesta: nombra construcciones
            // de SQL y quien lee la respuesta es el usuario final.
            log.LogWarning(
                "El validador rechazó la consulta generada: {Motivo}", veredicto.Motivo);

            return NoContestable(
                generacion, aMostrar, PoliticaDeAbstencion.TextoRechazadaPorValidador);
        }

        var resultado = await ejecutor.EjecutarAsync(
            generacion.Sql, actor, perfil.VeDatosPersonales, ct);

        if (resultado.EstaVacio && PoliticaDeAbstencion.ConvieneReintentar(resultado, perfil.EsGlobal))
        {
            (generacion, resultado) = await ReintentarAsync(
                actor, pregunta, generacion, resultado, perfil, ct);
        }

        return resultado.EstaVacio
            ? Vacio(generacion, aMostrar, perfil.EsGlobal)
            : await RedactadoAsync(mensaje, generacion, aMostrar, resultado, perfil, ct);
    }

    /// <summary>
    /// Vuelve a generar una vez, cuando el vacío no puede explicarse por el
    /// alcance.
    /// </summary>
    /// <remarks>
    /// Si la segunda generación no es contestable o su consulta no valida, se
    /// conserva el resultado de la primera: un reintento peor que el original no
    /// tiene por qué reemplazarlo.
    /// </remarks>
    private async Task<(GeneracionDeSql, ResultadoDeConsulta)> ReintentarAsync(
        Guid actor,
        string pregunta,
        GeneracionDeSql original,
        ResultadoDeConsulta resultadoOriginal,
        PerfilDelActor perfil,
        CancellationToken ct)
    {
        contador.MarcarReintento();

        var segunda = await generador.GenerarAsync(pregunta, perfil.VeDatosPersonales, ct);

        if (!segunda.EsContestable
            || segunda.Sql is null
            || !ValidadorDeSql.Validar(segunda.Sql).EsValida)
        {
            return (original, resultadoOriginal);
        }

        var resultado = await ejecutor.EjecutarAsync(
            segunda.Sql, actor, perfil.VeDatosPersonales, ct);

        return resultado.EstaVacio ? (original, resultadoOriginal) : (segunda, resultado);
    }

    private async Task<ResultadoDelTurno> RedactadoAsync(
        string mensaje,
        GeneracionDeSql generacion,
        string? aMostrar,
        ResultadoDeConsulta resultado,
        PerfilDelActor perfil,
        CancellationToken ct)
    {
        // LA FRONTERA DE SALIDA. Lo que va al modelo es el resultado enmascarado;
        // lo que vuelve al llamador son las filas reales. Cambiar el orden de estas
        // dos líneas, o pasarle `resultado` al redactor, manda datos personales al
        // proveedor sin que nada falle.
        var paraElModelo = Enmascarador.Enmascarar(resultado);
        var texto = await redactor.RedactarAsync(mensaje, paraElModelo, perfil.EsGlobal, ct);

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
            contador.Llamadas);
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
    private ResultadoDelTurno Vacio(GeneracionDeSql generacion, string? aMostrar, bool esGlobal) =>
        new(EstadoDelTurno.Respondida,
            PoliticaDeAbstencion.TextoDeResultadoVacio(esGlobal),
            generacion.Razonamiento,
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            generacion.Categoria,
            contador.Llamadas);

    private ResultadoDelTurno NoContestable(
        GeneracionDeSql generacion, string? aMostrar, string texto) =>
        new(EstadoDelTurno.NoContestable,
            texto,
            generacion.Razonamiento,
            aMostrar,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
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
