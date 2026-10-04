import { Button, Field, Select } from "@ars-docendi/ui";
import type { MembresiaFila, RolMembresiaOpcion } from "./membresias";

export type { MembresiaFila, RolMembresiaOpcion } from "./membresias";

interface OpcionCatalogo {
  id: string;
  codigo: string;
  nombre: string;
  carreraId?: string | null;
  carreraNombre?: string | null;
}

interface MembresiasSelectorProps {
  filas: MembresiaFila[];
  onChange: (filas: MembresiaFila[]) => void;
  roles: RolMembresiaOpcion[];
  /** Materias canónicas: membresía de Jefe de Cátedra y de Docente. */
  materias: OpcionCatalogo[];
  /**
   * Pares materia–carrera informativos (catálogo materia–plan deduplicado): acota las
   * carreras ofrecidas al Docente a las que de verdad dictan la materia elegida.
   */
  materiasPlan?: OpcionCatalogo[];
  carreras?: OpcionCatalogo[];
  error?: string;
}

export function MembresiasSelector({
  filas,
  onChange,
  roles,
  materias,
  materiasPlan = [],
  carreras = [],
  error,
}: MembresiasSelectorProps) {
  function actualizar(i: number, cambios: Partial<MembresiaFila>) {
    onChange(filas.map((fila, indice) => (indice === i ? { ...fila, ...cambios } : fila)));
  }

  function cambiarRol(i: number, rolId: string) {
    actualizar(i, { rolId, materiaId: "", carreraId: "" });
  }

  return (
    <Field label="Membresías de rol (rol y ámbito)" required error={error}>
      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
        {filas.map((fila, i) => {
          const rol = roles.find((opcion) => opcion.id === fila.rolId);
          const esDocente = rol?.codigo === "docente";
          // Para Docente, las carreras ofrecidas son las que de verdad dictan la materia
          // elegida (catálogo informativo materia–plan); para Coordinador, todas.
          const opcionesCarrera =
            rol?.ambito === "carrera"
              ? carreras
              : esDocente
                ? materiasPlan
                    .filter((mp) => mp.id === fila.materiaId && mp.carreraId)
                    .map((mp) => ({
                      id: mp.carreraId!,
                      codigo: "",
                      nombre: mp.carreraNombre ?? "",
                    }))
                : [];
          return (
            <div key={i} style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <Select
                value={fila.rolId}
                onChange={(e) => cambiarRol(i, e.target.value)}
                aria-label={`Rol de membresía ${i + 1}`}
                style={{ flex: "1" }}
              >
                <option value="">Rol…</option>
                {roles.map((opcion) => (
                  <option key={opcion.id} value={opcion.id}>
                    {opcion.nombre}
                  </option>
                ))}
              </Select>

              {rol?.ambito === "materia" && (
                <Select
                  value={fila.materiaId}
                  onChange={(e) =>
                    actualizar(i, {
                      materiaId: e.target.value,
                      ...(esDocente ? { carreraId: "" } : {}),
                    })
                  }
                  aria-label={`Materia de membresía ${i + 1}`}
                  style={{ flex: "1.4" }}
                >
                  <option value="">Materia…</option>
                  {materias.map((materia) => (
                    <option key={materia.id} value={materia.id}>
                      {materia.codigo} – {materia.nombre}
                    </option>
                  ))}
                </Select>
              )}

              {(rol?.ambito === "carrera" || esDocente) && (
                <Select
                  value={fila.carreraId}
                  onChange={(e) => actualizar(i, { carreraId: e.target.value })}
                  aria-label={`Carrera de membresía ${i + 1}`}
                  style={{ flex: "1.4" }}
                >
                  <option value="">Carrera…</option>
                  {opcionesCarrera.map((carrera) => (
                    <option key={carrera.id} value={carrera.id}>
                      {carrera.codigo ? `${carrera.codigo} – ` : ""}
                      {carrera.nombre}
                    </option>
                  ))}
                </Select>
              )}

              {filas.length > 1 && (
                <button
                  type="button"
                  onClick={() => onChange(filas.filter((_, indice) => indice !== i))}
                  aria-label={`Quitar membresía ${i + 1}`}
                  style={{
                    border: "1px solid var(--color-border-default)",
                    background: "#fff",
                    borderRadius: "var(--radius-xs)",
                    width: "28px",
                    height: "36px",
                    cursor: "pointer",
                  }}
                >
                  ×
                </button>
              )}
            </div>
          );
        })}
        <Button
          type="button"
          variant="secondary"
          size="sm"
          onClick={() => onChange([...filas, { rolId: "", materiaId: "", carreraId: "" }])}
        >
          + Agregar membresía
        </Button>
      </div>
    </Field>
  );
}
