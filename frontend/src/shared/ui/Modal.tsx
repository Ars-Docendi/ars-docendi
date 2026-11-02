import type { ReactNode } from "react";

import { ModalDeFormulario } from "./ModalDeFormulario";
import "./formulario.css";

interface ModalProps {
  open: boolean;
  onOpenChange?: (open: boolean) => void;
  title?: ReactNode;
  children?: ReactNode;
  footer?: ReactNode;
  className?: string;
  "aria-label"?: string;
  /** `confirmacion` 480 px, `formulario` 600 px, `libre` = ancho de la clase del consumidor. */
  tamano?: "confirmacion" | "formulario" | "libre";
  /** Hay datos sin guardar: cerrar por Esc, X o fondo pide confirmar dentro del mismo diálogo. */
  sucio?: boolean;
  /** Hay una mutación en curso: nada cierra el diálogo. */
  pendiente?: boolean;
  /** Si se pasa, el cuerpo y el pie van dentro de un `<form>` y el botón `submit` lo dispara. */
  alEnviar?: () => void;
}

/**
 * Modal de la app con la firma del `Modal` de `@ars-docendi/ui` (`open`, `onOpenChange`, `title`,
 * `footer`) pero con la carcasa común de los diálogos: trampa de foco, foco inicial y devuelto,
 * Esc sólo en el diálogo superior, confirmación de cambios sin guardar, sin cierre mientras hay
 * una mutación pendiente y cierre con el botón Atrás del navegador.
 */
export function Modal({
  open,
  onOpenChange,
  title,
  children,
  footer,
  className,
  "aria-label": etiqueta,
  tamano = "formulario",
  sucio,
  pendiente,
  alEnviar,
}: ModalProps) {
  if (!open) return null;
  return (
    <ModalDeFormulario
      titulo={title}
      etiqueta={etiqueta}
      className={className}
      tamano={tamano}
      sucio={sucio}
      pendiente={pendiente}
      alEnviar={alEnviar}
      pie={footer}
      onCerrar={() => onOpenChange?.(false)}
    >
      {children}
    </ModalDeFormulario>
  );
}
