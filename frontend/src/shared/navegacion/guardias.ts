import { useEffect, useRef } from "react";

/**
 * Cambios sin guardar. Las páginas se marcan sucias con `useGuardiaDeCambios`; un único
 * `GestorDeNavegacion` (react-router sólo admite un bloqueador activo) decide qué hacer.
 */
const guardias = new Set<{ activa: () => boolean; liberar: () => () => void }>();

export function hayCambiosPendientes() {
  for (const guardia of guardias) if (guardia.activa()) return true;
  return false;
}

/**
 * El usuario confirmó descartar los cambios de la página: ninguna guardia vuelve a preguntar.
 * Devuelve `rearmar`, que deja cada guardia como estaba si la salida finalmente no ocurre.
 */
export function liberarGuardias(): () => void {
  const restauraciones = [...guardias].map((guardia) => guardia.liberar());
  return () => restauraciones.forEach((restaurar) => restaurar());
}

function alDescargarPagina(evento: BeforeUnloadEvent) {
  if (!hayCambiosPendientes()) return;
  evento.preventDefault();
  // Los navegadores antiguos exigen asignar returnValue para mostrar su aviso.
  evento.returnValue = "";
}

/**
 * Marca la página como sucia mientras `sucio` sea verdadero. Devuelve `permitirSalida`: se llama
 * justo antes de navegar tras guardar con éxito (es síncrono, a diferencia de bajar `sucio`).
 */
export function useGuardiaDeCambios(sucio: boolean): { permitirSalida: () => void } {
  const sucioRef = useRef(sucio);
  const liberada = useRef(false);
  useEffect(() => {
    sucioRef.current = sucio;
    if (sucio) liberada.current = false;
  }, [sucio]);

  useEffect(() => {
    const guardia = {
      activa: () => sucioRef.current && !liberada.current,
      liberar: () => {
        const previa = liberada.current;
        liberada.current = true;
        return () => {
          liberada.current = previa;
        };
      },
    };
    if (guardias.size === 0) window.addEventListener("beforeunload", alDescargarPagina);
    guardias.add(guardia);
    return () => {
      guardias.delete(guardia);
      if (guardias.size === 0) window.removeEventListener("beforeunload", alDescargarPagina);
    };
  }, []);

  return {
    permitirSalida: () => {
      liberada.current = true;
    },
  };
}

/** Sólo para tests: limpia guardias que quedaron registradas. */
export function reiniciarGuardias() {
  guardias.clear();
  window.removeEventListener("beforeunload", alDescargarPagina);
}
