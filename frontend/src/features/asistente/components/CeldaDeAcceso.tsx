import { useState } from "react";
import { Button, Toggle } from "@ars-docendi/ui";

import { IconoRotateCcw } from "../../../shared/ui/iconos";

interface CeldaDeAccesoProps {
  /** El nombre para mostrar de la fila (usuario o rol), para los nombres accesibles. */
  nombre: string;
  acceso: boolean;
  /**
   * De dónde sale `acceso` en una fila de USUARIO: heredado del `"rol"` o
   * una revocación `"propio"`. Sin origen, la fila es un ROL y el
   * interruptor se mueve libremente.
   */
  origen?: "rol" | "propio";
  /**
   * `true` prende, `false` apaga. En una fila de usuario, prender sólo borra
   * su revocación: a un usuario se le puede quitar el acceso, no darlo.
   */
  onCambiar: (habilitado: boolean) => Promise<void>;
}

/**
 * La columna «Acceso» del panel de uso (asistente-acceso-granular, canvas
 * «Uso del asistente»): «Con acceso / Sin acceso» con su origen debajo, y el
 * ícono de restablecer cuando el valor es propio del usuario.
 *
 * El backend valida la misma regla (design.md D3); el interruptor
 * deshabilitado para un usuario sin acceso heredado del rol sólo la anticipa.
 */
export function CeldaDeAcceso({ nombre, acceso, origen, onCambiar }: CeldaDeAccesoProps) {
  const [enviando, setEnviando] = useState(false);
  const esUsuario = origen !== undefined;
  // «Quitar, no dar»: un usuario al que el rol no le da acceso no puede
  // prenderse desde acá — sólo se restablece una revocación propia.
  const bloqueadoPorRol = esUsuario && !acceso && origen === "rol";

  async function cambiar(habilitado: boolean) {
    setEnviando(true);
    try {
      await onCambiar(habilitado);
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="adoc-asistente-admin-acceso">
      <Toggle
        checked={acceso}
        disabled={enviando || bloqueadoPorRol}
        aria-label={`Acceso de ${nombre} al asistente`}
        onChange={(e) => void cambiar(e.target.checked)}
        label={
          <span className="adoc-asistente-admin-acceso-texto">
            <span>{acceso ? "Con acceso" : "Sin acceso"}</span>
            {esUsuario && (
              <span
                className={`adoc-asistente-admin-acceso-origen${
                  origen === "propio" ? " adoc-asistente-admin-acceso-origen--propio" : ""
                }`}
              >
                {origen === "propio" ? "propio" : "del rol"}
              </span>
            )}
          </span>
        }
      />
      {origen === "propio" && (
        <Button
          variant="ghost"
          size="sm"
          className="adoc-asistente-admin-cupo-editar"
          aria-label={`Restablecer el acceso de ${nombre} al del rol`}
          title="Volver al acceso del rol"
          disabled={enviando}
          onClick={() => void cambiar(true)}
        >
          <IconoRotateCcw />
        </Button>
      )}
    </div>
  );
}
