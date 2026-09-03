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
  quitarTag,
  vocabularioDisponible,
} from "./mockStore";
import {
  anioDe,
  componerFecha,
  formatearFecha,
  mesDe,
  ordenarPorFecha,
  ordenarPorPeriodo,
  rangoPeriodo,
} from "../formato";
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
    const conAmbas = {
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

describe("orden y formato de fechas", () => {
  const items = [
    { id: "a", desde: "2004", hasta: "2010" },
    { id: "b", desde: "2014", hasta: null },
    { id: "c", desde: "2014", hasta: "2016" },
    { id: "d", desde: "2014-03", hasta: "2016-08" },
  ];

  it("pone primero lo que sigue en curso", () => {
    expect(ordenarPorPeriodo(items)[0].id).toBe("b");
  });

  it("ordena por fecha de fin, de más reciente a más antigua", () => {
    expect(ordenarPorPeriodo(items).map((i) => i.id)).toEqual(["b", "d", "c", "a"]);
  });

  it("desempata por mes cuando el año coincide", () => {
    const mismoAnio = [
      { id: "sin-mes", desde: "2020", hasta: "2022" },
      { id: "con-mes", desde: "2020", hasta: "2022-09" },
    ];
    expect(ordenarPorPeriodo(mismoAnio).map((i) => i.id)).toEqual(["con-mes", "sin-mes"]);
  });

  it("no muta la lista original", () => {
    const original = [...items];
    ordenarPorPeriodo(items);
    expect(items).toEqual(original);
  });

  it("ordena las certificaciones por fecha de emisión descendente", () => {
    const certs = [{ fecha: "2021-03-18" }, { fecha: "2024-05-10" }, { fecha: "2023-11-02" }];
    expect(ordenarPorFecha(certs).map((c) => c.fecha)).toEqual([
      "2024-05-10",
      "2023-11-02",
      "2021-03-18",
    ]);
  });

  it("muestra el mes solo cuando está cargado", () => {
    expect(formatearFecha("2014")).toBe("2014");
    expect(formatearFecha("2014-03")).toBe("mar 2014");
  });

  it("marca como actual el período sin fecha de fin", () => {
    expect(rangoPeriodo({ desde: "2022-05", hasta: null })).toBe("may 2022 – actual");
    expect(rangoPeriodo({ desde: "2015", hasta: "2020" })).toBe("2015 – 2020");
  });

  it("compone y descompone la fecha con mes opcional", () => {
    expect(componerFecha("2014", "03")).toBe("2014-03");
    expect(componerFecha("2014", "")).toBe("2014");
    expect(componerFecha("", "03")).toBe("");
    expect(anioDe("2014-03")).toBe("2014");
    expect(mesDe("2014")).toBe("");
  });
});
