import { Button, Field, Input, Select } from "@ars-docendi/ui";
import type { MateriaMock } from "../models";

export interface AsignacionRow {
  materia: string;
  cargo: string;
  horas: string;
  dedicacionId: string;
  dedicacionLegada?: string;
}

interface AsignacionesSelectorProps {
  rows: AsignacionRow[];
  onChange: (rows: AsignacionRow[]) => void;
  error?: string;
  materias: MateriaMock[];
  cargos: string[];
  dedicaciones: { id: string; nombre: string }[];
}

export function AsignacionesSelector({
  rows,
  onChange,
  error,
  materias,
  cargos,
  dedicaciones,
}: AsignacionesSelectorProps) {
  function actualizarFila(i: number, campo: keyof AsignacionRow, valor: string) {
    onChange(
      rows.map((r, idx) =>
        idx === i
          ? {
              ...r,
              [campo]: valor,
              ...(campo === "materia" ? { dedicacionLegada: undefined } : {}),
            }
          : r,
      ),
    );
  }

  function agregarFila() {
    onChange([...rows, { materia: "", cargo: "", horas: "", dedicacionId: "" }]);
  }

  function quitarFila(i: number) {
    onChange(rows.filter((_, idx) => idx !== i));
  }

  const materiasUsadas = rows.map((r) => r.materia).filter(Boolean);

  return (
    <Field label="Asignaciones (materia, cargo, dedicación y horas)" required error={error}>
      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
        {rows.map((fila, i) => {
          const opcionesMateria = materias.filter(
            (m) => !materiasUsadas.includes(m.codigo) || m.codigo === fila.materia,
          );

          return (
            <div
              key={i}
              style={{
                display: "grid",
                gridTemplateColumns: "minmax(0, 1.75fr) minmax(0, 1fr) 28px",
                gap: "0.5rem",
                alignItems: "center",
              }}
            >
              <div style={{ minWidth: 0 }}>
                <Select
                  value={fila.materia}
                  onChange={(e) => actualizarFila(i, "materia", e.target.value)}
                  aria-label={`Materia de asignación ${i + 1}`}
                  style={{ width: "100%" }}
                >
                  <option value="">Seleccioná materia…</option>
                  {opcionesMateria.map((m) => (
                    <option key={m.codigo} value={m.codigo}>
                      {m.codigo} – {m.nombre}
                    </option>
                  ))}
                </Select>
              </div>

              <div style={{ minWidth: 0 }}>
                <Select
                  value={fila.cargo}
                  onChange={(e) => actualizarFila(i, "cargo", e.target.value)}
                  aria-label={`Cargo de asignación ${i + 1}`}
                  style={{ width: "100%" }}
                >
                  <option value="">Cargo…</option>
                  {cargos.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                </Select>
              </div>

              <div style={{ minWidth: 0, gridColumn: "1 / 2" }}>
                <Select
                  value={fila.dedicacionId}
                  onChange={(e) => actualizarFila(i, "dedicacionId", e.target.value)}
                  aria-label={`Dedicación de asignación ${i + 1}`}
                  style={{ width: "100%" }}
                >
                  <option value="">{fila.dedicacionLegada ?? "Dedicación…"}</option>
                  {fila.dedicacionId && !dedicaciones.some((d) => d.id === fila.dedicacionId) && (
                    <option value={fila.dedicacionId} disabled>
                      {fila.dedicacionLegada ?? "Dedicación inactiva"}
                    </option>
                  )}
                  {dedicaciones.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.nombre}
                    </option>
                  ))}
                </Select>
              </div>

              <Input
                type="number"
                min="1"
                placeholder="Hs"
                value={fila.horas}
                onChange={(e) => actualizarFila(i, "horas", e.target.value)}
                aria-label={`Horas de asignación ${i + 1}`}
                style={{ width: "68px", gridColumn: "2 / 3", gridRow: "2" }}
              />

              {rows.length > 1 && (
                <button
                  type="button"
                  onClick={() => quitarFila(i)}
                  aria-label={`Quitar asignación ${i + 1}`}
                  style={{
                    border: "1px solid var(--color-border-default)",
                    background: "#fff",
                    borderRadius: "var(--radius-xs)",
                    width: "28px",
                    height: "36px",
                    cursor: "pointer",
                    color: "var(--color-text-secondary)",
                    fontSize: "14px",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    gridColumn: "3 / 4",
                    gridRow: "2",
                  }}
                >
                  ×
                </button>
              )}
            </div>
          );
        })}

        <div>
          <Button type="button" variant="secondary" size="sm" onClick={agregarFila}>
            + Agregar asignación
          </Button>
        </div>
      </div>
    </Field>
  );
}
