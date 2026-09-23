import { describe, it, expect } from "vitest";
import {
  aplicarAccionTarea,
  ErrorDominioTarea,
  puedeAsignarComoResponsable,
  puedeCambiarEstado,
  puedeCrearTarea,
  puedeEditarAvance,
  puedeEditarCampos,
} from "./maquinaEstadosTarea";
import type { ActorTarea, DatosEditablesTarea, EstadoTarea, Tarea } from "../types";

const SECRETARIA: ActorTarea = { nombre: "L. Fernández", rol: "Secretaría Académica" };
const DECANATO: ActorTarea = { nombre: "R. Sosa", rol: "Decanato" };
const ADMINISTRACION: ActorTarea = { nombre: "P. Gómez", rol: "Administrativo" };
const RESPONSABLE: ActorTarea = { nombre: "G. Ruiz", rol: "Jefe de Cátedra" };
const OTRO: ActorTarea = { nombre: "M. Díaz", rol: "Coordinador de Carrera" };
const DOCENTE: ActorTarea = { nombre: "C. López", rol: "Docente" };

function tarea(overrides: Partial<Tarea> = {}): Tarea {
  return {
    id: "t1",
    numero: 1,
    titulo: "Revisar aulas del turno noche",
    descripcion: "Chequear disponibilidad para el próximo cuatrimestre.",
    fechaInicio: "2026-01-01",
    fechaFin: "2026-01-10",
    prioridad: "media",
    tipo: "administrativa",
    estado: "pendiente",
    porcentajeAvance: 0,
    responsable: RESPONSABLE,
    creadoPor: SECRETARIA,
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
    ...overrides,
  };
}

describe("puedeCrearTarea", () => {
  it("permite a Secretaría Académica, Decanato y Administrativo", () => {
    expect(puedeCrearTarea({ nombre: "x", rol: "Secretaría Académica" })).toBe(true);
    expect(puedeCrearTarea({ nombre: "x", rol: "Decanato" })).toBe(true);
    expect(puedeCrearTarea({ nombre: "x", rol: "Administrativo" })).toBe(true);
  });

  it("rechaza Jefe de Cátedra, Coordinador de Carrera y Docente", () => {
    expect(puedeCrearTarea({ nombre: "x", rol: "Jefe de Cátedra" })).toBe(false);
    expect(puedeCrearTarea({ nombre: "x", rol: "Coordinador de Carrera" })).toBe(false);
    expect(puedeCrearTarea({ nombre: "x", rol: "Docente" })).toBe(false);
  });
});

describe("cambiar estado", () => {
  it("el Responsable mueve libremente entre pendiente/en_curso/pausa/resuelta", () => {
    const enCurso = aplicarAccionTarea(
      tarea(),
      { tipo: "cambiarEstado", estadoDestino: "en_curso" },
      RESPONSABLE,
    );
    expect(enCurso.estado).toBe("en_curso");
  });

  it("pasar a pausa sin comentario es rechazado", () => {
    expect(() =>
      aplicarAccionTarea(
        tarea({ estado: "en_curso" }),
        { tipo: "cambiarEstado", estadoDestino: "pausa" },
        RESPONSABLE,
      ),
    ).toThrow(ErrorDominioTarea);
  });

  it("pasar a pausa con comentario lo agrega al hilo de comentarios", () => {
    const resultado = aplicarAccionTarea(
      tarea({ estado: "en_curso" }),
      { tipo: "cambiarEstado", estadoDestino: "pausa", comentario: "Tengo una consulta" },
      RESPONSABLE,
    );
    expect(resultado.estado).toBe("pausa");
    expect(resultado.comentarios).toHaveLength(1);
    expect(resultado.comentarios[0].texto).toBe("Tengo una consulta");
  });

  it("pasar a resuelta sin Solución es rechazado", () => {
    expect(() =>
      aplicarAccionTarea(
        tarea({ estado: "en_curso" }),
        { tipo: "cambiarEstado", estadoDestino: "resuelta" },
        RESPONSABLE,
      ),
    ).toThrow(ErrorDominioTarea);
  });

  it("pasar a resuelta con Solución completa la transición", () => {
    const resultado = aplicarAccionTarea(
      tarea({ estado: "en_curso" }),
      { tipo: "cambiarEstado", estadoDestino: "resuelta", solucion: "Se reasignó el aula" },
      RESPONSABLE,
    );
    expect(resultado.estado).toBe("resuelta");
    expect(resultado.solucion).toBe("Se reasignó el aula");
  });

  it("el Responsable no puede cancelar", () => {
    expect(() =>
      aplicarAccionTarea(
        tarea(),
        { tipo: "cambiarEstado", estadoDestino: "cancelada" },
        RESPONSABLE,
      ),
    ).toThrow(ErrorDominioTarea);
  });

  it("la autoridad creadora sí puede cancelar", () => {
    const resultado = aplicarAccionTarea(
      tarea(),
      { tipo: "cambiarEstado", estadoDestino: "cancelada" },
      SECRETARIA,
    );
    expect(resultado.estado).toBe("cancelada");
  });

  it("un tercero ajeno no puede cambiar el estado", () => {
    expect(() =>
      aplicarAccionTarea(tarea(), { tipo: "cambiarEstado", estadoDestino: "en_curso" }, OTRO),
    ).toThrow(ErrorDominioTarea);
  });

  it("un estado terminal solo lo reabre la autoridad creadora", () => {
    const resuelta = tarea({ estado: "resuelta", solucion: "Listo" });
    expect(() =>
      aplicarAccionTarea(
        resuelta,
        { tipo: "cambiarEstado", estadoDestino: "en_curso" },
        RESPONSABLE,
      ),
    ).toThrow(ErrorDominioTarea);
    const reabierta = aplicarAccionTarea(
      resuelta,
      { tipo: "cambiarEstado", estadoDestino: "en_curso" },
      SECRETARIA,
    );
    expect(reabierta.estado).toBe("en_curso");
  });
});

