interface RazonamientoProps {
  razonamiento?: string | null;
  /** Sólo se muestra en modo debug (`VITE_ASISTENTE_DEBUG=true`), ver `utils/modoDebug`. */
  debug: boolean;
}

/**
 * Cómo el asistente entendió la pregunta, colapsado.
 *
 * El backend lo redacta para el usuario final —una o dos oraciones, sin nombres de
 * tablas ni de columnas— y lo omite cuando no tiene nada que decir; acá tampoco
 * queda un hueco. Va DENTRO del mensaje, en la región viva, porque es parte de la
 * respuesta: el contenido de un `<details>` cerrado no se anuncia hasta abrirlo,
 * así que no le agrega ruido al lector. Es la variante 1 de ARS-79.
 *
 * El backend sigue mandando `razonamiento` en todas las respuestas (queda visible
 * en devtools/network); lo que este componente decide es si lo RENDERIZA, y sólo
 * lo hace con el modo debug prendido (asistente-razonamiento-solo-en-debug).
 */
export function Razonamiento({ razonamiento, debug }: RazonamientoProps) {
  if (!debug || !razonamiento) return null;

  return (
    <details className="adoc-asistente-razonamiento">
      <summary>Cómo lo interpreté</summary>
      <p>{razonamiento}</p>
    </details>
  );
}
