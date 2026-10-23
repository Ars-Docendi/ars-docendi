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
    // Ídem: el rail sigue alternando en memoria, sólo que no lo recuerda
    // entre sesiones. `asistente-superficie-frontend` lo pide explícito
    // («el modal SHALL work normally when esa preferencia no se puede leer
    // o escribir»).
  }
}

/**
 * La preferencia de ancho del rail de conversaciones (design.md D2 de
 * asistente-rediseno-v3), guardada en `localStorage` bajo
 * `asistente.rail.<userId>`.
 *
 * ES UNA PREFERENCIA DE INTERFAZ Y NO UN DATO DE LA CONVERSACIÓN: por eso
 * vive en el navegador sin que esto contradiga «nada de la conversación se
 * guarda en el navegador» (asistente-superficie-frontend), que es una
 * afirmación sobre las preguntas y las respuestas, no sobre si un panel
 * está abierto o cerrado.
 *
 * POR USUARIO Y NO GLOBAL: dos personas que comparten navegador —un puesto
 * compartido, por ejemplo— no tienen por qué compartir esta preferencia.
 * `userId` lo resuelve quien llama (la sesión de desarrollo hoy; la
 * integración institucional cuando exista) y no este hook, que se queda
 * agnóstico de cómo se identifica a nadie.
 *
 * TRY/CATCH EN LAS DOS PUNTAS: un navegador con el storage bloqueado —modo
 * privado estricto, política de organización— no puede tirar abajo el
 * rail por eso. Falla en silencio y el rail sigue alternando en memoria.
 */
export function usePreferenciaDelRail(userId: string | undefined): PreferenciaDelRail {
  const [colapsado, setColapsado] = useState(() => leer(userId));

  // AJUSTE DE ESTADO EN RENDER, no un efecto — mismo patrón que
  // `useHistorialAsistente` ya usa para «Nueva conversación» y el resto de
  // esta feature. Si `userId` todavía no se conocía al montar —o cambia, por
  // ejemplo al cambiar de rol en el selector de desarrollo— la preferencia
  // se vuelve a leer para ESE usuario, en vez de arrastrar la del anterior;
  // un `useEffect` acá pintaría un frame de más con la preferencia del
  // usuario viejo antes de corregirla en el siguiente commit.
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
