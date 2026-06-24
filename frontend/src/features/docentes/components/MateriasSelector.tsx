import { Select } from "@ars-docendi/ui";
import { MATERIAS_CATALOGO } from "../mock/mockStore";

const estiloBotonQuitar: React.CSSProperties = {
  border: "1px solid var(--color-border-default)",
  background: "#fff",
  borderRadius: "var(--radius-xs)",
  width: "36px",
  height: "36px",
  cursor: "pointer",
  color: "var(--color-text-secondary)",
  fontSize: "16px",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  flexShrink: 0,
};

interface MateriasRowsSelectorProps {
  rows: string[];
  onChange: (rows: string[]) => void;
  error?: string;
}

export function MateriasSelector({ rows, onChange, error }: MateriasRowsSelectorProps) {
  const codigosActivos = new Set(rows.filter(Boolean));

  function actualizar(index: number, codigo: string) {
    onChange(rows.map((r, i) => (i === index ? codigo : r)));
  }

  function agregar() {
    onChange([...rows, ""]);
  }

  function quitar(index: number) {
    onChange(rows.filter((_, i) => i !== index));
  }

  return (
    <div>
      <div
        style={{
          fontSize: "0.875rem",
          fontWeight: 500,
          marginBottom: "0.375rem",
          color: "var(--color-text-default)",
        }}
      >
        Materias asignadas <span style={{ color: "var(--danger-500)" }}>*</span>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
        {rows.map((codigo, i) => (
          <div key={i} style={{ display: "flex", gap: "8px", alignItems: "center" }}>
            <Select
              value={codigo}
              onChange={(e) => actualizar(i, e.target.value)}
              style={{ flex: 1 }}
            >
              <option value="">Seleccioná una materia…</option>
              {MATERIAS_CATALOGO.filter(
                (m) => !codigosActivos.has(m.codigo) || m.codigo === codigo,
              ).map((m) => (
                <option key={m.codigo} value={m.codigo}>
                  {m.codigo} – {m.nombre}
                </option>
              ))}
            </Select>
            {rows.length > 1 && (
              <button
                type="button"
                onClick={() => quitar(i)}
                aria-label={`Quitar materia ${i + 1}`}
                style={estiloBotonQuitar}
              >
                ×
              </button>
            )}
          </div>
        ))}

        <div>
          <button
            type="button"
            onClick={agregar}
            disabled={rows.length >= MATERIAS_CATALOGO.length}
            style={{
              border: "1px dashed var(--color-border-default)",
              background: "transparent",
              borderRadius: "var(--radius-xs)",
              padding: "0 12px",
              height: "32px",
              cursor: rows.length >= MATERIAS_CATALOGO.length ? "not-allowed" : "pointer",
              color: "var(--color-text-secondary)",
              fontSize: "0.8125rem",
              opacity: rows.length >= MATERIAS_CATALOGO.length ? 0.5 : 1,
            }}
          >
            + Agregar materia
          </button>
        </div>
      </div>

      {error && (
        <p
          style={{
            margin: "0.25rem 0 0",
            fontSize: "0.8125rem",
            color: "var(--danger-500)",
          }}
        >
          {error}
        </p>
      )}
    </div>
  );
}
