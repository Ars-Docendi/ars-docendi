import { describe, it, expect } from "vitest";

import {
  agregarCertificacion,
  agregarEducacion,
  agregarExperiencia,
  agregarProyecto,
  agregarTag,
  editarExperiencia,
  eliminarPorId,
  obtenerPerfilInstitucional,
  perfilVacio,
  quitarTag,
  vocabularioDisponible,
} from "./mockStore";
import type { DatosExperiencia } from "../types";

const EXPERIENCIA: DatosExperiencia = {
  puesto: "Data Architect",
  organizacion: "Globant",
  desde: "2015",
  hasta: "2020",
  descripcion: "Modelos de datos.",
};

describe("perfil institucional", () => {
  it("trae identidad y datos institucionales del padrón compartido", () => {
    const perfil = obtenerPerfilInstitucional("marina.diaz@unlam.edu.ar");
    expect(perfil).toMatchObject({
      nombre: "Marina",
      apellido: "Díaz",
      documento: "31089234",
      legajo: "0033",
      cuil: "27-31089234-8",
    });
  });

  it("no expone el teléfono del padrón: ese campo lo posee el docente", () => {
    const perfil = obtenerPerfilInstitucional("marina.diaz@unlam.edu.ar");
    expect(perfil).not.toHaveProperty("telefono");
  });

  it("devuelve null si el UPN no está en el padrón", () => {
    expect(obtenerPerfilInstitucional("nadie@unlam.edu.ar")).toBeNull();
  });
});

describe("listas del perfil", () => {
  it("agrega ítems asignando un id", () => {
    const lista = agregarExperiencia([], EXPERIENCIA);
    expect(lista).toHaveLength(1);
    expect(lista[0].id).toBeTruthy();
    expect(lista[0].puesto).toBe("Data Architect");
  });

  it("edita un ítem conservando su id y sin tocar a los demás", () => {
    const lista = agregarExperiencia(agregarExperiencia([], EXPERIENCIA), {
      ...EXPERIENCIA,
      puesto: "JTP",
    });
    const editada = editarExperiencia(lista, lista[0].id, { ...EXPERIENCIA, puesto: "Tech Lead" });
    expect(editada[0].id).toBe(lista[0].id);
    expect(editada[0].puesto).toBe("Tech Lead");
    expect(editada[1]).toEqual(lista[1]);
  });

  it("elimina por id", () => {
    const lista = agregarExperiencia([], EXPERIENCIA);
    expect(eliminarPorId(lista, lista[0].id)).toHaveLength(0);
  });

  it("guarda un período vigente como hasta = null", () => {
    const lista = agregarProyecto([], {
      nombre: "PID anomalías",
      rol: "Responsable",
      desde: "2022",
      hasta: null,
      descripcion: "Seguridad en redes industriales.",
      documento: null,
      doi: "",
    });
    expect(lista[0].hasta).toBeNull();
  });

  it("admite una certificación sin vencimiento", () => {
    const lista = agregarCertificacion([], {
      nombre: "Scrum Master",
      emisor: "Scrum.org",
      fecha: "2024-05-10",
      vencimiento: null,
    });
    expect(lista[0].vencimiento).toBeNull();
  });

  it("guarda el nivel de una formación", () => {
    const lista = agregarEducacion([], {
      nivel: "Doctorado",
      carrera: "Ciencias de la Computación",
      institucion: "UBA",
      desde: "2016",
      hasta: "2021",
    });
    expect(lista[0].nivel).toBe("Doctorado");
  });
});

describe("tags de habilidades e intereses", () => {
  it("marca como sugerido lo que no está en el vocabulario", () => {
    const lista = agregarTag([], "Computación cuántica");
    expect(lista[0]).toEqual({ termino: "Computación cuántica", sugerido: true });
  });

  it("no marca como sugerido lo que sí está en el vocabulario", () => {
    const lista = agregarTag([], "Bases de datos");
    expect(lista[0].sugerido).toBe(false);
  });

  it("no duplica un término ya elegido", () => {
    const lista = agregarTag(agregarTag([], "Redes de computadoras"), "redes de computadoras");
    expect(lista).toHaveLength(1);
  });

  it("ignora un término vacío", () => {
    expect(agregarTag([], "   ")).toHaveLength(0);
  });

  it("quita un término", () => {
    const lista = agregarTag([], "Bases de datos");
    expect(quitarTag(lista, "Bases de datos")).toHaveLength(0);
  });

  it("no ofrece en el vocabulario los términos ya elegidos", () => {
    const lista = agregarTag([], "Bases de datos");
    expect(vocabularioDisponible(lista)).not.toContain("Bases de datos");
  });

  it("habilidades e intereses son independientes: quitar de una no toca la otra", () => {
    const perfil = perfilVacio(obtenerPerfilInstitucional("marina.diaz@unlam.edu.ar")!);
    const conAmbas = {
      ...perfil,
      habilidades: agregarTag([], "Machine learning"),
      intereses: agregarTag([], "Machine learning"),
    };
    const sinHabilidad = {
      ...conAmbas,
      habilidades: quitarTag(conAmbas.habilidades, "Machine learning"),
    };
    expect(sinHabilidad.habilidades).toHaveLength(0);
    expect(sinHabilidad.intereses).toHaveLength(1);
  });
});
