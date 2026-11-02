import { describe, expect, it } from "vitest";

import { resolverVolver } from "./volver";

const REVISOR = ["designaciones.revisar", "designaciones.gestionar", "designaciones.ver"];
const JEFE = ["designaciones.gestionar", "designaciones.ver"];

describe("resolverVolver", () => {
  it("acepta una lista permitida con su query y rotula el destino", () => {
    const destino = resolverVolver(
      "pedido",
      "/designaciones/revision?pestania=finalizados",
      REVISOR,
    );
    expect(destino).toMatchObject({
      ruta: "/designaciones/revision?pestania=finalizados",
      texto: "Volver a Revisión",
      desdeEstado: true,
    });
  });

  it.each([
    ["una URL externa", "https://malo.example/designaciones/revision"],
    ["una ruta relativa al protocolo", "//malo.example/designaciones/revision"],
    ["una barra invertida", "/designaciones\\revision"],
    ["una ruta fuera de la lista", "/personas"],
    ["un hash", "/designaciones/revision#x"],
    ["un valor que no es texto", { ruta: "/designaciones/revision" }],
    ["un query con caracteres peligrosos", "/designaciones/revision?x=<script>"],
  ])("rechaza %s y usa el destino fijo", (_nombre, candidata) => {
    const destino = resolverVolver("pedido", candidata, REVISOR);
    expect(destino).toMatchObject({ ruta: "/designaciones/revision", desdeEstado: false });
  });

  it("no lleva a una pantalla que el usuario no puede ver", () => {
    // Un jefe sin permiso de revisión no puede volver a Revisión aunque el estado lo pida.
    const destino = resolverVolver("pedido", "/designaciones/revision", JEFE);
    expect(destino).toMatchObject({
      ruta: "/designaciones/mis-pedidos",
      texto: "Volver a Mis pedidos",
    });
  });

  it("acepta un proyecto como origen de una tarea, con su identificador", () => {
    const destino = resolverVolver("tarea", "/tareas/proyectos/abc-1", ["tareas.ver"]);
    expect(destino).toMatchObject({ ruta: "/tareas/proyectos/abc-1", texto: "Volver a Proyecto" });
  });

  it("sin estado usa el destino fijo de cada entidad", () => {
    expect(resolverVolver("solicitud", undefined, []).texto).toBe("Volver a Reserva de aulas");
    expect(resolverVolver("tarea", undefined, []).texto).toBe("Volver a Tareas");
    expect(resolverVolver("proyecto", undefined, []).texto).toBe("Volver a Proyectos");
  });
});
