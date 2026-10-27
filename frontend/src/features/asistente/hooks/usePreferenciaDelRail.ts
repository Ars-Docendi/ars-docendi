import { useCallback, useState } from "react";

const PREFIJO_CLAVE = "asistente.rail.";

export interface PreferenciaDelRail {
  /** Si el rail está colapsado a 60 px (`false` = expandido a 268 px, el default). */
  colapsado: boolean;
  /** Invierte el estado y lo persiste, si se puede. */
  alternar: () => void;
}

function leer(userId: string | undefined): boolean {
  if (!userId) return false;

  try {
    return localStorage.getItem(`${PREFIJO_CLAVE}${userId}`) === "colapsado";
  } catch {
    // Sin storage no hay preferencia que leer: el rail arranca en el
    // default (expandido), que es lo mismo que un usuario nuevo vería.
    return false;
  }
}

function escribir(userId: string | undefined, colapsado: boolean): void {
  if (!userId) return;

  try {
    localStorage.setItem(`${PREFIJO_CLAVE}${userId}`, colapsado ? "colapsado" : "expandido");
  } catch {
    // Ídem: el rail sigue alternando en memoria, sólo que no lo recuerda entre sesiones.
  }
}

/**
 * La preferencia de ancho del rail de conversaciones (design.md D2 de asistente-rediseno-v3),
 * guardada en `localStorage` bajo `asistente.rail.<userId>`.
 */
export function usePreferenciaDelRail(userId: string | undefined): PreferenciaDelRail {
  const [colapsado, setColapsado] = useState(() => leer(userId));

  // AJUSTE DE ESTADO EN RENDER, no un efecto — mismo patrón que `useHistorialAsistente` ya usa para
  // «Nueva conversación» y el resto de esta feature.
  const [userIdVisto, setUserIdVisto] = useState(userId);
  if (userId !== userIdVisto) {
    setUserIdVisto(userId);
    setColapsado(leer(userId));
  }

  const alternar = useCallback(() => {
    setColapsado((actual) => {
      const nuevo = !actual;
      escribir(userId, nuevo);
      return nuevo;
    });
  }, [userId]);

  return { colapsado, alternar };
}
