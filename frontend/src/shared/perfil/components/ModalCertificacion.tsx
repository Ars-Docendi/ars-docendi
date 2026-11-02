import { useState } from "react";
import { Button, DatePicker, Field, Input, Modal } from "@ars-docendi/ui";

import type { Certificacion, DatosCertificacion } from "../types";
import "./portal.css";

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
      <div className="portal-form">
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
        <div className="portal-form-grid">
          <Field label="Fecha" required error={errores.fecha}>
            <DatePicker
              value={datos.fecha}
              onChange={(e) => setDatos({ ...datos, fecha: e.target.value })}
            />
          </Field>
          <Field label="Vencimiento">
            <DatePicker
              value={datos.vencimiento ?? ""}
              onChange={(e) => setDatos({ ...datos, vencimiento: e.target.value })}
            />
          </Field>
        </div>
      </div>
    </Modal>
  );
}
