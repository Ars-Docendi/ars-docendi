import type { ComponentProps } from "react";

import { PanelAsistente } from "../components/PanelAsistente";
import { useAsistente } from "../hooks/useAsistente";
import { useHistorialAsistente } from "../hooks/useHistorialAsistente";

type Props = Omit<ComponentProps<typeof PanelAsistente>, "asistente" | "historial"> & {
  /** Lo que en el lanzador viene de `GET /capacidades` (D9 de optimizaciones). */
  redaccionEnFlujo?: boolean;
};

/**
 * El panel con una conversación propia, como lo monta el lanzador —único montaje real desde
 * ARS-151 (tasks.md §10)—.
 */
export function PanelDePrueba({ redaccionEnFlujo, ...props }: Props) {
  const asistente = useAsistente({ redaccionEnFlujo });
  // Habilitado siempre: este banco no simula un modal cerrado.
  const historial = useHistorialAsistente(asistente, true);

  return <PanelAsistente asistente={asistente} historial={historial} {...props} />;
}
