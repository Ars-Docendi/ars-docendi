import { useEffect } from "react";

export const SUFIJO_TITULO = "Ars Docendi";
export const ID_CONTENIDO = "contenido-principal";

/** `document.title` = "<página> · Ars Docendi". */
export function tituloDocumento(pagina: string) {
  return pagina ? `${pagina} · ${SUFIJO_TITULO}` : SUFIJO_TITULO;
}

/** Fija el título del documento desde el `h1` de la página (lo llama `PageHeader`). */
export function useTituloDePagina(titulo: HTMLElement | null) {
  useEffect(() => {
    const texto = titulo?.textContent?.trim();
    if (texto) document.title = tituloDocumento(texto);
  });
}
