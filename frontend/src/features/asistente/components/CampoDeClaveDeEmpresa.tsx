import type { RefObject } from "react";
import { Button, Field, Input } from "@ars-docendi/ui";

import type { CredencialDeEmpresa } from "../types";

interface CampoDeClaveDeEmpresaProps {
  nombreDeLaEmpresa: string;
  credencial: CredencialDeEmpresa | null;
  /** `false` cuando el servidor no tiene clave maestra: no se pueden guardar claves. */
  cifradoDisponible: boolean;
  claveNueva: string;
  onClaveNueva: (clave: string) => void;
  /** Hay un panel de reemplazo abierto para esta empresa. */
  reemplazando: boolean;
  onReemplazar: () => void;
  botonReemplazarRef: RefObject<HTMLButtonElement | null>;
}

/** La clave de la empresa elegida (tareas 4.7 y 4.9). */
export function CampoDeClaveDeEmpresa({
  nombreDeLaEmpresa,
  credencial,
  cifradoDisponible,
  claveNueva,
  onClaveNueva,
  reemplazando,
  onReemplazar,
  botonReemplazarRef,
}: CampoDeClaveDeEmpresaProps) {
  if (credencial?.estado === "guardada") {
    return (
      <div className="adoc-field">
        <span className="adoc-label" id="modelo-clave-guardada-etiqueta">
          Clave de {nombreDeLaEmpresa}
        </span>
        <div
          className="adoc-asistente-modelo-clave-guardada"
          role="group"
          aria-labelledby="modelo-clave-guardada-etiqueta"
        >
          <span className="adoc-asistente-modelo-mono">••••{credencial.pista}</span>
          {!reemplazando && (
            <Button
              ref={botonReemplazarRef}
              variant="ghost"
              size="sm"
              disabled={!cifradoDisponible}
              onClick={onReemplazar}
            >
              Reemplazar
            </Button>
          )}
        </div>
        <div className="adoc-hint">
          {reemplazando
            ? "Clave actual. Sigue en uso hasta que confirmes el reemplazo."
            : "Guardada cifrada. No se vuelve a mostrar completa."}
        </div>
      </div>
    );
  }

  return (
    <Field
      label={`Clave de ${nombreDeLaEmpresa}`}
      required
      hint={
        cifradoDisponible
          ? `Este modelo necesita la clave de ${nombreDeLaEmpresa}. Se guarda cifrada al aplicar el cambio y sirve para todos sus modelos.`
          : "Guardar claves no está disponible: el servidor no tiene una clave maestra válida. Los modelos locales siguen disponibles."
      }
    >
      <Input
        type="password"
        autoComplete="off"
        placeholder="Pegá la clave de API"
        value={claveNueva}
        disabled={!cifradoDisponible}
        onChange={(e) => onClaveNueva(e.target.value)}
      />
    </Field>
  );
}
