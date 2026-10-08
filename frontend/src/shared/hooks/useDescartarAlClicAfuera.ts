import { useEffect, type RefObject } from "react";

/**
 * Cierra un popover cuando se hace clic afuera o se presiona Escape.
 *
 * Estaba copiado en cuatro popovers repartidos en TRES capas —`shared/ui`,
 * `app/shell` y dos features—, con cuatro juegos de nombres para exactamente el
 * mismo efecto. La duplicación no era el problema en sí: el problema es que un
 * arreglo —cambiar `mousedown` por `pointerdown`, agregar el foco, contemplar el
 * táctil— había que hacerlo cuatro veces, y la cuarta se olvida.
 *
 * `mousedown` y no `click`: con `click`, un botón que abre otro popover primero
 * dispara el suyo y después el cierre, y el segundo popover nace cerrado.
 *
 * No hace nada mientras `abierto` es falso: sin esa guarda, cada popover cerrado
 * de la pantalla escucharía todos los clics del documento.
 *
 * @param abierto Si el popover está visible.
 * @param contenedor El elemento que NO cuenta como «afuera».
 * @param descartar Qué hacer para cerrarlo.
 */
export function useDescartarAlClicAfuera(
  abierto: boolean,
  contenedor: RefObject<HTMLElement | null>,
  descartar: () => void,
): void {
  useEffect(() => {
    if (!abierto) return;

    function alApuntar(evento: MouseEvent) {
      if (contenedor.current && !contenedor.current.contains(evento.target as Node)) {
        descartar();
      }
    }

    function alTeclear(evento: KeyboardEvent) {
      if (evento.key === "Escape") descartar();
    }

    document.addEventListener("mousedown", alApuntar);
    document.addEventListener("keydown", alTeclear);

    return () => {
      document.removeEventListener("mousedown", alApuntar);
      document.removeEventListener("keydown", alTeclear);
    };
  }, [abierto, contenedor, descartar]);
}
