import { useState } from "react";

/**
 * Estado de alta / edición / borrado de una sección de lista del perfil.
 * Lo comparten Experiencia, Educación, Certificaciones y Proyectos.
 */
export function useSeccionLista<T extends { id: string }>() {
  const [modalAbierto, setModalAbierto] = useState(false);
  const [enEdicion, setEnEdicion] = useState<T | null>(null);
  const [aEliminar, setAEliminar] = useState<T | null>(null);

  return {
    modalAbierto,
    enEdicion,
    aEliminar,
    abrirAlta() {
      setEnEdicion(null);
      setModalAbierto(true);
    },
    abrirEdicion(item: T) {
      setEnEdicion(item);
      setModalAbierto(true);
    },
    cerrarModal() {
      setModalAbierto(false);
      setEnEdicion(null);
    },
    pedirBorrado(item: T) {
      setAEliminar(item);
    },
    cancelarBorrado() {
      setAEliminar(null);
    },
  };
}
