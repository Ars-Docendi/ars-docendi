// ============================================================
// Store mock de pedidos de designación.
// Singleton en memoria hidratado desde localStorage (clave
// "adoc.mock.pedidos") y persistido en cada escritura. Lectura/escritura
// SÍNCRONA: NO lo consumen los componentes directamente — solo lo usa
// `pedidosApi.ts` (el seam del backend). Las copias (structuredClone)
// evitan que el caller mute el estado guardado por referencia.
// ============================================================
import type { PedidoDesignacion } from "../types";
import { crearSeedPedidos } from "./pedidosSeed";

// v2: el seed sumó pedidos de revisión + fechas recientes (SCRUM-8). El sufijo
// fuerza un re-seed limpio en navegadores con datos viejos persistidos.
const CLAVE = "adoc.mock.pedidos.v2";

let pedidos: PedidoDesignacion[] | null = null;

function persistir(): void {
  if (pedidos !== null) {
    localStorage.setItem(CLAVE, JSON.stringify(pedidos));
  }
}

/** Hidrata el singleton desde localStorage; si está vacío, siembra el seed. */
function asegurarHidratado(): PedidoDesignacion[] {
  if (pedidos === null) {
    const crudo = localStorage.getItem(CLAVE);
    if (crudo) {
      pedidos = JSON.parse(crudo) as PedidoDesignacion[];
    } else {
      pedidos = crearSeedPedidos();
      persistir();
    }
  }
  return pedidos;
}

export function leerTodos(): PedidoDesignacion[] {
  return asegurarHidratado().map((p) => structuredClone(p));
}

export function buscar(id: string): PedidoDesignacion | undefined {
  const encontrado = asegurarHidratado().find((p) => p.id === id);
  return encontrado ? structuredClone(encontrado) : undefined;
}

/** Inserta o reemplaza un pedido (upsert por id) y persiste. */
export function guardar(pedido: PedidoDesignacion): PedidoDesignacion {
  const lista = asegurarHidratado();
  const indice = lista.findIndex((p) => p.id === pedido.id);
  if (indice >= 0) {
    lista[indice] = structuredClone(pedido);
  } else {
    lista.push(structuredClone(pedido));
  }
  persistir();
  return structuredClone(pedido);
}

/** Reemplaza todo el contenido del store (útil para tests). */
export function sembrarPedidos(iniciales: PedidoDesignacion[]): void {
  pedidos = iniciales.map((p) => structuredClone(p));
  persistir();
}

/** Resetea el singleton: la próxima lectura re-hidrata desde localStorage. */
export function reiniciarStorePedidos(): void {
  pedidos = null;
}
