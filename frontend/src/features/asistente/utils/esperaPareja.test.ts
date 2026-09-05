import { describe, it, expect } from "vitest";

import {
  crearMedidorDeEspera,
  esperarHasta,
  ESPERA_MAXIMA_MS,
  ESPERA_MINIMA_MS,
  MEDIA_SEMILLA_MS,
} from "./esperaPareja";

/** Un azar fijo, para poder afirmar el número en vez de un rango. */
const mitad = () => 0.5;

describe("el medidor de la espera pareja", () => {
  it("sin muestras usa la semilla, no un cero", () => {
    // El primer turno de una conversación suele ser un saludo: si sin muestras el
    // objetivo fuera cero, el caso más visible sería justo el que no se empareja.
    const medidor = crearMedidorDeEspera(mitad);

    expect(medidor.objetivoMs()).toBe(Math.round(MEDIA_SEMILLA_MS * 0.55));
  });

  it("con la semilla, el sorteo cabe entero entre el piso y el techo", () => {
    // Si el piso se comiera la banda, las primeras esperas serían todas iguales y
    // se percibirían como un temporizador. Los dos extremos tienen que quedar
    // adentro sin recortarse.
    const flojo = crearMedidorDeEspera(() => 0).objetivoMs();
    const apretado = crearMedidorDeEspera(() => 1).objetivoMs();

    expect(flojo).toBeGreaterThan(ESPERA_MINIMA_MS);
    expect(apretado).toBeLessThan(ESPERA_MAXIMA_MS);
    expect(apretado - flojo).toBeGreaterThan(500);
  });

  it("aprende de los turnos que sí llamaron al modelo", () => {
    const medidor = crearMedidorDeEspera(mitad);

    medidor.anotar(3000);
    medidor.anotar(5000);

    // Media 4000, fracción 0,55 → 2200, dentro de la banda.
    expect(medidor.objetivoMs()).toBe(2200);
  });

  it("no deja que un proveedor lento vuelva eterna una respuesta instantánea", () => {
    const medidor = crearMedidorDeEspera(() => 1);

    medidor.anotar(30_000);

    expect(medidor.objetivoMs()).toBe(ESPERA_MAXIMA_MS);
  });

  it("no baja del piso que el indicador necesita para no parpadear", () => {
    // `IndicadorDeProceso` aparece a los 400 ms. Con un objetivo más corto que el
    // piso, aparecería y desaparecería antes de poder leerse.
    const medidor = crearMedidorDeEspera(() => 0);

    medidor.anotar(200);

    expect(medidor.objetivoMs()).toBe(ESPERA_MINIMA_MS);
  });

  it("descarta una duración imposible en vez de envenenar la media", () => {
    // El reloj saltó, o la pestaña estuvo dormida. Sin el descarte, una muestra
    // absurda arrastraría los cinco turnos siguientes.
    const medidor = crearMedidorDeEspera(mitad);

    medidor.anotar(Number.NaN);
    medidor.anotar(-1);
    medidor.anotar(0);

    expect(medidor.objetivoMs()).toBe(Math.round(MEDIA_SEMILLA_MS * 0.55));
  });

  it("sortea dentro de una banda: dos turnos seguidos no esperan lo mismo", () => {
    // Una espera clavada siempre en el mismo número se percibe como un
    // temporizador y no como trabajo.
    const flojo = crearMedidorDeEspera(() => 0);
    const apretado = crearMedidorDeEspera(() => 1);
    for (const m of [flojo, apretado]) m.anotar(4000);

    expect(flojo.objetivoMs()).toBe(1600);
    expect(apretado.objetivoMs()).toBe(ESPERA_MAXIMA_MS);
    expect(flojo.objetivoMs()).toBeLessThan(apretado.objetivoMs());
  });
});

describe("esperarHasta", () => {
  it("vuelve enseguida si no falta nada", async () => {
    const arranco = performance.now();
    await esperarHasta(0, new AbortController().signal);

    expect(performance.now() - arranco).toBeLessThan(50);
  });

  it("se suelta al abortar, sin esperar a que venza el temporizador", async () => {
    // Es lo que hace que «Dejar de esperar» libere el campo en el acto.
    const aborto = new AbortController();
    const arranco = performance.now();

    const espera = esperarHasta(10_000, aborto.signal);
    aborto.abort();
    await espera;

    expect(performance.now() - arranco).toBeLessThan(200);
  });
});
