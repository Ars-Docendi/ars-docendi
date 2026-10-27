interface RazonamientoProps {
  razonamiento?: string | null;
  /** Sólo se muestra en modo debug (`VITE_ASISTENTE_DEBUG=true`), ver `utils/modoDebug`. */
  debug: boolean;
}

/** Cómo el asistente entendió la pregunta, colapsado. Es la variante 1 de ARS-79. */
export function Razonamiento({ razonamiento, debug }: RazonamientoProps) {
  if (!debug || !razonamiento) return null;

  return (
    <details className="adoc-asistente-razonamiento">
      <summary>Cómo lo interpreté</summary>
      <p>{razonamiento}</p>
    </details>
  );
}
