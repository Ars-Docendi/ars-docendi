import { vi } from "vitest";

/** Observadores creados por el mock, para disparar intersecciones a mano. */
export const observadores: MockIntersectionObserver[] = [];

export class MockIntersectionObserver {
  elementos = new Set<Element>();
  private readonly callback: IntersectionObserverCallback;
  constructor(callback: IntersectionObserverCallback) {
    this.callback = callback;
    observadores.push(this);
  }
  observe = (elemento: Element) => this.elementos.add(elemento);
  unobserve = (elemento: Element) => this.elementos.delete(elemento);
  disconnect = () => this.elementos.clear();
  takeRecords = () => [];
  /** Simula que `elemento` pasó a ser visible. */
  interseccion(elemento: Element) {
    this.callback(
      [{ isIntersecting: true, target: elemento } as IntersectionObserverEntry],
      this as unknown as IntersectionObserver,
    );
  }
}

/** jsdom no implementa `scrollIntoView` ni `IntersectionObserver`: se simulan para los tests de ayuda. */
export function prepararEntornoDom() {
  observadores.length = 0;
  const scrollIntoView = vi.fn();
  Element.prototype.scrollIntoView = scrollIntoView;
  vi.stubGlobal("IntersectionObserver", MockIntersectionObserver);
  return { scrollIntoView };
}
