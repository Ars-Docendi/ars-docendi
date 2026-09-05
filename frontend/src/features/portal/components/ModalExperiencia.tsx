import { useState } from "react";
import { Button, Field, Input, Modal, Textarea } from "@ars-docendi/ui";

import type { DatosExperiencia, Experiencia } from "../types";
import { CampoPeriodo } from "./CampoPeriodo";
import "./portal.css";

const VACIA: DatosExperiencia = {
  puesto: "",
  organizacion: "",
  desde: "",
  hasta: "",
  descripcion: "",
};

interface ModalExperienciaProps {
  /** null = alta; con valor = edición. */
  experiencia: Experiencia | null;
  onCerrar: () => void;
  onGuardar: (datos: DatosExperiencia) => void;
}

export function ModalExperiencia({ experiencia, onCerrar, onGuardar }: ModalExperienciaProps) {
  const [datos, setDatos] = useState<DatosExperiencia>(() =>
    experiencia ? { ...experiencia } : VACIA,
  );
  const [actual, setActual] = useState(() => experiencia?.hasta === null);
  const [errores, setErrores] = useState<Record<string, string>>({});

  function guardar() {
    const nuevos: Record<string, string> = {};
    if (!datos.puesto.trim()) nuevos.puesto = "Ingresá el puesto.";
    if (!datos.organizacion.trim()) nuevos.organizacion = "Ingresá la organización.";
    if (!datos.desde.trim()) nuevos.desde = "Ingresá desde cuándo.";
    if (!datos.descripcion.trim()) nuevos.descripcion = "Contá de qué se trató.";
    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) return;

    onGuardar({
      puesto: datos.puesto.trim(),
      organizacion: datos.organizacion.trim(),
      desde: datos.desde.trim(),
      hasta: actual ? null : datos.hasta?.trim() || null,
      descripcion: datos.descripcion.trim(),
    });
  }

  return (
    <Modal
      open
      onOpenChange={(abierto) => !abierto && onCerrar()}
      title={experiencia ? "Editar experiencia" : "Agregar experiencia"}
      footer={
        <>
          <Button variant="secondary" onClick={onCerrar}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={guardar}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="portal-form">
        <Field label="Puesto" required error={errores.puesto}>
          <Input
            value={datos.puesto}
            onChange={(e) => setDatos({ ...datos, puesto: e.target.value })}
          />
        </Field>
        <Field label="Organización" required error={errores.organizacion}>
          <Input
            value={datos.organizacion}
            onChange={(e) => setDatos({ ...datos, organizacion: e.target.value })}
          />
        </Field>
        <CampoPeriodo
          desde={datos.desde}
          hasta={datos.hasta}
          enCurso={actual}
          etiquetaEnCurso="Sigo en este puesto"
          errorDesde={errores.desde}
          onDesde={(desde) => setDatos({ ...datos, desde })}
          onHasta={(hasta) => setDatos({ ...datos, hasta })}
          onEnCurso={setActual}
        />
        <Field label="De qué se trató" required error={errores.descripcion}>
          <Textarea
            rows={3}
            value={datos.descripcion}
            onChange={(e) => setDatos({ ...datos, descripcion: e.target.value })}
          />
        </Field>
      </div>
    </Modal>
  );
}
