import type { ChangeEvent } from "react";

/**
 * Aplica una máscara al escribir y deja el cursor después de la misma cantidad de dígitos, para
 * que escribir en el medio del texto no mande el cursor al final.
 */
export function aplicarMascara(
  evento: ChangeEvent<HTMLInputElement>,
  formatear: (texto: string, borrando: boolean) => string,
  alCambiar: (texto: string) => void,
) {
  const elemento = evento.target;
  const tipo = (evento.nativeEvent as InputEvent).inputType ?? "";
  const borrando = tipo.startsWith("delete");
  const posicion = elemento.selectionStart ?? elemento.value.length;
  const digitosAntes = elemento.value.slice(0, posicion).replace(/\D/g, "").length;
  const nuevo = formatear(elemento.value, borrando);
  alCambiar(nuevo);

  requestAnimationFrame(() => {
    if (document.activeElement !== elemento) return;
    let indice = 0;
    let digitos = 0;
    while (indice < nuevo.length && digitos < digitosAntes) {
      if (/\d/.test(nuevo.charAt(indice))) digitos++;
      indice++;
    }
    if (!borrando && /[/:]/.test(nuevo.charAt(indice))) indice++;
    try {
      elemento.setSelectionRange(indice, indice);
    } catch {
      // Si el navegador no lo soporta, el cursor queda al final.
    }
  });
}
