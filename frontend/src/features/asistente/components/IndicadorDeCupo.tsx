import { CUPO_SIN_LIMITE } from "../types";
import type { CupoDelActor, TurnoDeLaConversacion } from "../types";

interface IndicadorDeCupoProps {
  /** El de `capacidades`. `undefined` mientras no se conoce todavía. */
  cupo: CupoDelActor | undefined;
  turnos: TurnoDeLaConversacion[];
}

/**
 * El cupo diario restante, en la franja de estado (asistente-cupo-visible).
 *
 * A DIFERENCIA de `LineaDeMetricas` —que es `aria-hidden`, un dato de más—, ESTE
 * SÍ ES TEXTO LEÍBLE: el estado bloqueado no puede depender de que alguien note
 * un detalle visual, y tasks.md 13.3 lo pide explícitamente. Sigue FUERA de la
 * región viva de los mensajes (`Conversacion.tsx`) y sin `aria-live` propio: es
 * un dato que cambia con cada turno, y una segunda región viva que lo anuncie
 * competiría con la que ya anuncia la respuesta misma.
 *
 * SE ACTUALIZA DESDE EL PROPIO TURNO (tasks.md 12.4): si algún turno ya
 * respondido trajo `cupoRestante`, ese valor —el más reciente— gana sobre el
 * de `capacidades`, que quedó desactualizado en cuanto se cobró el primer
 * turno de la sesión.
 */
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
 * (docs/product/designs/asistente-conversacional-design-spec.md
 * §Administración de uso, «Indicador de cupo restante»). El de mantenimiento
 * no está acá: ese motivo lo dice el banner (`PanelAsistente`), y el mismo
 * documento remite a él en vez de repetirlo en este lugar.
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
    default:
      return null;
  }
}