describe("editar avance", () => {
  it("el Responsable actualiza el porcentaje de avance", () => {
    const resultado = aplicarAccionTarea(
      tarea(),
      { tipo: "editarAvance", porcentajeAvance: 60 },
      RESPONSABLE,
    );
    expect(resultado.porcentajeAvance).toBe(60);
  });

  it("un tercero no puede editar el avance", () => {
    expect(() =>
      aplicarAccionTarea(tarea(), { tipo: "editarAvance", porcentajeAvance: 60 }, OTRO),
    ).toThrow(ErrorDominioTarea);
  });

  it("rechaza valores fuera de 0-100", () => {
    expect(() =>
      aplicarAccionTarea(tarea(), { tipo: "editarAvance", porcentajeAvance: 120 }, RESPONSABLE),
    ).toThrow(ErrorDominioTarea);
    expect(() =>
      aplicarAccionTarea(tarea(), { tipo: "editarAvance", porcentajeAvance: -1 }, RESPONSABLE),
    ).toThrow(ErrorDominioTarea);
  });
});

describe("editar campos", () => {
  const datos: DatosEditablesTarea = {
    titulo: "Nuevo título",
    descripcion: "Nueva descripción",
    fechaInicio: "2026-02-01",
    fechaFin: "2026-02-15",
    prioridad: "alta",
    tipo: "posgrado",
    responsable: RESPONSABLE,
  };

  it("la autoridad creadora edita los campos", () => {
    const resultado = aplicarAccionTarea(tarea(), { tipo: "editar", datos }, SECRETARIA);
    expect(resultado.titulo).toBe("Nuevo título");
    expect(resultado.prioridad).toBe("alta");
  });

  it("el Responsable no puede editar los campos", () => {
    expect(() => aplicarAccionTarea(tarea(), { tipo: "editar", datos }, RESPONSABLE)).toThrow(
      ErrorDominioTarea,
    );
  });

  it("no permite reasignar a un Responsable de mayor jerarquía", () => {
    const datosConDecanato: DatosEditablesTarea = { ...datos, responsable: DECANATO };
    expect(() =>
      aplicarAccionTarea(tarea(), { tipo: "editar", datos: datosConDecanato }, SECRETARIA),
    ).toThrow(ErrorDominioTarea);
  });

  it("una tarea hija conserva el Proyecto del padre aunque `datos.proyectoId` diga otra cosa", () => {
    const hija = tarea({ tareaPadreId: "padre-1", proyectoId: "p-1" });
    const datosConOtroProyecto: DatosEditablesTarea = { ...datos, proyectoId: "p-2" };
    const resultado = aplicarAccionTarea(
      hija,
      { tipo: "editar", datos: datosConOtroProyecto },
      SECRETARIA,
    );
    expect(resultado.proyectoId).toBe("p-1");
  });
});

describe("puedeAsignarComoResponsable", () => {
  it("Decanato puede asignar a cualquier nivel, incluido Docente", () => {
    expect(puedeAsignarComoResponsable(DECANATO, SECRETARIA)).toBe(true);
    expect(puedeAsignarComoResponsable(DECANATO, ADMINISTRACION)).toBe(true);
    expect(puedeAsignarComoResponsable(DECANATO, DOCENTE)).toBe(true);
  });

  it("Secretaría Académica no puede asignar a Decanato", () => {
    expect(puedeAsignarComoResponsable(SECRETARIA, DECANATO)).toBe(false);
  });

  it("Administrativo no puede asignar a Decanato ni Secretaría Académica", () => {
    expect(puedeAsignarComoResponsable(ADMINISTRACION, DECANATO)).toBe(false);
    expect(puedeAsignarComoResponsable(ADMINISTRACION, SECRETARIA)).toBe(false);
  });

  it("Administrativo sí puede asignar a Docente, varios niveles por debajo", () => {
    expect(puedeAsignarComoResponsable(ADMINISTRACION, DOCENTE)).toBe(true);
  });

  it("permite asignar entre pares del mismo nivel (Decanato, Secretaría Académica, Administrativo)", () => {
    expect(puedeAsignarComoResponsable(DECANATO, { nombre: "otro", rol: "Decanato" })).toBe(true);
    expect(
      puedeAsignarComoResponsable(SECRETARIA, { nombre: "otro", rol: "Secretaría Académica" }),
    ).toBe(true);
    expect(
      puedeAsignarComoResponsable(ADMINISTRACION, { nombre: "otro", rol: "Administrativo" }),
    ).toBe(true);
  });
});

describe("predicados puros usados por la UI", () => {
  it("puedeEditarCampos solo la autoridad creadora", () => {
    expect(puedeEditarCampos(tarea(), SECRETARIA)).toBe(true);
    expect(puedeEditarCampos(tarea(), RESPONSABLE)).toBe(false);
  });

  it("puedeEditarAvance el Responsable o la autoridad creadora", () => {
    expect(puedeEditarAvance(tarea(), RESPONSABLE)).toBe(true);
    expect(puedeEditarAvance(tarea(), SECRETARIA)).toBe(true);
    expect(puedeEditarAvance(tarea(), OTRO)).toBe(false);
  });

  it("puedeCambiarEstado a cancelada solo la autoridad", () => {
    const destino: EstadoTarea = "cancelada";
    expect(puedeCambiarEstado(tarea(), SECRETARIA, destino)).toBe(true);
    expect(puedeCambiarEstado(tarea(), RESPONSABLE, destino)).toBe(false);
  });
});
