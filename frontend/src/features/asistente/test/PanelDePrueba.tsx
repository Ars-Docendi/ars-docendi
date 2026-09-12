import type { ComponentProps } from "react";

import { AyudaDelAsistente } from "../components/AyudaDelAsistente";
import { NuevaConversacion } from "../components/NuevaConversacion";
import { PanelAsistente } from "../components/PanelAsistente";
import { useAsistente } from "../hooks/useAsistente";

type Props = Omit<ComponentProps<typeof PanelAsistente>, "asistente"> & {
  /**
   * Monta también «Nueva conversación», como hacen los dos montajes reales.
   *
   * El botón NO es del panel: en el modal va en el título del `Modal` y en la ruta
   * en el encabezado de página. Los dos lo arman con el mismo `asistente` que le
   * pasan al panel, y esta prop reproduce esa composición sin traer un modal ni un
   * encabezado de página al test.
   */
  conNuevaConversacion?: boolean;
};

/**
 * El panel con una conversación propia, como lo montan la ruta y el modal.
 *
 * La conversación vive en el dueño del montaje y el panel la recibe por prop, así
 * que para probar el panel hace falta alguien que haga de dueño. Es éste, y nada
 * más: sin encabezado de página ni modal alrededor.
 */
export function PanelDePrueba({ conNuevaConversacion = false, ...props }: Props) {
  const asistente = useAsistente();

  return (
    <>
      {/* La ayuda va SIEMPRE: en los dos montajes reales vive en el encabezado
          —el título del modal, el de la página— y no en el panel, así que un banco
          sin ella probaría una composición que no existe. */}
      <AyudaDelAsistente />
      {conNuevaConversacion && <NuevaConversacion asistente={asistente} />}
      <PanelAsistente asistente={asistente} {...props} />
    </>
  );
}
