import { useEffect, useRef } from "react";
import { Drawer as DrawerBase } from "@ars-docendi/ui";
import type { ComponentProps } from "react";

import { registrarCapa } from "./capas";

type DrawerProps = ComponentProps<typeof DrawerBase>;

const ENFOCABLES = "a[href], button:not(:disabled), input, select, textarea, [tabindex]";

/**
 * Panel lateral con la misma conducta que los diálogos: entra al panel con el foco, lo atrapa,
 * Esc y el botón Atrás del navegador cierran sólo la capa superior y el foco vuelve a quien lo
 * abrió. Dibuja con el `Drawer` de la librería, que ya no maneja Esc por su cuenta (con un
 * diálogo encima, cerraría ambos).
 *
 * `className` debe llevar una clase propia del consumidor: con ella se encuentra el panel.
 */
export function Drawer({ open, onOpenChange, className, ...resto }: DrawerProps) {
  const alCambiar = useRef(onOpenChange);
  useEffect(() => {
    alCambiar.current = onOpenChange;
  });
  const marca = className?.split(" ")[0];

  useEffect(() => {
    if (!open || !marca) return;
    const abridor = document.activeElement;
    const contenedor = () => document.querySelector<HTMLElement>(`.${marca}`);
    const quitar = registrarCapa({ contenedor, cerrar: () => alCambiar.current?.(false) });
    // El panel se pinta en el mismo ciclo: el foco entra al primer control, o al panel mismo.
    const entrar = window.setTimeout(() => {
      const panel = contenedor();
      (panel?.querySelector<HTMLElement>(ENFOCABLES) ?? panel)?.focus();
    }, 0);
    return () => {
      window.clearTimeout(entrar);
      quitar();
      window.setTimeout(() => {
        if (abridor instanceof HTMLElement && document.contains(abridor)) abridor.focus();
      }, 0);
    };
  }, [open, marca]);

  return (
    <DrawerBase
      {...resto}
      open={open}
      onOpenChange={onOpenChange}
      className={className}
      closeOnEscape={false}
    />
  );
}
