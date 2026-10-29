using ArsDocendi.Migraciones;
using ArsDocendi.Shared.Persistencia;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services.Integridad;

namespace Modules.Designaciones.Infrastructure;

/// <summary>
/// Relaciones locales más la cadena de sellos de cada período [BR-designaciones-019].
/// Solo categorías técnicas e ids; Host compara destinos sin acceso cruzado.
/// </summary>
internal sealed class DiagnosticoDesignaciones(
    DesignacionesDbContext db, RepositorioSellos sellos, VerificadorIntegridadPedidos verificador)
    : IDiagnosticoIntegridadModulo
{
    private readonly Relaciones relaciones = new(db);

    public string Contexto => relaciones.Contexto;

    public async Task<IReadOnlyList<RegistroIntegridad>> LeerAsync(CancellationToken ct)
    {
        var registros = (await relaciones.LeerAsync(ct)).ToList();
        foreach (var periodoId in await sellos.ListarPeriodosAsync(ct))
        {
            var resultado = await verificador.VerificarAsync(periodoId, ct);
            registros.AddRange(resultado.Hallazgos.Select(h =>
                new RegistroIntegridad("hallazgo", "designaciones-" + h.Categoria, h.Id, null, null)));
        }
        return registros;
    }

    private sealed class Relaciones(DesignacionesDbContext db)
        : DiagnosticoEfSql<DesignacionesDbContext>(db, "Designaciones", """
        SELECT 'referencia', 'designaciones-persona', id, 'persona', persona_id FROM designaciones.designaciones
        UNION ALL SELECT 'referencia', 'designaciones-materia', id, 'materia', materia_id FROM designaciones.designaciones
        UNION ALL SELECT 'referencia', 'designaciones-persona', id, 'persona', persona_id FROM designaciones.pedidos
        UNION ALL SELECT 'referencia', 'designaciones-materia', id, 'materia', materia_id FROM designaciones.pedidos
        UNION ALL SELECT 'referencia', 'designaciones-archivo', id, 'archivo', archivo_id FROM designaciones.pedido_adjuntos WHERE archivo_id IS NOT NULL
        UNION ALL SELECT 'hallazgo', 'designaciones-pedido-incompleto', id, NULL::text, NULL::uuid
        FROM designaciones.pedidos
        WHERE novedad IN ('Alta','Cambio de cargo o dedicación')
          AND estado IN ('en_revision_coordinador','en_revision_secretaria','en_revision_decanato','devuelto','en_lote')
          AND (cargo_solicitado_id IS NULL OR dedicacion_solicitada_id IS NULL OR horas IS NULL OR horas <= 0)
        """);
}
