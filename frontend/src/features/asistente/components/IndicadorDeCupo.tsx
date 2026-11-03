import { CUPO_SIN_LIMITE } from "../types";
import type { CupoDelActor, TurnoDeLaConversacion } from "../types";

interface IndicadorDeCupoProps {
  /** El de `capacidades`. `undefined` mientras no se conoce todavía. */
  cupo: CupoDelActor | undefined;
  turnos: TurnoDeLaConversacion[];
}

/** El cupo diario restante, en la franja de estado (asistente-cupo-visible). */
export function IndicadorDeCupo({ cupo, turnos }: IndicadorDeCupoProps) {
  if (!cupo) return null;

  const ultimoConCupo = [...turnos].reverse().find((t) => t.respuesta?.cupoRestante != null);
  const restante = ultimoConCupo?.respuesta?.cupoRestante ?? cupo.restante;

  if (cupo.bloqueado) {
    const texto = textoDeBloqueo(cupo);
    // El motivo "mantenimiento" ya lo dice el banner (`PanelAsistente`): repetirlo
    // acá sería la misma noticia dos veces con dos redacciones distintas.
    return texto ? (
      <p className="adoc-asistente-cupo adoc-asistente-cupo--bloqueado">{texto}</p>
    ) : null;
  }

  // Cupo desactivado (0 en el rol/override): no hay nada legible que decir de un
  // número que no representa ningún tope.
  if (restante >= CUPO_SIN_LIMITE) return null;

  return (
    <p className="adoc-asistente-cupo">
      Te queda{restante === 1 ? "" : "n"} {restante} {restante === 1 ? "consulta" : "consultas"}{" "}
      hoy.
    </p>
  );
}

/**
 * Los dos textos exactos del design spec
 * (docs/product/designs/asistente-conversacional-design-spec.md §Administración de uso, «Indicador
 * de cupo restante»).
 */
function textoDeBloqueo(cupo: CupoDelActor): string | null {
  switch (cupo.motivo) {
    case "presupuesto_propio":
      return "Alcanzaste tu límite de hoy.";
    // Nunca expone el costo ni el tope de la organización a quien no administra
    // (design.md D4 de asistente-administracion-de-uso): el texto es genérico
    // a propósito, igual que ya lo exige el backend para este motivo.
    case "tope_organizacional":
      return "El asistente alcanzó el límite de uso de la organización.";
    // asistente-acceso-granular: no dice si se lo quitaron a él o a su rol —
    // es administración interna, y lo que puede hacer es lo mismo.
    case "sin_acceso":
      return "No tenés acceso al asistente.";
    default:
      return null;
  }
}
