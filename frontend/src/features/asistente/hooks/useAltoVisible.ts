import { useEffect } from "react";

/**
 * Publishes the visible viewport (what the on-screen keyboard leaves) as CSS variables while
 * `activo`, so the full-screen assistant on a phone can size itself to it and keep the input
 * above the keyboard. `100dvh` alone does not shrink for the keyboard on iOS Safari nor, by
 * default, on Chrome for Android.
 */
export function useAltoVisible(activo: boolean): void {
  useEffect(() => {
    const vista = window.visualViewport;
    if (!activo || !vista) return;
    const raiz = document.documentElement;

    function medir() {
      if (!vista) return;
      // Pinch zoom scales the visual viewport too; only the keyboard should shrink the panel.
      raiz.style.setProperty("--asistente-alto-visible", `${vista.height * vista.scale}px`);
      raiz.style.setProperty(
        "--asistente-desplazamiento",
        `${vista.scale === 1 ? vista.offsetTop : 0}px`,
      );
    }

    medir();
    vista.addEventListener("resize", medir);
    vista.addEventListener("scroll", medir);
    return () => {
      vista.removeEventListener("resize", medir);
      vista.removeEventListener("scroll", medir);
      raiz.style.removeProperty("--asistente-alto-visible");
      raiz.style.removeProperty("--asistente-desplazamiento");
    };
  }, [activo]);
}
