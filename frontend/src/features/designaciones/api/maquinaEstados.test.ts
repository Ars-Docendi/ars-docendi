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

// ============================================================
// SCRUM-8 — Circuito de revisión (Coordinador → Secretaría → Decanato,
// + Administración como revisor sin aprobación). Una falla primero por
// fila/guard de la tabla §6.5 del plan.
// ============================================================
const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "M. Díaz",
  carrera: "Ingeniería en Informática",
};
const SECRE: ActorContexto = { rol: "Secretaría", nombre: "L. Fernández" };
const DECANO: ActorContexto = { rol: "Decanato", nombre: "R. Sosa" };
const ADMIN: ActorContexto = { rol: "Administración", nombre: "P. Gómez" };

/** Pedido en una etapa de revisión, partiendo del fixture de borrador. */
function pedidoEnRevision(
  estado: EstadoPedido,
  overrides: Partial<PedidoDesignacion> = {},
): PedidoDesignacion {
  return pedidoBorrador({ estado, ...overrides });
}

describe("aplicarAccion — circuito de revisión (SCRUM-8)", () => {
  describe("aceptar — avance de la cadena", () => {
    it("aceptaCoordinadorAvanzaASecretaria", () => {
      const resultado = aplicarAccion(
        pedidoEnRevision("en_revision_coordinador"),
        { tipo: "aceptar" },
        COORD,
      );
      expect(resultado.estado).toBe("en_revision_secretaria");
      expect(resultado.historial.at(-1)).toMatchObject({
        accion: "aceptar",
        porRol: "Coordinador",
      });
    });

    it("aceptaSecretariaAvanzaADecanato", () => {
      const resultado = aplicarAccion(
        pedidoEnRevision("en_revision_secretaria"),
        { tipo: "aceptar" },
        SECRE,
      );
      expect(resultado.estado).toBe("en_revision_decanato");
    });

    it("aceptaDecanatoVaAEnLote", () => {
      const resultado = aplicarAccion(
        pedidoEnRevision("en_revision_decanato"),
        { tipo: "aceptar" },
        DECANO,
      );
      expect(resultado.estado).toBe("en_lote");
    });

    it("administracionNoPuedeAceptar [BR-015]", () => {
      expect(() =>
        aplicarAccion(pedidoEnRevision("en_revision_coordinador"), { tipo: "aceptar" }, ADMIN),
      ).toThrow(ErrorDominioPedido);
    });
  });

  describe("rechazar [BR-005, BR-011]", () => {
    it("rechazoSinJustificativoFalla", () => {
      expect(() =>
        aplicarAccion(
          pedidoEnRevision("en_revision_coordinador"),
          { tipo: "rechazar", comentario: "   " },
          COORD,
        ),
      ).toThrow(ErrorDominioPedido);
    });

    it("rechazoEsTerminal", () => {
      const rechazado = aplicarAccion(
        pedidoEnRevision("en_revision_coordinador"),
        { tipo: "rechazar", comentario: "No cumple los requisitos de antigüedad." },
        COORD,
      );
      expect(rechazado.estado).toBe("rechazado");
      // Idempotencia terminal: ninguna acción procede sobre un rechazado.
      expect(() => aplicarAccion(rechazado, { tipo: "aceptar" }, SECRE)).toThrow(
        ErrorDominioPedido,
      );
    });

    it("administracionPuedeRechazar", () => {
      const resultado = aplicarAccion(
        pedidoEnRevision("en_revision_secretaria"),
        { tipo: "rechazar", comentario: "Documentación inconsistente." },
        ADMIN,
      );
      expect(resultado.estado).toBe("rechazado");
    });
  });

  describe("devolver [BR-005, BR-014]", () => {
    it("devolucionSinComentarioFalla", () => {
      expect(() =>
        aplicarAccion(
          pedidoEnRevision("en_revision_coordinador"),
          { tipo: "devolver", comentario: "" },
          COORD,
        ),
      ).toThrow(ErrorDominioPedido);
    });

    it("devolucionRetrocedeUnNivel", () => {
      const resultado = aplicarAccion(
        pedidoEnRevision("en_revision_secretaria"),
        { tipo: "devolver", comentario: "Revisar el cargo solicitado." },
        SECRE,
      );
      expect(resultado.estado).toBe("devuelto");
      expect(resultado.propietarioActual).toBe("Coordinador");
      expect(resultado.etapaRetorno).toBe("en_revision_secretaria");
    });

    it("reenvioRetomaEtapaDelRevisor", () => {
      const devuelto = pedidoEnRevision("devuelto", {
        etapaRetorno: "en_revision_coordinador",
        propietarioActual: "Jefe de Cátedra",
      });
      const resultado = aplicarAccion(devuelto, { tipo: "reenviar" }, JC);
      expect(resultado.estado).toBe("en_revision_coordinador");
    });

    it("reenvioSoloDelPropietario", () => {
      const devuelto = pedidoEnRevision("devuelto", {
        etapaRetorno: "en_revision_coordinador",
        propietarioActual: "Jefe de Cátedra",
      });
      expect(() => aplicarAccion(devuelto, { tipo: "reenviar" }, COORD)).toThrow(
        ErrorDominioPedido,
      );
    });
  });

  describe("guards transversales", () => {
    it("rolEtapaIncorrectaDenegado [BR-013]", () => {
      // El Coordinador no puede actuar sobre un pedido que ya está en la etapa de Secretaría.
      expect(() =>
        aplicarAccion(pedidoEnRevision("en_revision_secretaria"), { tipo: "aceptar" }, COORD),
      ).toThrow(ErrorDominioPedido);
    });

    it("coordinadorFueraDeCarreraDenegado [BR-009]", () => {
      const otraCarrera: ActorContexto = {
        rol: "Coordinador",
        nombre: "F. Luna",
        carrera: "Ingeniería Industrial",
      };
      expect(() =>
        aplicarAccion(
          pedidoEnRevision("en_revision_coordinador"),
          { tipo: "aceptar" },
          otraCarrera,
        ),
      ).toThrow(ErrorDominioPedido);
    });
  });

  describe("priorizar [BR-017]", () => {
    it("prioritarioExigeJustificativo", () => {
      expect(() =>
        aplicarAccion(
          pedidoEnRevision("en_revision_coordinador"),
          { tipo: "priorizar", comentario: "" },
          COORD,
        ),
      ).toThrow(ErrorDominioPedido);
    });

    it("prioridadNoCambiaEstado", () => {
      const resultado = aplicarAccion(
        pedidoEnRevision("en_revision_coordinador"),
        { tipo: "priorizar", comentario: "Caso urgente por inicio de cuatrimestre." },
        COORD,
      );
      expect(resultado.prioritario).toBe(true);
      expect(resultado.estado).toBe("en_revision_coordinador");
    });
  });
});
