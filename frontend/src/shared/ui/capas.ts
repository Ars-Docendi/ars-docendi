/**
 * Pila de superposiciones (modales). Esc y la trampa de foco (Tab) actúan únicamente sobre la
 * capa superior: una capa debajo nunca reacciona mientras otra está encima.
 */
export interface Capa {
  /** Elemento dentro del cual queda atrapado el foco. */
  contenedor: () => HTMLElement | null;
  /** Pide cerrar la capa (el modal decide si lo permite: pendiente, cambios sin guardar). */
  cerrar: () => void;
}

const pila: Capa[] = [];

const SELECTOR_ENFOCABLES = "a[href], button, input, select, textarea, [tabindex]";

function enfocables(raiz: HTMLElement): HTMLElement[] {
  return Array.from(raiz.querySelectorAll<HTMLElement>(SELECTOR_ENFOCABLES)).filter(
    (el) =>
      !(el as HTMLButtonElement).disabled &&
      el.tabIndex >= 0 &&
      // Oculto (display: none, content-visibility, visibility: hidden) no recibe foco. jsdom no
      // implementa checkVisibility: ahí se asume visible.
      (typeof el.checkVisibility !== "function" ||
        el.checkVisibility({ visibilityProperty: true })) &&
      (el as HTMLInputElement).type !== "hidden",
  );
}

function alPresionarTecla(evento: KeyboardEvent) {
  const superior = pila[pila.length - 1];
  if (!superior) return;

  if (evento.key === "Escape") {
    evento.preventDefault();
    superior.cerrar();
    return;
  }
  if (evento.key !== "Tab") return;

  const contenedor = superior.contenedor();
  if (!contenedor) return;
  const lista = enfocables(contenedor);
  const activo = document.activeElement;
  if (lista.length === 0) {
    evento.preventDefault();
    contenedor.focus();
    return;
  }
  const primero = lista[0];
  const ultimo = lista[lista.length - 1];
  if (!contenedor.contains(activo)) {
    evento.preventDefault();
    primero.focus();
  } else if (evento.shiftKey && (activo === primero || activo === contenedor)) {
    evento.preventDefault();
    ultimo.focus();
  } else if (!evento.shiftKey && activo === ultimo) {
    evento.preventDefault();
    primero.focus();
  }
}

const oyentes = new Set<() => void>();
function emitir() {
  oyentes.forEach((oyente) => oyente());
}

/** Suscripción al cambio de la pila (para `useSyncExternalStore`). */
export function suscribirCapas(oyente: () => void): () => void {
  oyentes.add(oyente);
  return () => oyentes.delete(oyente);
}

export function cantidadDeCapas(): number {
  return pila.length;
}

/**
 * Pide cerrar la capa superior, como Esc. Lo usa el botón Atrás del navegador: con un diálogo
 * abierto cierra el diálogo y deja la página donde está.
 */
export function cerrarCapaSuperior(): boolean {
  const superior = pila[pila.length - 1];
  if (!superior) return false;
  superior.cerrar();
  return true;
}

/** `true` si el contenedor es el de la capa de más arriba (un clic afuera de él es de otra capa). */
export function esCapaSuperior(contenedor: HTMLElement | null): boolean {
  return pila.length > 0 && pila[pila.length - 1].contenedor() === contenedor;
}

/** Apila una capa; devuelve la función que la retira. */
export function registrarCapa(capa: Capa): () => void {
  if (pila.length === 0) document.addEventListener("keydown", alPresionarTecla);
  pila.push(capa);
  emitir();
  return () => {
    const indice = pila.indexOf(capa);
    if (indice >= 0) pila.splice(indice, 1);
    if (pila.length === 0) document.removeEventListener("keydown", alPresionarTecla);
    emitir();
  };
}
