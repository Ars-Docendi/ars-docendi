import { useState } from "react";
import { Button, Field, Input } from "@ars-docendi/ui";
import { CampoFechaISO } from "../../ui/CampoFechaISO";

import type { Certificacion, DatosCertificacion } from "../types";
import "./portal.css";
import { Modal } from "../../ui/Modal";

const VACIA: DatosCertificacion = {
  nombre: "",
  emisor: "",
  fecha: "",
  vencimiento: "",
};

interface ModalCertificacionProps {
  certificacion: Certificacion | null;
  onCerrar: () => void;
  onGuardar: (datos: DatosCertificacion) => void;
}

export function ModalCertificacion({
  certificacion,
  onCerrar,
  onGuardar,
}: ModalCertificacionProps) {
  const [datos, setDatos] = useState<DatosCertificacion>(() =>
    certificacion ? { ...certificacion } : VACIA,
  );
  const [errores, setErrores] = useState<Record<string, string>>({});
  // Lo cargado se compara con lo que se abrió: cerrar con datos sin guardar pide confirmar.
  const [inicial] = useState(() => JSON.stringify(datos));

  function guardar() {
    const nuevos: Record<string, string> = {};
    if (!datos.nombre.trim()) nuevos.nombre = "Ingresá el nombre de la certificación.";
    if (!datos.emisor.trim()) nuevos.emisor = "Ingresá quién la emitió.";
    if (!datos.fecha) nuevos.fecha = "Ingresá la fecha.";
    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) return;

    onGuardar({
      nombre: datos.nombre.trim(),
      emisor: datos.emisor.trim(),
      fecha: datos.fecha,
      vencimiento: datos.vencimiento || null,
    });
  }

  return (
    <Modal
      open
      sucio={JSON.stringify(datos) !== inicial}
      onOpenChange={(abierto) => !abierto && onCerrar()}
      title={certificacion ? "Editar certificación" : "Agregar certificación"}
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
      <div className="adoc-form-seccion">
        <Field label="Nombre" required error={errores.nombre}>
          <Input
            value={datos.nombre}
            onChange={(e) => setDatos({ ...datos, nombre: e.target.value })}
          />
        </Field>
        <Field label="Emisor" required error={errores.emisor}>
          <Input
            value={datos.emisor}
            onChange={(e) => setDatos({ ...datos, emisor: e.target.value })}
          />
        </Field>
        <div className="adoc-form-fila cols-2">
          <Field label="Fecha" required error={errores.fecha}>
            <CampoFechaISO
              value={datos.fecha}
              onChange={(valor) => setDatos({ ...datos, fecha: valor })}
            />
          </Field>
          <Field label="Vencimiento">
            <CampoFechaISO
              value={datos.vencimiento ?? ""}
              onChange={(valor) => setDatos({ ...datos, vencimiento: valor })}
            />
          </Field>
        </div>
      </div>
    </Modal>
  );
}
