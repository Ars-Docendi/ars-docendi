import { useState } from "react";
import { Button, DatePicker, Field, InlineAlert, Input, Modal, Select } from "@ars-docendi/ui";
import type { DatosNuevaSolicitud } from "../types";
import { useMateriasPropias } from "../hooks/useSolicitudesAula";

const FORMULARIO_VACIO: DatosNuevaSolicitud = {
  dia: "",
  horarioDesde: "",
  horarioHasta: "",
  cantidadAlumnosAprox: 0,
  materiaId: "",
  comision: "",
};

interface Errores {
  dia?: string;
  horarioHasta?: string;
  cantidadAlumnosAprox?: string;
  materiaId?: string;
  comision?: string;
}

function validar(datos: DatosNuevaSolicitud): Errores {
  const errores: Errores = {};
  if (!datos.dia) errores.dia = "Elegí el día del examen.";
  if (!datos.horarioDesde || !datos.horarioHasta) {
    errores.horarioHasta = "Completá el horario desde y hasta.";
  } else if (datos.horarioHasta <= datos.horarioDesde) {
    errores.horarioHasta = "El horario hasta debe ser posterior al horario desde.";
  }
  if (!datos.cantidadAlumnosAprox || datos.cantidadAlumnosAprox <= 0) {
    errores.cantidadAlumnosAprox = "Ingresá una cantidad de alumnos mayor a cero.";
  }
  if (!datos.materiaId) errores.materiaId = "Elegí una materia.";
  if (!datos.comision.trim()) errores.comision = "Ingresá la comisión.";
  return errores;
}

interface ModalNuevaSolicitudProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  error?: string;
  guardando?: boolean;
  onGuardar: (datos: DatosNuevaSolicitud) => void;
}

/**
 * Formulario para generar una nueva solicitud de reserva de aula (Docente). La Materia es un
 * desplegable acotado a las materias asignadas al docente (no texto libre): el backend vuelve a
 * validar el `materiaId` recibido, este desplegable es solo la anticipación en el frontend.
 */
export function ModalNuevaSolicitud({
  open,
  onOpenChange,
  error,
  guardando = false,
  onGuardar,
}: ModalNuevaSolicitudProps) {
  const materias = useMateriasPropias();
  const [formulario, setFormulario] = useState<DatosNuevaSolicitud>(FORMULARIO_VACIO);
  const [errores, setErrores] = useState<Errores>({});
  const [mostrarErrores, setMostrarErrores] = useState(false);

  function actualizar<K extends keyof DatosNuevaSolicitud>(
    campo: K,
    valor: DatosNuevaSolicitud[K],
  ) {
    setFormulario((prev) => ({ ...prev, [campo]: valor }));
  }

  function handleGuardar() {
    const erroresActuales = validar(formulario);
    setErrores(erroresActuales);
    setMostrarErrores(true);
    if (Object.keys(erroresActuales).length > 0) return;
    onGuardar(formulario);
  }

  function handleOpenChange(siguiente: boolean) {
    if (siguiente) {
      setFormulario(FORMULARIO_VACIO);
      setErrores({});
      setMostrarErrores(false);
    }
    onOpenChange(siguiente);
  }

  const sinMaterias = materias.isSuccess && materias.data.length === 0;

  return (
    <Modal
      open={open}
      onOpenChange={handleOpenChange}
      title="Nueva solicitud de reserva de aula"
      footer={
        <>
          <Button variant="secondary" onClick={() => handleOpenChange(false)} disabled={guardando}>
            Cancelar
          </Button>
          <Button
            variant="primary"
            onClick={handleGuardar}
            loading={guardando}
            disabled={sinMaterias}
          >
            Generar solicitud
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="danger" title="No se pudo generar la solicitud">
            {error}
          </InlineAlert>
        )}

        {materias.isError && (
          <InlineAlert severity="danger" title="No se pudieron cargar tus materias">
            Hubo un problema al obtener tus materias asignadas.{" "}
            <button onClick={() => materias.refetch()}>Reintentar</button>.
          </InlineAlert>
        )}

        {sinMaterias && (
          <InlineAlert severity="info" title="No tenés materias asignadas">
            No podés generar una solicitud sin al menos una materia a tu cargo.
          </InlineAlert>
        )}

        <Field label="Día" required error={mostrarErrores ? errores.dia : undefined}>
          <DatePicker value={formulario.dia} onChange={(e) => actualizar("dia", e.target.value)} />
        </Field>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
          <Field label="Horario desde" required>
            <Input
              type="time"
              value={formulario.horarioDesde}
              onChange={(e) => actualizar("horarioDesde", e.target.value)}
            />
          </Field>

          <Field
            label="Horario hasta"
            required
            error={mostrarErrores ? errores.horarioHasta : undefined}
          >
            <Input
              type="time"
              value={formulario.horarioHasta}
              onChange={(e) => actualizar("horarioHasta", e.target.value)}
            />
          </Field>
        </div>

        <Field
          label="Cantidad aproximada de alumnos"
          required
          error={mostrarErrores ? errores.cantidadAlumnosAprox : undefined}
        >
          <Input
            type="number"
            min={1}
            value={formulario.cantidadAlumnosAprox || ""}
            onChange={(e) => actualizar("cantidadAlumnosAprox", Number(e.target.value))}
          />
        </Field>

        <Field label="Materia" required error={mostrarErrores ? errores.materiaId : undefined}>
          <Select
            value={formulario.materiaId}
            disabled={materias.isLoading || sinMaterias}
            onChange={(e) => actualizar("materiaId", e.target.value)}
          >
            <option value="">
              {materias.isLoading ? "Cargando materias…" : "Seleccioná una materia…"}
            </option>
            {materias.data?.map((materia) => (
              <option key={materia.id} value={materia.id}>
                {materia.nombre}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Comisión" required error={mostrarErrores ? errores.comision : undefined}>
          <Input
            value={formulario.comision}
            placeholder="Ej: K3001"
            onChange={(e) => actualizar("comision", e.target.value)}
          />
        </Field>
      </div>
    </Modal>
  );
}
