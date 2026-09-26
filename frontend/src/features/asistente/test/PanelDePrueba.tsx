import type { ComponentProps } from "react";

import { PanelAsistente } from "../components/PanelAsistente";
import { useAsistente } from "../hooks/useAsistente";
import { useHistorialAsistente } from "../hooks/useHistorialAsistente";

type Props = Omit<ComponentProps<typeof PanelAsistente>, "asistente" | "historial">;

/**
 * El panel con una conversación propia, como lo montan los dos montajes
 * reales (el modal del lanzador, y la ruta mientras siga existiendo).
 *
 * La conversación vive en el dueño del montaje y el panel la recibe por prop, así
 * que para probar el panel hace falta alguien que haga de dueño. Es éste, y nada
 * más: sin encabezado de página ni modal alrededor.
 *
 * DESDE v3 EL PANEL YA NO NECESITA COMPOSICIÓN EXTRA: el rail —«Nueva
 * conversación», buscar, la lista— y el encabezado —el título, la ayuda—
 * viven ADENTRO de `PanelAsistente` (design.md D1 de
 * asistente-rediseno-v3), así que este banco ya no tiene que montar
 * `AyudaDelAsistente`, `AbrirHistorial` ni `NuevaConversacion` por su cuenta
 * para reproducir la composición real.
 */
export function PanelDePrueba(props: Props) {
  const asistente = useAsistente();
  // Habilitado siempre: este banco no simula un modal cerrado.
  const historial = useHistorialAsistente(asistente, true);

  return <PanelAsistente asistente={asistente} historial={historial} {...props} />;
}
