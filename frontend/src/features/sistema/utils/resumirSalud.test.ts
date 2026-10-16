import { describe, expect, it } from "vitest";

import { resumirSalud, type EntradaComponenteSalud } from "./resumirSalud";

const BASE: EntradaComponenteSalud[] = [
  { id: "aulas", nombre: "Aulas", disponible: true, comprobadoEn: "2026-09-26T22:00:00Z" },
  { id: "tareas", nombre: "Tareas", disponible: true, comprobadoEn: "2026-09-26T22:00:01Z" },
  {
    id: "designaciones",
    nombre: "Designaciones",
    disponible: true,
    comprobadoEn: "2026-09-26T22:00:02Z",
  },
  { id: "portal", nombre: "Portal", disponible: true, comprobadoEn: "2026-09-26T22:00:03Z" },
  { id: "asistente", nombre: "Asistente", disponible: true, comprobadoEn: "2026-09-26T22:00:04Z" },
  {
    id: "postgresql",
    nombre: "PostgreSQL",
    disponible: true,
    comprobadoEn: "2026-09-26T22:00:05Z",
  },
];

describe("resumirSalud", () => {
  it("todos disponibles: banner ok con el conteo N de N", () => {
    const resumen = resumirSalud({ componentes: BASE, mantenimientoAsistente: "inactivo" });

    expect(resumen.variante).toBe("ok");
    expect(resumen.titulo).toBe("Todos los componentes disponibles");
    expect(resumen.subtitulo).toBe("6 de 6 responden con normalidad.");
    expect(resumen.puntoEstado).toBe("positivo");
    expect(resumen.puntoAsistente).toBe("positivo");
    expect(resumen.ultimaComprobacion).toBe("2026-09-26T22:00:05.000Z");
  });

  it("un componente no disponible: singular, nombra y dice 'El resto...'", () => {
    const componentes = BASE.map((c) =>
      c.id === "designaciones" ? { ...c, disponible: false } : c,
    );

    const resumen = resumirSalud({ componentes, mantenimientoAsistente: "inactivo" });

    expect(resumen.variante).toBe("fallo");
    expect(resumen.titulo).toBe("1 componente no disponible");
    expect(resumen.subtitulo).toBe("Designaciones no respondió. El resto funciona con normalidad.");
    expect(resumen.puntoEstado).toBe("negativo");
  });

  it("dos componentes no disponibles: plural y los nombra a ambos", () => {
    const componentes = BASE.map((c) =>
      c.id === "aulas" || c.id === "tareas" ? { ...c, disponible: false } : c,
    );

    const resumen = resumirSalud({ componentes, mantenimientoAsistente: "inactivo" });

    expect(resumen.titulo).toBe("2 componentes no disponibles");
    expect(resumen.subtitulo).toBe(
      "Aulas y Tareas no respondieron. El resto funciona con normalidad.",
    );
    expect(resumen.nombresNoDisponibles).toEqual(["Aulas", "Tareas"]);
  });

  it("'El resto...' se omite cuando no queda ninguno disponible", () => {
    const componentes = BASE.map((c) => ({ ...c, disponible: false }));

    const resumen = resumirSalud({ componentes, mantenimientoAsistente: "inactivo" });

    expect(resumen.subtitulo).not.toContain("El resto");
    expect(resumen.totalDisponibles).toBe(0);
  });

  it("todos disponibles + mantenimiento activo: banner de mantenimiento", () => {
    const resumen = resumirSalud({ componentes: BASE, mantenimientoAsistente: "activo" });

    expect(resumen.variante).toBe("mantenimiento");
    expect(resumen.titulo).toBe("Todo disponible, asistente en mantenimiento");
    expect(resumen.puntoAsistente).toBe("advertencia");
    expect(resumen.puntoEstado).toBe("positivo");
  });

  it("la indisponibilidad pesa más que el mantenimiento", () => {
    const componentes = BASE.map((c) => (c.id === "portal" ? { ...c, disponible: false } : c));

    const resumen = resumirSalud({ componentes, mantenimientoAsistente: "activo" });

    expect(resumen.variante).toBe("fallo");
    expect(resumen.puntoAsistente).toBe("advertencia");
  });

  it("el ping del asistente fallando durante mantenimiento cuenta como no disponible, no como mantenimiento", () => {
    const componentes = BASE.map((c) => (c.id === "asistente" ? { ...c, disponible: false } : c));

    const resumen = resumirSalud({ componentes, mantenimientoAsistente: "activo" });

    expect(resumen.variante).toBe("fallo");
    expect(resumen.nombresNoDisponibles).toEqual(["Asistente"]);
  });

  it("mantenimiento desconocido: punto neutral, no aporta al banner de mantenimiento", () => {
    const resumen = resumirSalud({ componentes: BASE, mantenimientoAsistente: "desconocido" });

    expect(resumen.variante).toBe("ok");
    expect(resumen.puntoAsistente).toBe("neutral");
  });

  it("comprobaciones pendientes: variante pendiente y punto neutral", () => {
    const componentes = BASE.map((c) =>
      c.id === "aulas" ? { ...c, disponible: undefined, comprobadoEn: undefined } : c,
    );

    const resumen = resumirSalud({ componentes, mantenimientoAsistente: "inactivo" });

    expect(resumen.variante).toBe("pendiente");
    expect(resumen.puntoEstado).toBe("neutral");
  });
});
