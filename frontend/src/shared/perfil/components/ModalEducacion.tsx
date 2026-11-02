import { useState } from "react";
import { Button, Field, Input, Modal, Select } from "@ars-docendi/ui";

import {
  NIVELES_EDUCACION,
  type DatosEducacion,
  type Educacion,
  type NivelEducacion,
} from "../types";
import { CampoPeriodo } from "./CampoPeriodo";
import { MENSAJE_FALTA_ANIO, SIN_MES_SIN_ANIO, type MesSinAnio } from "./mesSinAnio";
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
  const [cursando, setCursando] = useState(() => educacion?.hasta === null);
  const [errores, setErrores] = useState<Record<string, string>>({});
  const [mesSinAnio, setMesSinAnio] = useState<MesSinAnio>(SIN_MES_SIN_ANIO);

  function guardar() {
    const nuevos: Record<string, string> = {};
    if (!datos.carrera.trim()) nuevos.carrera = "Ingresá la carrera o el título.";
    if (!datos.institucion.trim()) nuevos.institucion = "Ingresá la institución.";
    if (mesSinAnio.desde) nuevos.desde = MENSAJE_FALTA_ANIO;
    else if (!datos.desde.trim()) nuevos.desde = "Ingresá desde cuándo.";
    if (mesSinAnio.hasta && !cursando) nuevos.hasta = MENSAJE_FALTA_ANIO;
    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) return;

    onGuardar({
      nivel: datos.nivel,
      carrera: datos.carrera.trim(),
      institucion: datos.institucion.trim(),
      desde: datos.desde.trim(),
      hasta: cursando ? null : datos.hasta?.trim() || null,
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
        <CampoPeriodo
          desde={datos.desde}
          hasta={datos.hasta}
          enCurso={cursando}
          etiquetaEnCurso="Estoy cursando"
          errorDesde={errores.desde}
          errorHasta={errores.hasta}
          onDesde={(desde) => setDatos({ ...datos, desde })}
          onHasta={(hasta) => setDatos({ ...datos, hasta })}
          onMesSinAnio={(campo, falta) =>
            setMesSinAnio((previo) => ({ ...previo, [campo]: falta }))
          }
          onEnCurso={setCursando}
        />
      </div>
    </Modal>
  );
}
