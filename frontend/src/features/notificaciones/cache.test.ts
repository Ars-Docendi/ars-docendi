import { describe, expect, it } from "vitest";

import { decrementar, quitarNoLeidas, type ListaCacheada } from "./cache";
import { notificacion, pagina } from "./pruebas";

function lista(...paginas: ReturnType<typeof pagina>[]): ListaCacheada {
  return { pages: paginas, pageParams: paginas.map((p) => p.pagina) };
}

describe("quitarNoLeidas", () => {
  it("removes matching unread items across pages and lowers the totals", () => {
    const a = notificacion();
    const b = notificacion();
    const c = notificacion();
    const original = lista(
      pagina([a, b], { pagina: 1, totalPaginas: 2, total: 3, tamanio: 2 }),
      pagina([c], { pagina: 2, totalPaginas: 2, total: 3, tamanio: 2 }),
    );

    const resultado = quitarNoLeidas(original, (n) => n.id === a.id || n.id === c.id)!;

    expect(resultado.pages[0].datos).toEqual([b]);
    expect(resultado.pages[1].datos).toEqual([]);
    expect(resultado.pages.map((p) => p.total)).toEqual([1, 1]);
    expect(resultado.pages[0].totalPaginas).toBe(1);
    expect(original.pages[0].datos).toEqual([a, b]); // never mutates the cache entry
  });

  it("leaves read items and untouched lists alone (same reference)", () => {
    const leida = notificacion({ leidaEn: "2026-11-04T10:00:00Z" });
    const original = lista(pagina([leida]));
    expect(quitarNoLeidas(original, () => true)).toBe(original);
    expect(quitarNoLeidas(undefined, () => true)).toBeUndefined();
    expect(quitarNoLeidas(lista(), () => true)).toEqual(lista());
  });
});

describe("decrementar", () => {
  it("never goes below zero and keeps an unknown count unknown", () => {
    expect(decrementar(5, 2)).toBe(3);
    expect(decrementar(1, 3)).toBe(0);
    expect(decrementar(undefined)).toBeUndefined();
  });
});
