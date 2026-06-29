import { Field, Input, Select } from "@ars-docendi/ui";
import type { Cargo, Dedicacion, DocenteExistente, DocentePedido, Novedad } from "../types";
import { formatearDni } from "../api/catalogos";
import { DatosActualesPanel } from "./DatosActualesPanel";

interface SeccionDocentePedidoProps {
  novedad: Novedad;
  docente: DocentePedido;
  errorDocente?: string;
  /** Catálogo de docentes seleccionables (novedades sobre docente existente). */
  opcionesDocente: DocenteExistente[];
  cargoActual: Cargo | null;
  dedicacionActual: Dedicacion | null;
  materia: string;
  /** Cambio: dedicación solicitada, para mostrar la transición en el panel. */
  dedicacionSolicitada?: Dedicacion;
  /** Edición de los datos de un docente nuevo (Alta). */
  onCambiarDocente: (docente: DocentePedido) => void;
  /** Selección de un docente existente por DNI. */
  onSeleccionarDocente: (dni: string) => void;
}

/** Nota contextual bajo el encabezado de la sección, según la novedad. */
function notaDocente(novedad: Novedad): string {
  switch (novedad) {
    case "Alta":
      return "Es un Alta: el docente todavía no existe en el sistema. Cargá sus datos y adjuntá la documentación obligatoria.";
    case "Baja":
      return "Seleccioná el docente que se da de baja. Sus datos actuales son de solo lectura.";
    case "Cambio de cargo o dedicación":
      return "Editás una designación existente. Los datos actuales del docente provienen del sistema (solo lectura); modificá solo lo que solicitás cambiar.";
    default:
      return "Seleccioná el docente. Sus datos actuales provienen del sistema (solo lectura).";
  }
}

/**
 * Sección "Datos del docente". En Alta son inputs nuevos (DNI + Apellido y
 * Nombre); en el resto es un selector de docente existente + panel de datos
 * actuales en solo lectura.
 */
export function SeccionDocentePedido({
  novedad,
  docente,
  errorDocente,
  opcionesDocente,
  cargoActual,
  dedicacionActual,
  materia,
  dedicacionSolicitada,
  onCambiarDocente,
  onSeleccionarDocente,
}: SeccionDocentePedidoProps) {
  const esAlta = novedad === "Alta";
  const muestraDatosActuales = Boolean(docente.dni && cargoActual && dedicacionActual);

  return (
    <section className="adoc-pf-sec">
      <h2 className="adoc-pf-sec-h">{esAlta ? "Datos del docente · Nuevo" : "Docente"}</h2>
      <p className="adoc-pf-note">{notaDocente(novedad)}</p>

      {esAlta ? (
        <div className="adoc-pf-row">
          <Field label="DNI" error={errorDocente}>
            <Input
              value={docente.dni}
              onChange={(e) => onCambiarDocente({ ...docente, dni: e.target.value })}
              placeholder="Ej. 30111222"
            />
          </Field>
          <Field label="Apellido y Nombre">
            <Input
              value={docente.nombre}
              onChange={(e) => onCambiarDocente({ ...docente, nombre: e.target.value })}
              placeholder="Ej. Pérez, Ana"
            />
          </Field>
        </div>
      ) : (
        <>
          <Field label="Docente" error={errorDocente}>
            <Select value={docente.dni} onChange={(e) => onSeleccionarDocente(e.target.value)}>
              <option value="">Seleccioná un docente…</option>
              {opcionesDocente.map((item) => (
                <option key={item.dni} value={item.dni}>
                  {item.nombre} · DNI {formatearDni(item.dni)}
                </option>
              ))}
            </Select>
          </Field>
          {muestraDatosActuales && cargoActual && dedicacionActual && (
            <DatosActualesPanel
              antiguedad={docente.antiguedad}
              cargoActual={cargoActual}
              dedicacionActual={dedicacionActual}
              materia={materia}
              dedicacionSolicitada={dedicacionSolicitada}
            />
          )}
        </>
      )}
    </section>
  );
}
