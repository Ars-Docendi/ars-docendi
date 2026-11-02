import { useSyncExternalStore } from "react";

export type TipoToast = "danger" | "success";

export interface ToastItem {
  id: number;
  tipo: TipoToast;
  titulo: string;
  descripcion?: string;
}

const DURACION_MS = 7000;

let items: ToastItem[] = [];
let secuencia = 0;
const oyentes = new Set<() => void>();

function emitir() {
  items = items.slice();
  oyentes.forEach((oyente) => oyente());
}

/** Shows a toast and dismisses it by itself after 7 seconds. Usable from any module, even a hook callback. */
export function mostrarToast(toast: Omit<ToastItem, "id" | "tipo"> & { tipo?: TipoToast }): number {
  const id = ++secuencia;
  items = [
    ...items,
    { id, tipo: toast.tipo ?? "danger", titulo: toast.titulo, descripcion: toast.descripcion },
  ];
  emitir();
  setTimeout(() => cerrarToast(id), DURACION_MS);
  return id;
}

export function cerrarToast(id: number) {
  if (!items.some((t) => t.id === id)) return;
  items = items.filter((t) => t.id !== id);
  emitir();
}

/** Test helper: forgets every toast. */
export function limpiarToasts() {
  items = [];
  emitir();
}

export function useToasts(): ToastItem[] {
  return useSyncExternalStore(
    (oyente) => {
      oyentes.add(oyente);
      return () => oyentes.delete(oyente);
    },
    () => items,
  );
}
