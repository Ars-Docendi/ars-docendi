import { describe, it, expect } from "vitest";
import { aplicarAccion, ErrorDominioPedido } from "./maquinaEstados";
import type {
  ActorContexto,
  DatosEditablesPedido,
  EstadoPedido,
  PedidoDesignacion,
} from "../types";

const JC: ActorContexto = {
  rol: "Jefe de Cátedra",
  nombre: "G. Ruiz",
  carrera: "Ingeniería en Informática",
};

function pedidoBorrador(overrides: Partial<PedidoDesignacion> = {}): PedidoDesignacion {
  return {
    id: "p1",
    periodoId: "1",
    catedra: "Ingeniería de Software",
    carrera: "Ingeniería en Informática",
    docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 5 },
    materiaAsociada: "Ingeniería de Software",
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    haceHorasOtroDepto: false,
    adjuntos: [],
    estado: "borrador",
    prioritario: false,
    historial: [],
    ...overrides,
  };
}

const DATOS_EDITADOS: DatosEditablesPedido = {
  docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 6 },
  materiaAsociada: "Algoritmos y Estructuras de Datos",
  cargoActual: "Adjunto",
  dedicacionActual: "Categoría 3",
  novedad: "Sin novedad",
  haceHorasOtroDepto: true,
  adjuntos: [],
};

describe("aplicarAccion — máquina de estados (lado Jefe de Cátedra)", () => {
  describe("enviar [BR-designaciones-008]", () => {
    it("enviaBorradorVaARevisionCoordinador", () => {
      const resultado = aplicarAccion(pedidoBorrador(), { tipo: "enviar" }, JC);
      expect(resultado.estado).toBe("en_revision_coordinador");
    });

    it("no permite enviar un pedido que no está en borrador", () => {
      const pedido = pedidoBorrador({ estado: "en_revision_coordinador" });
      expect(() => aplicarAccion(pedido, { tipo: "enviar" }, JC)).toThrow(ErrorDominioPedido);
    });
  });

  describe("cancelar", () => {
    it("cancelarSoloEnBorrador — cancela un borrador", () => {
      const resultado = aplicarAccion(pedidoBorrador(), { tipo: "cancelar" }, JC);
      expect(resultado.estado).toBe("cancelado");
    });

    it("cancelarSoloEnBorrador — no cancela fuera de borrador", () => {
      const pedido = pedidoBorrador({ estado: "en_revision_coordinador" });
      expect(() => aplicarAccion(pedido, { tipo: "cancelar" }, JC)).toThrow(ErrorDominioPedido);
    });
  });

  describe("editar [BR-designaciones-008]", () => {
    it("editarSoloBorradorODevueltoDelPropietario — edita un borrador y conserva el estado", () => {
      const resultado = aplicarAccion(
        pedidoBorrador(),
        { tipo: "editar", datos: DATOS_EDITADOS },
        JC,
      );
      expect(resultado.estado).toBe("borrador");
      expect(resultado.materiaAsociada).toBe("Algoritmos y Estructuras de Datos");
      expect(resultado.haceHorasOtroDepto).toBe(true);
    });

    it("editarSoloBorradorODevueltoDelPropietario — edita un devuelto del propietario", () => {
      const pedido = pedidoBorrador({ estado: "devuelto", propietarioActual: "Jefe de Cátedra" });
      const resultado = aplicarAccion(pedido, { tipo: "editar", datos: DATOS_EDITADOS }, JC);
      expect(resultado.estado).toBe("devuelto");
    });

    it("no edita tras enviar a revisión", () => {
      const pedido = pedidoBorrador({ estado: "en_revision_coordinador" });
      expect(() => aplicarAccion(pedido, { tipo: "editar", datos: DATOS_EDITADOS }, JC)).toThrow(
        ErrorDominioPedido,
      );
    });
  });

  describe("idempotencia terminal", () => {
    const terminales: EstadoPedido[] = ["cancelado", "rechazado", "en_lote"];
    it.each(terminales)("accionSobrePedidoTerminalDenegada (%s)", (estado) => {
      const pedido = pedidoBorrador({ estado });
      expect(() => aplicarAccion(pedido, { tipo: "enviar" }, JC)).toThrow(ErrorDominioPedido);
      expect(() => aplicarAccion(pedido, { tipo: "cancelar" }, JC)).toThrow(ErrorDominioPedido);
      expect(() => aplicarAccion(pedido, { tipo: "editar", datos: DATOS_EDITADOS }, JC)).toThrow(
        ErrorDominioPedido,
      );
    });
  });

  describe("historial e inmutabilidad", () => {
    it("cadaTransicionRegistraHistorial", () => {
      const resultado = aplicarAccion(pedidoBorrador(), { tipo: "enviar" }, JC);
      expect(resultado.historial).toHaveLength(1);
      expect(resultado.historial[0]).toMatchObject({
        accion: "enviar",
        porRol: "Jefe de Cátedra",
        porNombre: "G. Ruiz",
        etapa: "en_revision_coordinador",
      });
      expect(resultado.historial[0].id).toBeTruthy();
      expect(resultado.historial[0].fecha).toBeTruthy();
    });

    it("no muta el pedido original (devuelve uno nuevo)", () => {
      const pedido = pedidoBorrador();
      aplicarAccion(pedido, { tipo: "enviar" }, JC);
      expect(pedido.estado).toBe("borrador");
      expect(pedido.historial).toHaveLength(0);
    });
  });
});
