import { useState } from "react";

export type PreferenciaTema = "sistema" | "claro" | "oscuro";

/** Preferencia de tema, por navegador. `index.html` lee la misma clave antes del primer pintado. */
const CLAVE = "ars-docendi:tema";
const CONSULTA_OSCURO = "(prefers-color-scheme: dark)";

export function leerPreferenciaTema(): PreferenciaTema {
  try {
    const valor = localStorage.getItem(CLAVE);
    return valor === "claro" || valor === "oscuro" ? valor : "sistema";
  } catch {
    return "sistema";
  }
}

/** Pone `data-theme` en `<html>`: `@ars-docendi/ui` y `tema-oscuro.css` cuelgan de ese atributo. */
export function aplicarTema(preferencia: PreferenciaTema) {
  const oscuro =
    preferencia === "oscuro" ||
    (preferencia === "sistema" && (window.matchMedia?.(CONSULTA_OSCURO).matches ?? false));
  document.documentElement.dataset.theme = oscuro ? "dark" : "light";
}

/** Se llama una vez al arrancar: con "sistema", el tema sigue al del dispositivo en vivo. */
export function iniciarTema() {
  aplicarTema(leerPreferenciaTema());
  window
    .matchMedia?.(CONSULTA_OSCURO)
    .addEventListener("change", () => aplicarTema(leerPreferenciaTema()));
}

export function usePreferenciaTema() {
  const [preferencia, setPreferencia] = useState(leerPreferenciaTema);
  function elegir(nueva: PreferenciaTema) {
    setPreferencia(nueva);
    try {
      localStorage.setItem(CLAVE, nueva);
    } catch {
      // Sin almacenamiento (modo privado, bloqueado): la preferencia dura la sesión.
    }
    aplicarTema(nueva);
  }
  return [preferencia, elegir] as const;
}
