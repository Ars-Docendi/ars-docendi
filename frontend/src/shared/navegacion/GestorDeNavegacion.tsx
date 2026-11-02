import { useEffect, useState } from "react";
import { useBlocker } from "react-router-dom";
import { Button } from "@ars-docendi/ui";

import { ModalDeFormulario } from "../ui/ModalDeFormulario";
import { cantidadDeCapas, cerrarCapaSuperior } from "../ui/capas";
import { hayCambiosPendientes } from "./guardias";

/**
 * Único bloqueador del router:
 * 1. un Atrás con un diálogo abierto cierra el diálogo y no sale de la página;
 * 2. salir de la página con cambios pendientes pide confirmar;
 * 3. cambiar sólo el query de la misma página nunca bloquea.
 */
/** Diálogo compartido de salida (mismo texto que la confirmación de los modales). */
function DialogoSalida({
  alSeguir,
  alDescartar,
}: {
  alSeguir: () => void;
  alDescartar: () => void;
}) {
  return (
    <ModalDeFormulario
      titulo="¿Salir de esta página?"
      onCerrar={alSeguir}
      pie={
        <>
          <Button id="salida-seguir-editando" variant="secondary" onClick={alSeguir}>
            Seguir editando
          </Button>
          <Button id="salida-descartar" variant="destructive" onClick={alDescartar}>
            Descartar cambios
          </Button>
        </>
      }
    >
      <p role="alert">Tenés cambios sin guardar.</p>
    </ModalDeFormulario>
  );
}

/** Se monta una sola vez en el shell, dentro del router de datos. */
export function GestorDeNavegacion() {
  // Por qué se bloqueó la última navegación: decide si se cierra un diálogo o se pide confirmar.
  const [motivo, setMotivo] = useState<"capa" | "cambios" | null>(null);
  const bloqueador = useBlocker(({ currentLocation, nextLocation, historyAction }) => {
    if (historyAction === "POP" && cantidadDeCapas() > 0) {
      setMotivo("capa");
      return true;
    }
    if (currentLocation.pathname !== nextLocation.pathname && hayCambiosPendientes()) {
      setMotivo("cambios");
      return true;
    }
    return false;
  });

  const bloqueada = bloqueador.state === "blocked";
  useEffect(() => {
    if (!bloqueada || motivo !== "capa") return;
    // Atrás con un diálogo abierto: lo cierra y la navegación se descarta.
    cerrarCapaSuperior();
    bloqueador.reset();
  }, [bloqueada, motivo, bloqueador]);

  if (!bloqueada || motivo !== "cambios") return null;
  return (
    <DialogoSalida alSeguir={() => bloqueador.reset()} alDescartar={() => bloqueador.proceed()} />
  );
}
