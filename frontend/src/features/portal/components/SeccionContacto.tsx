import { useState } from "react";
import { Button, DataList, Field, Input } from "@ars-docendi/ui";

import type { DatosContacto } from "../types";
import { SeccionPerfil } from "./SeccionPerfil";
import "./portal.css";

const FORMATO_MAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

interface SeccionContactoProps {
  contacto: DatosContacto;
  onGuardar: (contacto: DatosContacto) => void;
}

/**
 * Contacto del docente. Es la excepción al patrón de diálogo: son dos campos y
 * es lo que más se edita, así que se edita inline dentro de la tarjeta.
 * Ambos campos son opcionales; lo único que bloquea es un mail mal formado.
 */
export function SeccionContacto({ contacto, onGuardar }: SeccionContactoProps) {
  const [editando, setEditando] = useState(false);
  const [borrador, setBorrador] = useState<DatosContacto>(contacto);
  const [errorMail, setErrorMail] = useState<string | undefined>();

  const vacia = !contacto.telefono && !contacto.mail && !editando;

  function abrir() {
    setBorrador(contacto);
    setErrorMail(undefined);
    setEditando(true);
  }

  function cancelar() {
    setEditando(false);
    setErrorMail(undefined);
  }

  function guardar() {
    const mail = borrador.mail.trim();
    if (mail && !FORMATO_MAIL.test(mail)) {
      setErrorMail("Revisá el mail: no tiene un formato válido.");
      return;
    }
    onGuardar({ telefono: borrador.telefono.trim(), mail });
    setEditando(false);
    setErrorMail(undefined);
  }

  if (editando) {
    return (
      <SeccionPerfil titulo="Contacto">
        <div className="portal-form">
          <div className="portal-form-grid">
            <Field label="Teléfono">
              <Input
                value={borrador.telefono}
                onChange={(e) => setBorrador({ ...borrador, telefono: e.target.value })}
              />
            </Field>
            <Field label="Mail" error={errorMail}>
              <Input
                type="email"
                value={borrador.mail}
                onChange={(e) => setBorrador({ ...borrador, mail: e.target.value })}
              />
            </Field>
          </div>
          <div className="portal-form-acciones">
            <Button variant="secondary" onClick={cancelar}>
              Cancelar
            </Button>
            <Button variant="primary" onClick={guardar}>
              Guardar
            </Button>
          </div>
        </div>
      </SeccionPerfil>
    );
  }

  if (vacia) {
    return (
      <SeccionPerfil titulo="Contacto" vacia accion={{ etiqueta: "+ Agregar", onClick: abrir }} />
    );
  }

  return (
    <SeccionPerfil titulo="Contacto" accion={{ etiqueta: "Editar", onClick: abrir }}>
      <DataList
        items={[
          { term: "Teléfono", description: contacto.telefono || undefined },
          { term: "Mail", description: contacto.mail || undefined },
        ]}
      />
    </SeccionPerfil>
  );
}
