import { Button, Field, Select } from "@ars-docendi/ui";
import { SelectorMateria } from "./SelectorMateria";
import type { MembresiaFila, RolMembresiaOpcion } from "./membresias";

export type { MembresiaFila, RolMembresiaOpcion } from "./membresias";

interface OpcionCatalogo {
  id: string;
  codigo: string;
  nombre: string;
  carreraId?: string | null;
  carreraNombre?: string | null;
  carreraCodigo?: string | null;
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

  // Cambiar el rol cambia el tipo de ámbito: la materia o carrera elegida deja de valer.
  function cambiarRol(i: number, rolId: string) {
    actualizar(i, { rolId, materiaId: "", carreraId: "" });
  }

  // Jefe de Cátedra elige la materia canónica: se ofrecen también las que no tienen carrera.
  const opcionesJefe = [...materiasPlan, ...materias];

  return (
    <Field label="Membresías de rol (rol y ámbito)" required error={error}>
      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
        {filas.map((fila, i) => {
          const rol = roles.find((opcion) => opcion.id === fila.rolId);
          const esDocente = rol?.codigo === "docente";
          const porMateria = rol?.ambito === "materia";
          // Sin duplicados: la misma membresía (rol y ámbito) en otra fila no se puede elegir.
          const otras = filas.filter((otra, indice) => indice !== i && otra.rolId === fila.rolId);
          return (
            <div key={i} style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <Select
                value={fila.rolId}
                onChange={(e) => cambiarRol(i, e.target.value)}
                aria-label={`Rol de membresía ${i + 1}`}
                style={{ flex: "0 0 13rem" }}
              >
                <option value="">Rol…</option>
                {roles.map((opcion) => (
                  <option key={opcion.id} value={opcion.id}>
                    {opcion.nombre}
                  </option>
                ))}
              </Select>

              {porMateria && (
                <div style={{ flex: "1 1 auto", minWidth: 0 }}>
                  <SelectorMateria
                    modo={esDocente ? "par" : "materia"}
                    opciones={esDocente ? materiasPlan : opcionesJefe}
                    seleccion={
                      fila.materiaId
                        ? { materiaId: fila.materiaId, carreraId: fila.carreraId || null }
                        : null
                    }
                    onChange={(resultado) =>
                      actualizar(i, {
                        materiaId: resultado.materiaId,
                        carreraId: esDocente ? (resultado.carrera?.id ?? "") : "",
                      })
                    }
                    contexto={
                      esDocente
                        ? `Membresía ${i + 1} · Docente: elegí la materia y la carrera`
                        : `Membresía ${i + 1} · ${rol.nombre}: la materia vale en todas sus carreras`
                    }
                    motivoNoElegible={(resultado) =>
                      otras.some(
                        (otra) =>
                          otra.materiaId === resultado.materiaId &&
                          (!esDocente || otra.carreraId === resultado.carrera?.id),
                      )
                        ? "Ya tiene esta membresía"
                        : undefined
                    }
                    ariaLabel={`Materia de membresía ${i + 1}`}
                    placeholder={
                      esDocente
                        ? "Ninguna materia y carrera seleccionada"
                        : "Ninguna materia seleccionada"
                    }
                  />
                </div>
              )}

              {rol?.ambito === "carrera" && (
                <Select
                  value={fila.carreraId}
                  onChange={(e) => actualizar(i, { carreraId: e.target.value })}
                  aria-label={`Carrera de membresía ${i + 1}`}
                  style={{ flex: "1" }}
                >
                  <option value="">Carrera…</option>
                  {carreras.map((carrera) => (
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
