import { useState } from "react";

import { IconoMail, IconoPhone } from "../../../shared/ui/iconos";
import type { DatosContacto } from "../types";
import { FilaDato } from "./FilaDato";
import { ModalEditarCampo } from "./ModalEditarCampo";
import { SeccionPerfil } from "./SeccionPerfil";
import "./portal.css";

const FORMATO_MAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

type CampoContacto = "telefono" | "mail";

interface SeccionContactoProps {
  contacto: DatosContacto;
  onGuardar: (contacto: DatosContacto) => void;
}

function validarMail(valor: string): string | undefined {
  if (valor && !FORMATO_MAIL.test(valor)) return "Revisá el mail: no tiene un formato válido.";
  return undefined;
}

/**
 * Contacto del docente. A diferencia de las listas, sus campos son fijos y
 * conocidos, así que las filas se muestran siempre —aunque estén vacías— en vez
 * de colapsar la sección: es lo que le dice al docente qué puede cargar.
 *
 * Cada campo se edita por separado desde su propia fila.
 */
export function SeccionContacto({ contacto, onGuardar }: SeccionContactoProps) {
  const [enEdicion, setEnEdicion] = useState<CampoContacto | null>(null);

  function guardarCampo(campo: CampoContacto, valor: string) {
    onGuardar({ ...contacto, [campo]: valor });
    setEnEdicion(null);
  }

  return (
    <>
      <SeccionPerfil titulo="Contacto" filas>
        <div className="portal-filas">
          <FilaDato
            icono={<IconoPhone />}
            valor={contacto.telefono}
            etiqueta="Teléfono"
            vacio={!contacto.telefono}
            onEditar={() => setEnEdicion("telefono")}
          />
          <FilaDato
            icono={<IconoMail />}
            valor={contacto.mail}
            etiqueta="Mail"
            vacio={!contacto.mail}
            onEditar={() => setEnEdicion("mail")}
          />
        </div>
      </SeccionPerfil>

      {enEdicion === "telefono" && (
        <ModalEditarCampo
          etiqueta="Teléfono"
          tipo="tel"
          valor={contacto.telefono ?? ""}
          onCerrar={() => setEnEdicion(null)}
          onGuardar={(valor) => guardarCampo("telefono", valor)}
        />
      )}
      {enEdicion === "mail" && (
        <ModalEditarCampo
          etiqueta="Mail"
          tipo="email"
          valor={contacto.mail ?? ""}
          validar={validarMail}
          onCerrar={() => setEnEdicion(null)}
          onGuardar={(valor) => guardarCampo("mail", valor)}
        />
      )}
    </>
  );
}
