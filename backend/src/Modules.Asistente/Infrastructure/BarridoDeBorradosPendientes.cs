using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Purga físicamente las conversaciones cuya ventana de «Deshacer» venció
/// (asistente-rediseno-v3, design.md D4 de asistente-historial-conversaciones).
/// </summary>
/// <remarks>
/// <b>La finalidad LÓGICA ya la da el filtro de lectura</b> —cada consulta
/// propia excluye <c>borrado_pendiente_desde IS NOT NULL</c>, y la de soporte
/// lo hace en cuanto pasa esta misma ventana—; esta clase es lo que la hace
/// FÍSICA: borra la fila de verdad, y con ella sus turnos, por la cascada.
///
/// Scoped, igual que <c>PurgaDeRegistros</c> y por el mismo motivo: pide la
/// cadena dueña, que es scoped, y el servicio que la dispara abre un scope
/// por vuelta en vez de capturarla para siempre. El método público
/// <see cref="BarrerAsync"/> es lo que un test llama directamente con un
/// reloj falso, sin pasar por el temporizador de <see cref="BarridoDeBorradosPendientes"/>.
/// </remarks>
internal sealed class BarridoDePendientes(
    CadenaDuena cadena, IOptions<OpcionesAsistente> opciones, TimeProvider reloj)
{
    /// <summary>
    /// Borra lo que superó la ventana. Idempotente: sin nada que borrar no
    /// falla ni escribe.
    /// </summary>
    /// <returns>Cuántas conversaciones borró (sus turnos cascadean aparte).</returns>
    public async Task<int> BarrerAsync(CancellationToken ct)
    {
        var corte = reloj.GetUtcNow() - TimeSpan.FromSeconds(opciones.Value.VentanaDeDeshacerSegundos);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            """
            DELETE FROM asistente.hilo_historico
             WHERE borrado_pendiente_desde IS NOT NULL
               AND borrado_pendiente_desde <= @corte
            """, conexion);

        comando.Parameters.AddWithValue("corte", corte);

        return await comando.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>
/// Corre <see cref="BarridoDePendientes"/> cada tanto, mientras el Host viva.
/// </summary>
/// <remarks>
/// Mismo patrón que <c>ServicioDePurga</c>: propio scope por vuelta, un fallo
/// se loguea y no tumba el servicio —un barrido caído no puede convertirse
/// en una fuente de indisponibilidad—, y el reloj se inyecta para que un test
/// pueda adelantarlo sin esperar de verdad.
/// </remarks>
internal sealed class BarridoDeBorradosPendientes(
    IServiceScopeFactory scopes,
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj,
    ILogger<BarridoDeBorradosPendientes> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var periodo = TimeSpan.FromSeconds(Math.Max(1, PeriodoOMenos()));

        using var reloj_ = new PeriodicTimer(periodo, reloj);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var barrido = scope.ServiceProvider.GetRequiredService<BarridoDePendientes>();
                await barrido.BarrerAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception excepcion)
            {
                log.LogError(
                    excepcion, "El barrido de borrados pendientes del asistente falló esta vuelta.");
            }

            if (!await reloj_.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// El período configurado, o el default si las opciones no validan —mismo
    /// criterio, y mismo motivo, que <c>ServicioDePurga.PeriodoDePurgaHorasOMenos</c>:
    /// una perilla mal configurada no puede tumbar el Host entero.
    /// </summary>
    private double PeriodoOMenos()
    {
        try
        {
            return opciones.Value.PeriodoDeBarridoDeBorradosSegundos;
        }
        catch (OptionsValidationException excepcion)
        {
            var porDefecto = new OpcionesAsistente().PeriodoDeBarridoDeBorradosSegundos;

            log.LogError(
                excepcion,
                "La configuración del asistente no valida; el barrido de borrados "
                + "pendientes arranca con el período por defecto de {PorDefecto} segundos "
                + "hasta que se corrija.",
                porDefecto);

            return porDefecto;
        }
    }
}
