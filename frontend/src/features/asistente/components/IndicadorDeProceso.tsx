import { useVisibleTrasUmbral } from "../hooks/useVisibleTrasUmbral";

/** Cuánto se espera antes de mostrar nada, en milisegundos. */
export const UMBRAL_DE_APARICION_MS = 400;

interface IndicadorDeProcesoProps {
  activo: boolean;
  /** Inyectable para que el test no dependa del reloj real. */
  umbralMs?: number;
}

/** Dice que el asistente está trabajando, pero recién después de un umbral. */
export function IndicadorDeProceso({
  activo,
  umbralMs = UMBRAL_DE_APARICION_MS,
}: IndicadorDeProcesoProps) {
  const visible = useVisibleTrasUmbral(activo, umbralMs);

  return (
    <div role="status" aria-live="polite" className="adoc-asistente-proceso adoc-sr">
      {visible ? <span>Consultando…</span> : null}
    </div>
  );
}
