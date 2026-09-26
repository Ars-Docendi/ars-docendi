import { Button } from "@ars-docendi/ui";

import { historyIcon } from "../../../app/shell/icons";
import type { HistorialAsistente } from "../hooks/useHistorialAsistente";

interface AbrirHistorialProps {
  historial: HistorialAsistente;
}

/**
 * Abre y cierra el panel de conversaciones propias.
 *
 * VA EN EL ENCABEZADO DE CADA MONTAJE, junto a «Nueva conversación» —en el
 * modal del lanzador y en la página de la ruta—, y no adentro del panel: los
 * dos montajes componen este mismo botón con el MISMO `historial` que le
 * pasan a `PanelAsistente`, así que sigue siendo un solo control alcanzable
 * desde los dos lugares y no dos copias que puedan desincronizarse (tasks.md
 * 10.3). Antes vivía dentro del panel, en su propia fila; se mudó para que
 * el cajón que abre pueda superponerse al hilo en vez de empujarlo.
 *
 * `aria-controls` apunta al panel por su id ESTABLE (`historial.idDelPanel`):
 * viven en dos subárboles distintos del DOM desde la mudanza, así que ya no
 * alcanza con la cercanía en el marcado para que un lector de pantalla los
 * relacione. La ref es la que Escape usa para devolverle el foco a este
 * botón al cerrar el panel (`useHistorialAsistente`).
 *
 * LA ETIQUETA VA EN UN `<span>` PROPIO por el mismo motivo que en
 * `NuevaConversacion`: a ancho de teléfono, en el modal, `asistente.css` la
 * esconde visualmente —el nombre accesible del botón no cambia, sigue siendo
 * «Historial»— para que la fila del encabezado no desborde.
 */
export function AbrirHistorial({ historial }: AbrirHistorialProps) {
  // Desestructurado ANTES del JSX, y no leído como `historial.x` ahí adentro:
  // pasar `historial.registrarDisparador` directo a `ref=` hace que el
  // linter (`react-hooks/refs`) trate cualquier otra lectura de una
  // propiedad de `historial` en el mismo render como sospechosa. Con
  // variables locales no queda ningún acceso a miembro que marcar.
  const { registrarDisparador, abierto, idDelPanel, alternar } = historial;

  return (
    <Button
      ref={registrarDisparador}
      variant="ghost"
      size="sm"
      leadingIcon={historyIcon}
      aria-expanded={abierto}
      aria-controls={idDelPanel}
      onClick={alternar}
    >
      <span className="adoc-asistente-etiqueta-boton">Historial</span>
    </Button>
  );
}
