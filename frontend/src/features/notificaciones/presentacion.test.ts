import { describe, expect, it } from "vitest";

import {
  agruparPorFecha,
  descripcionAccesible,
  etiquetaCampana,
  grupoDeFecha,
  rutaDeEntidad,
  textoInsignia,
} from "./presentacion";
import { notificacion } from "./pruebas";

// Wednesday 4 November 2026, noon. The week starts on Monday 2 November.
const AHORA = new Date(2026, 10, 4, 12, 0, 0);
const en = (dia: number, hora = 9) => new Date(2026, 10, dia, hora, 0, 0).toISOString();

describe("grupoDeFecha", () => {
  it("puts today, this week (Monday on) and earlier in their groups", () => {
    expect(grupoDeFecha(en(4, 8), AHORA)).toBe("Hoy");
    expect(grupoDeFecha(en(3), AHORA)).toBe("Esta semana");
    expect(grupoDeFecha(en(2, 0), AHORA)).toBe("Esta semana"); // Monday 00:00 is this week
    expect(grupoDeFecha(new Date(2026, 10, 1, 23, 59).toISOString(), AHORA)).toBe("Anteriores"); // Sunday
    expect(grupoDeFecha(en(1), AHORA)).toBe("Anteriores");
  });

  it("on a Monday there is no 'Esta semana' before today", () => {
    const lunes = new Date(2026, 10, 2, 12, 0, 0);
    expect(grupoDeFecha(new Date(2026, 10, 2, 9).toISOString(), lunes)).toBe("Hoy");
    expect(grupoDeFecha(new Date(2026, 10, 1, 9).toISOString(), lunes)).toBe("Anteriores");
  });

  it("on a Sunday the week still began the previous Monday", () => {
    const domingo = new Date(2026, 10, 8, 12, 0, 0);
    expect(grupoDeFecha(en(2), domingo)).toBe("Esta semana");
  });

  it("treats an invalid date as 'Anteriores'", () => {
    expect(grupoDeFecha("not-a-date", AHORA)).toBe("Anteriores");
  });
});

describe("agruparPorFecha", () => {
  it("keeps the order inside each group and drops the empty ones", () => {
    const a = notificacion({ creadaEn: en(4, 11) });
    const b = notificacion({ creadaEn: en(4, 10) });
    const c = notificacion({ creadaEn: en(1) });
    const grupos = agruparPorFecha([a, b, c], AHORA);
    expect(grupos.map((g) => g.etiqueta)).toEqual(["Hoy", "Anteriores"]);
    expect(grupos[0].notificaciones).toEqual([a, b]);
    expect(agruparPorFecha([], AHORA)).toEqual([]);
  });
});

describe("campana", () => {
  it.each([
    [0, "0"],
    [7, "7"],
    [99, "99"],
    [100, "99+"],
    [1500, "99+"],
  ])("badge text for %i is %s", (cantidad, texto) => {
    expect(textoInsignia(cantidad)).toBe(texto);
  });

  it("builds the exact accessible names", () => {
    expect(etiquetaCampana(0)).toBe("Notificaciones, ninguna sin leer");
    expect(etiquetaCampana(1)).toBe("Notificaciones, 1 notificación sin leer");
    expect(etiquetaCampana(2)).toBe("Notificaciones, 2 notificaciones sin leer");
    expect(etiquetaCampana(150)).toBe("Notificaciones, 150 notificaciones sin leer");
  });
});

describe("destinos y descripción", () => {
  it("maps each entity kind to its detail route", () => {
    expect(rutaDeEntidad("SOLICITUD_AULA", "x")).toBe("/aulas/solicitudes/x");
    expect(rutaDeEntidad("PEDIDO_DESIGNACION", "x")).toBe("/designaciones/pedidos/x");
    expect(rutaDeEntidad("TAREA", "x")).toBe("/tareas/x");
  });

  it("describes read state and entity for screen readers", () => {
    expect(descripcionAccesible(notificacion({ contexto: { numero: 7 } }))).toBe(
      "Notificación no leída. Tarea 7.",
    );
    expect(
      descripcionAccesible(
        notificacion({
          tipoEntidad: "PEDIDO_DESIGNACION",
          contexto: { numero: "2026-9002" },
          leidaEn: "2026-11-04T10:00:00Z",
        }),
      ),
    ).toBe("Notificación leída. Pedido 2026-9002.");
    expect(
      descripcionAccesible(notificacion({ tipoEntidad: "SOLICITUD_AULA", contexto: {} })),
    ).toBe("Notificación no leída. Solicitud de aula.");
  });
});
