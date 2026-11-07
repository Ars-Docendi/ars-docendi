import { useState } from "react";
import { Button, Field, Input, Select } from "@ars-docendi/ui";
import type {
  Cargo,
  Dedicacion,
  DocenteExistente,
  DocentePedido,
  MateriaPedido,
  Novedad,
} from "../types";
import { claveMateria, formatearDni, type PersonaBusqueda } from "../api/catalogos";
import { PopupBuscarPersona } from "./PopupBuscarPersona";
import { SelectorMateria } from "../../../shared/ui/SelectorMateria";
import { DatosActualesPanel } from "./DatosActualesPanel";
import { SeccionMateriaHoras } from "./SeccionMateriaHoras";

interface SeccionDocentePedidoProps {
  novedad: Novedad;
  docente: DocentePedido;
  errorDocente?: string;
  opcionesDocente: DocenteExistente[];
  materias: MateriaPedido[];
  materiaId?: string;
  carreraId?: string;
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
  /** Buscador de materias en lugar de lista: para quien ve muchas (no Jefe de Cátedra). */
  buscadorMaterias?: boolean;
  /** Alta de una persona ya registrada: sus datos no se editan. */
  personaExistente?: boolean;
  onElegirPersona?: (persona: PersonaBusqueda) => void;
  onPersonaNueva?: () => void;
  /** Recibe la clave de la opción (materia + carrera), no sólo el id de la materia. */
  onSeleccionarMateria: (clave: string) => void;
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
  carreraId,
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
  buscadorMaterias = false,
  personaExistente = false,
  onElegirPersona,
  onPersonaNueva,
  onSeleccionarMateria,
}: SeccionDocentePedidoProps) {
  const esAlta = novedad === "Alta";
  const [buscandoPersona, setBuscandoPersona] = useState(false);
  const esBaja = novedad === "Baja";
  const muestraMateria = esAlta ? true : Boolean(docente.dni);
  // Sólo una materia dictada en varias carreras necesita la carrera para distinguir la opción.
  const esCompartida = (item: { id: string }) =>
    materias.filter((otra) => otra.id === item.id).length > 1;
  const valorOpcion = (item: { id: string; carreraId?: string }) =>
    esCompartida(item) ? claveMateria(item) : item.id;
  const muestraDatosActuales = Boolean(docente.dni && materiaId && cargoActual && dedicacionActual);

  return (
    <section className="adoc-pf-sec">
      <h2 className="adoc-pf-sec-h">
        {esAlta
          ? personaExistente
            ? "Datos del docente · Del sistema"
            : "Datos del docente · Nuevo"
          : "Docente"}
      </h2>
      <p className="adoc-pf-note">{notaDocente(novedad)}</p>

      {esAlta ? (
        <>
          {onElegirPersona && (
            <div style={{ display: "flex", gap: "var(--space-2)", marginBottom: "var(--space-3)" }}>
              <Button variant="secondary" size="sm" onClick={() => setBuscandoPersona(true)}>
                Buscar persona del sistema
              </Button>
              {personaExistente && onPersonaNueva && (
                <Button variant="secondary" size="sm" onClick={onPersonaNueva}>
                  Cargar persona nueva
                </Button>
              )}
            </div>
          )}
          <div className="adoc-pf-row">
            <Field label="DNI" error={errorDocente}>
              <Input
                value={docente.dni}
                disabled={personaExistente}
                onChange={(e) => onCambiarPersona("dni", e.target.value)}
              />
            </Field>
            <Field label="Nombre">
              <Input
                value={docente.nombrePersona ?? ""}
                disabled={personaExistente}
                onChange={(e) => onCambiarPersona("nombrePersona", e.target.value)}
              />
            </Field>
            <Field label="Apellido">
              <Input
                value={docente.apellido ?? ""}
                disabled={personaExistente}
                onChange={(e) => onCambiarPersona("apellido", e.target.value)}
              />
            </Field>
          </div>
          {onElegirPersona && buscandoPersona && (
            <PopupBuscarPersona
              open={buscandoPersona}
              onSeleccionar={onElegirPersona}
              onCerrar={() => setBuscandoPersona(false)}
            />
          )}
        </>
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
          ) : buscadorMaterias ? (
            // Quien no es Jefe de Cátedra elige entre muchas materias: buscador.
            <SelectorMateria
              modo="par"
              opciones={materias.map((item) => ({
                id: item.id,
                codigo: item.codigo ?? "",
                nombre: item.nombre,
                carreraId: item.carreraId,
                carreraNombre: item.carreraNombre,
              }))}
              seleccion={materiaId ? { materiaId, carreraId } : null}
              onChange={(r) =>
                onSeleccionarMateria(claveMateria({ id: r.materiaId, carreraId: r.carrera?.id }))
              }
              contexto="Materia del pedido · elegí la materia y la carrera"
              ariaLabel="Materia y carrera del pedido"
              invalid={Boolean(errorMateria)}
            />
          ) : (
            <Select
              value={materiaId ? valorOpcion({ id: materiaId, carreraId }) : ""}
              onChange={(e) => onSeleccionarMateria(e.target.value)}
            >
              <option value="">Seleccioná una materia…</option>
              {materias.map((item) => (
                <option key={claveMateria(item)} value={valorOpcion(item)}>
                  {esCompartida(item) && item.carreraNombre
                    ? `${item.nombre} · ${item.carreraNombre}`
                    : item.nombre}
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
