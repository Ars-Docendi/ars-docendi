import { Button, Field, Input, Select } from "@ars-docendi/ui";
import type { MateriaMock } from "../models";

export interface AsignacionRow {
  materia: string;
  cargo: string;
  horas: string;
}

interface AsignacionesSelectorProps {
  rows: AsignacionRow[];
  onChange: (rows: AsignacionRow[]) => void;
  error?: string;
  materias: MateriaMock[];
  cargos: string[];
}

export function AsignacionesSelector({
  rows,
  onChange,
  error,
  materias,
  cargos,
}: AsignacionesSelectorProps) {
  function actualizarFila(i: number, campo: keyof AsignacionRow, valor: string) {
    onChange(rows.map((r, idx) => (idx === i ? { ...r, [campo]: valor } : r)));
  }

  function agregarFila() {
    onChange([...rows, { materia: "", cargo: "", horas: "" }]);
  }

  function quitarFila(i: number) {
    onChange(rows.filter((_, idx) => idx !== i));
  }

  const materiasUsadas = rows.map((r) => r.materia).filter(Boolean);

  return (
    <Field label="Asignaciones (materia + cargo + horas)" required error={error}>
      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
        {rows.map((fila, i) => {
          const opcionesMateria = materias.filter(
            (m) => !materiasUsadas.includes(m.codigo) || m.codigo === fila.materia,
          );

          return (
            <div key={i} style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <Select
                value={fila.materia}
                onChange={(e) => actualizarFila(i, "materia", e.target.value)}
                aria-label={`Materia de asignación ${i + 1}`}
                style={{ flex: "2" }}
              >
                <option value="">Seleccioná materia…</option>
                {opcionesMateria.map((m) => (
                  <option key={m.codigo} value={m.codigo}>
                    {m.codigo} – {m.nombre}
                  </option>
                ))}
              </Select>

              <Select
                value={fila.cargo}
                onChange={(e) => actualizarFila(i, "cargo", e.target.value)}
                aria-label={`Cargo de asignación ${i + 1}`}
                style={{ flex: "1" }}
              >
                <option value="">Cargo…</option>
                {cargos.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </Select>

              <Input
                type="number"
                min="1"
                placeholder="Hs"
                value={fila.horas}
                onChange={(e) => actualizarFila(i, "horas", e.target.value)}
                aria-label={`Horas de asignación ${i + 1}`}
                style={{ width: "68px", flexShrink: 0 }}
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
                    flexShrink: 0,
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
