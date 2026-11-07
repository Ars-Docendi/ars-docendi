import type { CatalogosPersonas } from "../api/personasApi";
import type { FilaAsignacion } from "../asignaciones";

interface AsignacionesPersonaProps {
  filas: FilaAsignacion[];
  catalogos: CatalogosPersonas;
}

/**
 * Designaciones vigentes de la persona, sólo a modo informativo: una designación cambia
 * únicamente por un pedido aprobado (Alta, Baja o Cambio), nunca desde la administración.
 * Una designación externa muestra el sector que la designa en lugar de cargo y dedicación.
 */
export function AsignacionesPersona({ filas, catalogos }: AsignacionesPersonaProps) {
  if (filas.length === 0) {
    return (
      <span className="adoc-personas-sin-dato">
        Sin designaciones vigentes. Se crean al aprobarse un pedido de Alta.
      </span>
    );
  }

  const materia = (f: FilaAsignacion) =>
    catalogos.materiasPlan.find((m) => m.id === f.materiaId && m.carreraId === f.carreraId) ??
    catalogos.materias.find((m) => m.id === f.materiaId);
  const nombre = (lista: { id: string; nombre: string }[], id: string) =>
    lista.find((x) => x.id === id)?.nombre ?? "—";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
      <span className="adoc-personas-sin-dato">
        Las designaciones cambian sólo mediante un pedido de Alta, Baja o Cambio.
      </span>
      {filas.map((fila) => {
        const m = materia(fila);
        const sector = catalogos.sectoresExternos.find((s) => s.id === fila.sectorExternoId);
        return (
          <div key={`${fila.materiaId}|${fila.carreraId}`} className="adoc-personas-asignacion">
            <strong>
              {m ? `${m.codigo} · ${m.nombre}` : "Materia"}
              {m?.carreraNombre ? ` · ${m.carreraNombre}` : ""}
            </strong>
            <div className="adoc-personas-asignacion-detalle">
              {fila.externo ? (
                <span>
                  Docente externo · {sector ? `${sector.nombre} (${sector.codigo})` : "Sector"}
                </span>
              ) : (
                <span>
                  {nombre(catalogos.cargos, fila.cargoId)} ·{" "}
                  {nombre(catalogos.dedicaciones, fila.dedicacionId)}
                </span>
              )}
              <span>{fila.horas} h</span>
            </div>
          </div>
        );
      })}
    </div>
  );
}
