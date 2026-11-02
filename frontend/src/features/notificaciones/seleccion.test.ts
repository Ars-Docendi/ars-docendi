import { describe, expect, it } from "vitest";

import { notificacion } from "./pruebas";
import {
  alternar,
  aSolicitud,
  cantidadSeleccionada,
  estaSeleccionada,
  iniciarSeleccion,
  SELECCION_INICIAL,
  todasSeleccionadas,
  type Seleccion,
} from "./seleccion";

describe("selección de notificaciones", () => {
  const a = notificacion({ creadaEn: "2026-11-04T10:00:00.000Z" });
  const b = notificacion({ creadaEn: "2026-11-04T12:00:00.000Z" });
  const c = notificacion({ creadaEn: "2026-11-04T11:00:00.000Z" });
  const ids = [a.id, b.id, c.id];

  it("only toggles while the mode is active", () => {
    expect(alternar(SELECCION_INICIAL, a.id, ids)).toBe(SELECCION_INICIAL);
    const una = alternar(iniciarSeleccion(), a.id, ids);
    expect(una.ids).toEqual([a.id]);
    expect(alternar(una, a.id, ids).ids).toEqual([]);
  });

  it("unticking one while 'all' is on selects every loaded one but that", () => {
    const todas: Seleccion = { activa: true, modo: "todas", ids: [] };
    expect(estaSeleccionada(todas, b.id)).toBe(true);
    const sinB = alternar(todas, b.id, ids);
    expect(sinB.modo).toBe("ids");
    expect(sinB.ids).toEqual([a.id, c.id]);
  });

  it("counts the chosen loaded ones, or everything unread when 'all' is on", () => {
    expect(cantidadSeleccionada({ activa: true, modo: "ids", ids: [a.id] }, ids, 50)).toBe(1);
    expect(cantidadSeleccionada({ activa: true, modo: "todas", ids: [] }, ids, 50)).toBe(50);
    expect(cantidadSeleccionada({ activa: true, modo: "todas", ids: [] }, ids, 2)).toBe(3);
  });

  it("is 'all selected' only when every unread one is covered, loaded or not", () => {
    const todasCargadas: Seleccion = { activa: true, modo: "ids", ids };
    expect(todasSeleccionadas(todasCargadas, ids, 3)).toBe(true);
    expect(todasSeleccionadas(todasCargadas, ids, 10)).toBe(false); // 7 unread not loaded
    expect(todasSeleccionadas({ activa: true, modo: "todas", ids: [] }, ids, 10)).toBe(true);
    expect(todasSeleccionadas(SELECCION_INICIAL, ids, 3)).toBe(false);
    expect(todasSeleccionadas({ activa: true, modo: "todas", ids: [] }, [], 0)).toBe(false);
  });

  it("builds the request: chosen ids, or everything up to the newest loaded instant", () => {
    expect(aSolicitud({ activa: true, modo: "ids", ids: [c.id, a.id] }, [a, b, c])).toEqual({
      ids: [a.id, c.id],
    });
    expect(aSolicitud({ activa: true, modo: "ids", ids: [] }, [a, b, c])).toBeNull();
    expect(aSolicitud({ activa: true, modo: "todas", ids: [] }, [a, b, c])).toEqual({
      todas: true,
      hasta: "2026-11-04T12:00:00.000Z",
    });
    expect(aSolicitud(SELECCION_INICIAL, [a])).toBeNull();
    expect(aSolicitud({ activa: true, modo: "todas", ids: [] }, [])).toBeNull();
  });
});
