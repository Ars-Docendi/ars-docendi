import { afterEach, describe, it, expect, vi } from "vitest";

import { esperarHasta } from "./esperaPareja";

afterEach(() => vi.useRealTimers());

describe("esperarHasta", () => {
  it("se suelta al abortar, sin esperar a que venza el temporizador", async () => {
    vi.useFakeTimers();
    const aborto = new AbortController();
    let terminada = false;
    const espera = esperarHasta(10_000, aborto.signal).then(() => {
      terminada = true;
    });

    await Promise.resolve();
    expect(terminada).toBe(false);
    aborto.abort();
    await espera;
    expect(terminada).toBe(true);
  });
});
