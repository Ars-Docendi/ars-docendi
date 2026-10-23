using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Borra de los dos registros lo que superó la ventana de retención (RNF-19).
/// </summary>
/// <remarks>
/// <b>«Retención de 90 días» sin un mecanismo que borre es una frase en un
/// documento.</b> Esta clase es lo que convierte la política en un hecho, y por eso
/// su test es parte del entregable y no un extra.
///
/// Corre en el proceso y no en la base. Un <c>pg_cron</c> exigiría una extensión más
/// en la provisión de cada ambiente y se testearía aparte del resto; un servicio
/// hospedado corre donde ya corre todo y se apaga con el módulo (RNF-20).
///
/// El corte se calcula contra el reloj inyectado, así que un test adelanta el tiempo
/// en vez de esperar noventa días.
/// </remarks>
internal sealed class PurgaDeRegistros(
    CadenaDuena cadena,
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj,
    ILogger<PurgaDeRegistros> log)
{
    /// <summary>
    /// Borra lo vencido. Idempotente: sin nada que borrar no falla ni escribe.
    /// </summary>
    /// <returns>Cuántas filas se borraron entre los dos registros.</returns>
    public async Task<int> PurgarAsync(CancellationToken ct)
    {
        var valores = opciones.Value;
        var corte = reloj.GetUtcNow() - TimeSpan.FromDays(valores.RetencionDeRegistrosDias);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        var operativas = await BorrarAsync(
            conexion,
            "DELETE FROM asistente.registro_operativo WHERE ocurrido_en < @corte",
            "corte",
            corte,
            ct);

        // El analítico se corta por día porque es lo único que guarda. Se compara
        // contra el día del corte y no contra el instante: la fila de ese día ya
        // perdió la hora, así que no hay forma de saber si cayó antes o después.
        var analiticas = await BorrarAsync(
            conexion,
            "DELETE FROM asistente.registro_analitico WHERE dia < @corte",
            "corte",
            DateOnly.FromDateTime(corte.UtcDateTime),
            ct);

        // EL HISTORIAL SE CORTA POR ultima_actividad, NO POR creado_en. Una
        // conversación retomada y con actividad nueve meses después no puede
        // perder sus turnos más viejos mientras la conversación misma sigue
        // vigente — retención es una propiedad de la CONVERSACIÓN (design.md
        // D7 de asistente-historial-conversaciones). El borrado cascadea a
        // turno_historico por la FK ON DELETE CASCADE: no hay un segundo
        // DELETE que pueda desincronizarse del primero.
        var corteDeHistorial = reloj.GetUtcNow() - TimeSpan.FromDays(valores.RetencionDeHistorialDias);
        var conversaciones = await BorrarAsync(
            conexion,
            "DELETE FROM asistente.hilo_historico WHERE ultima_actividad < @corte",
            "corte",
            corteDeHistorial,
            ct);

        // LA AUDITORÍA DE SOPORTE TIENE SU PROPIA VENTANA, independiente de la
        // del historial que describe: un registro de auditoría tiene que
        // sobrevivir aunque el propio usuario haya borrado la conversación que
        // el registro describe (design.md D10) — no hay ninguna relación entre
        // las dos ventanas.
        var corteDeAuditoria =
            reloj.GetUtcNow() - TimeSpan.FromDays(valores.RetencionDeAuditoriaDeSoporteDias);
        var auditorias = await BorrarAsync(
            conexion,
            "DELETE FROM asistente.auditoria_acceso_historial WHERE ocurrido_en < @corte",
            "corte",
            corteDeAuditoria,
            ct);

        // LA AUDITORÍA DE ADMINISTRACIÓN TIENE SU PROPIA VENTANA, por el mismo
        // motivo que la de soporte de arriba (design.md D11 de
        // asistente-administracion-de-uso): un presupuesto se puede volver a
        // editar, y el registro de que en algún momento valió tal cosa no
        // tiene por qué desaparecer sólo porque el valor cambió de nuevo.
        var corteDeAuditoriaDeAdministracion =
            reloj.GetUtcNow() - TimeSpan.FromDays(valores.RetencionDeAuditoriaDeAdministracionDias);
        var auditoriasDeAdministracion = await BorrarAsync(
            conexion,
            "DELETE FROM asistente.auditoria_administracion WHERE ocurrido_en < @corte",
            "corte",
            corteDeAuditoriaDeAdministracion,
            ct);

        // BACKSTOP DEL BARRIDO DE UN MINUTO (design.md D4 de
        // asistente-historial-conversaciones): `BarridoDeBorradosPendientes`
        // ya corre esta misma sentencia cada
        // `PeriodoDeBarridoDeBorradosSegundos`; ésta es la red para el
        // despliegue en que ese servicio no llegó a correr — un borrado
        // pendiente vencido no puede quedar sin purgar más allá de la
        // próxima purga diaria.
        var corteDeBorradosPendientes =
            reloj.GetUtcNow() - TimeSpan.FromSeconds(valores.VentanaDeDeshacerSegundos);
        var borradosPendientes = await BorrarAsync(
            conexion,
            """
            DELETE FROM asistente.hilo_historico
             WHERE borrado_pendiente_desde IS NOT NULL
               AND borrado_pendiente_desde <= @corte
            """,
            "corte",
            corteDeBorradosPendientes,
            ct);

        var total = operativas + analiticas + conversaciones + auditorias
            + auditoriasDeAdministracion + borradosPendientes;

        if (total > 0)
        {
            log.LogInformation(
                "Purga del asistente: {Operativas} filas operativas, {Analiticas} analíticas, "
                + "{Conversaciones} conversaciones, {Auditorias} auditorías de soporte, "
                + "{AuditoriasDeAdministracion} auditorías de administración y "
                + "{BorradosPendientes} borrados pendientes anteriores a sus respectivos cortes.",
                operativas,
                analiticas,
                conversaciones,
                auditorias,
                auditoriasDeAdministracion,
                borradosPendientes);
        }

        return total;
    }

    private static async Task<int> BorrarAsync(
        NpgsqlConnection conexion,
        string sql,
        string parametro,
        object valor,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddWithValue(parametro, valor);

        return await comando.ExecuteNonQueryAsync(ct);
    }
}
