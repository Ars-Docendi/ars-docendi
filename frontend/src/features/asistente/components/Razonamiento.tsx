interface RazonamientoProps {
  razonamiento?: string | null;
}

/**
 * Cómo el asistente entendió la pregunta, colapsado.
 *
 * El backend lo redacta para el usuario final —una o dos oraciones, sin nombres de
 * tablas ni de columnas— y lo omite cuando no tiene nada que decir; acá tampoco
 * queda un hueco. Va DENTRO del mensaje, en la región viva, porque es parte de la
 * respuesta: el contenido de un `<details>` cerrado no se anuncia hasta abrirlo,
 * así que no le agrega ruido al lector. Es la variante 1 de ARS-79.
 */
export function Razonamiento({ razonamiento }: RazonamientoProps) {
  if (!razonamiento) return null;

  return (
    <details className="adoc-asistente-razonamiento">
      <summary>Cómo lo interpreté</summary>
      <p>{razonamiento}</p>
    </details>
  );
}
