import { useMemo } from "react";
import type { SeccionToc } from "../components/FormToc";
import {
  JUSTIFICACION_MIN,
  documentosFaltantes,
  exigeDocumentacion,
  puedeEnviar as calcularPuedeEnviar,
  type PedidoMock,
} from "../mock/mockPedido";

export interface ValidacionPedido {
  puedeEnviar: boolean;
  documentosFaltantes: number;
  /** Mensaje del footer + su tono. */
  mensajeFooter: string;
  tonoFooter: "ok" | "warn";
  /** Items del TOC con su estado (done / error) derivado del pedido. */
  itemsToc: SeccionToc[];
}

/**
 * Deriva, desde el estado del pedido, todo lo que la UI necesita para validar:
 * gate de envío, documentos faltantes, mensaje del footer y estado por sección
 * del TOC. Hook puro — única fuente de verdad de la validación de pantalla.
 */
export function useValidacionPedido(pedido: PedidoMock): ValidacionPedido {
  return useMemo(() => {
    const faltantes = documentosFaltantes(pedido);
    const habilitado = calcularPuedeEnviar(pedido);
    const justificacionOk = pedido.justificacion.trim().length >= JUSTIFICACION_MIN;
    const requiereDocs = exigeDocumentacion(pedido.tipo);
    const docsOk = faltantes.length === 0;

    const itemsToc: SeccionToc[] = [
      { id: "tipo", label: "Tipo de pedido", done: true },
      { id: "docente", label: "Datos del docente", done: Boolean(pedido.docente.documento) },
      { id: "designacion", label: "Designación", done: Boolean(pedido.designacion.materia) },
      { id: "justif", label: "Justificación", done: justificacionOk, error: !justificacionOk },
      {
        id: "docs",
        label: "Documentación",
        done: !requiereDocs || docsOk,
        error: requiereDocs && !docsOk,
      },
    ];

    let mensajeFooter: string;
    let tonoFooter: "ok" | "warn";
    if (requiereDocs && !docsOk) {
      mensajeFooter = `! Faltan ${faltantes.length} documento${
        faltantes.length === 1 ? "" : "s"
      } obligatorio${faltantes.length === 1 ? "" : "s"} para enviar.`;
      tonoFooter = "warn";
    } else if (!justificacionOk) {
      mensajeFooter = `! La justificación debe tener al menos ${JUSTIFICACION_MIN} caracteres.`;
      tonoFooter = "warn";
    } else {
      mensajeFooter = "✓ Listo para enviar · autoguardado activo";
      tonoFooter = "ok";
    }

    return {
      puedeEnviar: habilitado,
      documentosFaltantes: faltantes.length,
      mensajeFooter,
      tonoFooter,
      itemsToc,
    };
  }, [pedido]);
}
