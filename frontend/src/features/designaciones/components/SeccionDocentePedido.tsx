import { Field, Input, Select } from "@ars-docendi/ui";
import type {
  Cargo,
  Dedicacion,
  DocenteExistente,
  DocentePedido,
  MateriaPedido,
  Novedad,
} from "../types";
import { formatearDni } from "../api/catalogos";
import { DatosActualesPanel } from "./DatosActualesPanel";
import { SeccionMateriaHoras } from "./SeccionMateriaHoras";

interface SeccionDocentePedidoProps {
  novedad: Novedad;
  docente: DocentePedido;
  errorDocente?: string;
  opcionesDocente: DocenteExistente[];
  materias: MateriaPedido[];
  materiaId?: string;
  errorMateria?: string;
  cargoActual: Cargo | null;
  cargoSolicitado?: Cargo;
  dedicacionActual: Dedicacion | null;
  dedicacionSolicitada?: Dedicacion;
  materia: string;
  horasActuales?: number | null;
  horasSolicitadas?: number;
  horasInvestigacionActuales?: number | null;
  horasInvestigacionSolicitadas?: number;
  horasExternasActuales?: number | null;
  horasExternasSolicitadas?: number;
  onSeleccionarDocente: (dni: string) => void;
  onCambiarPersona: (campo: "dni" | "nombrePersona" | "apellido", valor: string) => void;
  onSeleccionarMateria: (materiaId: string) => void;
}

function notaDocente(novedad: Novedad): string {
  switch (novedad) {
    case "Alta":
      return "Ingresá los datos de la persona nueva y adjuntá la documentación obligatoria.";
    case "Baja":
      return "Seleccioná el docente que se da de baja. Sus datos actuales son de solo lectura.";
    case "Cambio de cargo o dedicación":
      return "Editás una designación existente. Los datos actuales provienen del sistema; modificá sólo lo que solicitás cambiar.";
    default:
      return "Seleccioná el docente. Sus datos actuales provienen del sistema (solo lectura).";
  }
}

export function SeccionDocentePedido({
  novedad,
  docente,
  errorDocente,
  opcionesDocente,
  materias,
  materiaId,
  errorMateria,
  cargoActual,
  cargoSolicitado,
  dedicacionActual,
  dedicacionSolicitada,
  materia,
  horasActuales,
  horasSolicitadas,
  horasInvestigacionActuales,
  horasInvestigacionSolicitadas,
  horasExternasActuales,
  horasExternasSolicitadas,
  onSeleccionarDocente,
  onCambiarPersona,
  onSeleccionarMateria,
}: SeccionDocentePedidoProps) {
  const esAlta = novedad === "Alta";
  const esBaja = novedad === "Baja";
  const muestraMateria = esAlta ? true : Boolean(docente.dni);
  const muestraDatosActuales = Boolean(docente.dni && materiaId && cargoActual && dedicacionActual);

  return (
    <section className="adoc-pf-sec">
      <h2 className="adoc-pf-sec-h">{esAlta ? "Datos del docente · Nuevo" : "Docente"}</h2>
      <p className="adoc-pf-note">{notaDocente(novedad)}</p>

      {esAlta ? (
        <div className="adoc-pf-row">
          <Field label="DNI" error={errorDocente}>
            <Input value={docente.dni} onChange={(e) => onCambiarPersona("dni", e.target.value)} />
          </Field>
          <Field label="Nombre">
            <Input
              value={docente.nombrePersona ?? ""}
              onChange={(e) => onCambiarPersona("nombrePersona", e.target.value)}
            />
          </Field>
          <Field label="Apellido">
            <Input
              value={docente.apellido ?? ""}
              onChange={(e) => onCambiarPersona("apellido", e.target.value)}
            />
          </Field>
        </div>
      ) : (
        <Field label="Docente" error={errorDocente}>
          <Select value={docente.dni} onChange={(e) => onSeleccionarDocente(e.target.value)}>
            <option value="">Seleccioná un docente…</option>
            {opcionesDocente.map((item) => (
              <option key={item.personaId} value={item.dni}>
                {item.nombre} · DNI {formatearDni(item.dni)}
              </option>
            ))}
          </Select>
        </Field>
      )}

      {muestraMateria && (
        <Field label="Materia" error={errorMateria}>
          {materias.length === 1 ? (
            <output className="adoc-pf-readonly">{materias[0].nombre}</output>
          ) : (
            <Select value={materiaId ?? ""} onChange={(e) => onSeleccionarMateria(e.target.value)}>
              <option value="">Seleccioná una materia…</option>
              {materias.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.nombre}
                </option>
              ))}
            </Select>
          )}
        </Field>
      )}

      {!esAlta && muestraDatosActuales && (
        <DatosActualesPanel
          antiguedad={docente.antiguedad}
          cargoActual={cargoActual ?? ""}
          cargoSolicitado={cargoSolicitado}
          dedicacionActual={dedicacionActual ?? ""}
          dedicacionSolicitada={dedicacionSolicitada}
          materia={materia}
          horasActuales={horasActuales}
          horasSolicitadas={horasSolicitadas}
          mostrarMateria={novedad === "Sin novedad"}
          horasInvestigacionActuales={horasInvestigacionActuales}
          horasInvestigacionSolicitadas={horasInvestigacionSolicitadas}
          horasExternasActuales={horasExternasActuales}
          horasExternasSolicitadas={horasExternasSolicitadas}
        />
      )}
      {!esAlta && muestraDatosActuales && esBaja && (
        <SeccionMateriaHoras
          materia={materia}
          etiquetaMateria="Materia seleccionada"
          horas={horasActuales ?? 0}
        />
      )}
    </section>
  );
}
