import { Button, Input, Select } from "@ars-docendi/ui";

import { SelectorMateria } from "../../../shared/ui/SelectorMateria";
import type { CatalogosPersonas } from "../api/personasApi";
import { FILA_ASIGNACION_VACIA, type FilaAsignacion } from "../asignaciones";

interface AsignacionesPersonaProps {
  filas: FilaAsignacion[];
  onChange: (filas: FilaAsignacion[]) => void;
  catalogos: CatalogosPersonas;
  soloLectura: boolean;
}

/**
 * Cada asignación muestra la materia y la carrera completas en un renglón y debajo el cargo,
 * la dedicación y las horas. Una persona tiene a lo sumo una designación por materia canónica.
 */
export function AsignacionesPersona({
  filas,
  onChange,
  catalogos,
  soloLectura,
}: AsignacionesPersonaProps) {
  const actualizar = (i: number, cambios: Partial<FilaAsignacion>) =>
    onChange(filas.map((f, j) => (j === i ? { ...f, ...cambios } : f)));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
      {filas.length === 0 && <span className="adoc-personas-sin-dato">Sin asignaciones.</span>}
      {filas.map((fila, i) => {
        const otras = new Set(filas.filter((_, j) => j !== i).map((f) => f.materiaId));
        return (
          <div key={i} className="adoc-personas-asignacion">
            <div style={{ display: "flex", gap: "var(--space-2)", alignItems: "center" }}>
              <div style={{ flex: 1, minWidth: 0 }}>
                <SelectorMateria
                  modo="par"
                  opciones={catalogos.materiasPlan}
                  seleccion={
                    fila.materiaId ? { materiaId: fila.materiaId, carreraId: fila.carreraId } : null
                  }
                  onChange={(r) =>
                    actualizar(i, { materiaId: r.materiaId, carreraId: r.carrera?.id ?? "" })
                  }
                  contexto={`Asignación ${i + 1} · elegí la materia y la carrera`}
                  motivoNoElegible={(r) => (otras.has(r.materiaId) ? "Ya asignada" : undefined)}
                  ariaLabel={`Materia y carrera de asignación ${i + 1}`}
                  soloLectura={soloLectura}
                />
              </div>
              {!soloLectura && (
                <Button
                  variant="secondary"
                  onClick={() => onChange(filas.filter((_, j) => j !== i))}
                  aria-label={`Quitar asignación ${i + 1}`}
                  style={{ width: 36, padding: 0 }}
                >
                  ×
                </Button>
              )}
            </div>
            <div className="adoc-personas-asignacion-detalle">
              <label className="adoc-personas-campo-linea" style={{ flex: "1 1 260px" }}>
                Cargo
                <Select
                  value={fila.cargoId}
                  disabled={soloLectura}
                  onChange={(e) => actualizar(i, { cargoId: e.target.value })}
                >
                  <option value="">Cargo…</option>
                  {catalogos.cargos.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nombre}
                    </option>
                  ))}
                </Select>
              </label>
              <label className="adoc-personas-campo-linea" style={{ flex: "1 1 220px" }}>
                Dedicación
                <Select
                  value={fila.dedicacionId}
                  disabled={soloLectura}
                  onChange={(e) =>
                    actualizar(i, { dedicacionId: e.target.value, dedicacionLegada: null })
                  }
                >
                  <option value="">{fila.dedicacionLegada ?? "Dedicación…"}</option>
                  {catalogos.dedicaciones.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.nombre}
                    </option>
                  ))}
                </Select>
              </label>
              <label className="adoc-personas-campo-linea" style={{ flex: "0 0 auto" }}>
                Horas
                <Input
                  type="number"
                  min="1"
                  placeholder="Hs"
                  value={fila.horas}
                  readOnly={soloLectura}
                  onChange={(e) => actualizar(i, { horas: e.target.value })}
                  style={{ width: 88 }}
                />
              </label>
            </div>
          </div>
        );
      })}
      {!soloLectura && (
        <div>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => onChange([...filas, { ...FILA_ASIGNACION_VACIA }])}
          >
            + Agregar asignación
          </Button>
        </div>
      )}
    </div>
  );
}
