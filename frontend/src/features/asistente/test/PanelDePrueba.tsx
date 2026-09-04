import type { ComponentProps } from "react";

import { PanelAsistente } from "../components/PanelAsistente";
import { useAsistente } from "../hooks/useAsistente";

type Props = Omit<ComponentProps<typeof PanelAsistente>, "asistente">;

/**
 * El panel con una conversación propia, como lo montan la ruta y el modal.
 *
 * La conversación vive en el dueño del montaje y el panel la recibe por prop, así
 * que para probar el panel hace falta alguien que haga de dueño. Es éste, y nada
 * más: sin encabezado de página ni modal alrededor.
 */
export function PanelDePrueba(props: Props) {
  const asistente = useAsistente();

  return <PanelAsistente asistente={asistente} {...props} />;
}
