import { useState } from "react";
import { Button, Field, Input, Modal, Select } from "@ars-docendi/ui";

import {
  NIVELES_EDUCACION,
  type DatosEducacion,
  type Educacion,
  type NivelEducacion,
} from "../types";
import "./portal.css";

const VACIA: DatosEducacion = {
  nivel: "Grado",
  carrera: "",
  institucion: "",
  desde: "",
  hasta: "",
};

interface ModalEducacionProps {
  educacion: Educacion | null;
  onCerrar: () => void;
  onGuardar: (datos: DatosEducacion) => void;
}

export function ModalEducacion({ educacion, onCerrar, onGuardar }: ModalEducacionProps) {
  const [datos, setDatos] = useState<DatosEducacion>(() => (educacion ? { ...educacion } : VACIA));
  const [errores, setErrores] = useState<Record<string, string>>({});

  function guardar() {
    const nuevos: Record<string, string> = {};
    if (!datos.carrera.trim()) nuevos.carrera = "Ingresá la carrera o el título.";
    if (!datos.institucion.trim()) nuevos.institucion = "Ingresá la institución.";
    if (!datos.desde.trim()) nuevos.desde = "Ingresá desde cuándo.";
    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) return;

    onGuardar({
      nivel: datos.nivel,
      carrera: datos.carrera.trim(),
      institucion: datos.institucion.trim(),
      desde: datos.desde.trim(),
      hasta: datos.hasta?.trim() || null,
    });
  }

  return (
    <Modal
      open
      onOpenChange={(abierto) => !abierto && onCerrar()}
      title={educacion ? "Editar formación" : "Agregar formación"}
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
        <Field label="Nivel" required>
          <Select
            value={datos.nivel}
            onChange={(e) => setDatos({ ...datos, nivel: e.target.value as NivelEducacion })}
          >
            {NIVELES_EDUCACION.map((nivel) => (
              <option value={nivel} key={nivel}>
                {nivel}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Carrera o título" required error={errores.carrera}>
          <Input
            value={datos.carrera}
            onChange={(e) => setDatos({ ...datos, carrera: e.target.value })}
          />
        </Field>
        <Field label="Institución" required error={errores.institucion}>
          <Input
            value={datos.institucion}
            onChange={(e) => setDatos({ ...datos, institucion: e.target.value })}
          />
        </Field>
        <div className="portal-form-grid">
          <Field label="Desde" required error={errores.desde}>
            <Input
              value={datos.desde}
              placeholder="2002"
              onChange={(e) => setDatos({ ...datos, desde: e.target.value })}
            />
          </Field>
          <Field label="Hasta">
            <Input
              value={datos.hasta ?? ""}
              placeholder="2008"
              onChange={(e) => setDatos({ ...datos, hasta: e.target.value })}
            />
          </Field>
        </div>
      </div>
    </Modal>
  );
}
