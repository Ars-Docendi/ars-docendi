// ============================================================
// Perfiles seed del Portal, uno por UPN de la sesión mock.
//
// Hay tres perfiles distintos (investigación/datos, redes/infraestructura y
// desarrollo/docencia) repartidos entre los usuarios, para que cualquier login
// muestre la pantalla con contenido real. La excepción es admin.aulas: queda
// sin perfil a propósito, para poder recorrer el estado vacío.
// ============================================================

import type { PerfilDocente, PerfilInstitucional } from "../types";

type Seccion = Omit<PerfilDocente, "institucional">;

/** Investigación en datos y seguridad. */
const DATOS: Seccion = {
  contacto: { telefono: "11-5548-9900", mail: "marina.diaz@gmail.com" },
  cv: { nombre: "cv-diaz-2026.pdf", fechaCarga: "2026-08-12" },
  experiencia: [
    {
      id: "exp-1",
      puesto: "Investigadora principal",
      organizacion: "Departamento de Ingeniería · UNLaM",
      desde: "2021",
      hasta: null,
      descripcion:
        "Dirección de la línea de seguridad en redes industriales y formación de becarios de grado.",
    },
    {
      id: "exp-2",
      puesto: "Data Architect",
      organizacion: "Globant",
      desde: "2015",
      hasta: "2020",
      descripcion:
        "Diseño de modelos de datos y pipelines de ingesta para clientes de banca y retail.",
    },
    {
      id: "exp-3",
      puesto: "Jefa de Trabajos Prácticos",
      organizacion: "Universidad de Buenos Aires",
      desde: "2010",
      hasta: "2018",
      descripcion: "Cátedra de Bases de Datos, comisiones de tarde.",
    },
    {
      id: "exp-4",
      puesto: "Analista de sistemas",
      organizacion: "Banco Provincia",
      desde: "2008",
      hasta: "2015",
      descripcion: "Mantenimiento del core transaccional y migración a PostgreSQL.",
    },
  ],
  educacion: [
    {
      id: "edu-1",
      nivel: "Doctorado",
      carrera: "Ciencias de la Computación",
      institucion: "Universidad de Buenos Aires",
      desde: "2019",
      hasta: null,
    },
    {
      id: "edu-2",
      nivel: "Maestría",
      carrera: "Explotación de Datos y Descubrimiento del Conocimiento",
      institucion: "Universidad de Buenos Aires",
      desde: "2012",
      hasta: "2015",
    },
    {
      id: "edu-3",
      nivel: "Especialización",
      carrera: "Seguridad Informática",
      institucion: "Universidad Nacional de La Matanza",
      desde: "2010",
      hasta: "2011",
    },
    {
      id: "edu-4",
      nivel: "Grado",
      carrera: "Ingeniería en Informática",
      institucion: "Universidad Nacional de La Matanza",
      desde: "2002",
      hasta: "2008",
    },
  ],
  certificaciones: [
    {
      id: "cert-1",
      nombre: "AWS Certified Solutions Architect – Professional",
      emisor: "Amazon Web Services",
      fecha: "2024-05-10",
      vencimiento: "2027-05-10",
    },
    {
      id: "cert-2",
      nombre: "Certified Information Systems Security Professional (CISSP)",
      emisor: "ISC²",
      fecha: "2023-11-02",
      vencimiento: "2026-11-02",
    },
    {
      id: "cert-3",
      nombre: "Cisco Certified Network Associate (CCNA)",
      emisor: "Cisco",
      fecha: "2021-03-18",
      vencimiento: "2024-03-18",
    },
    {
      id: "cert-4",
      nombre: "Formación docente en entornos virtuales",
      emisor: "Universidad Nacional de La Matanza",
      fecha: "2022-09-30",
      vencimiento: null,
    },
  ],
  proyectos: [
    {
      id: "pro-1",
      nombre: "Detección de anomalías en tráfico SCADA",
      rol: "Investigadora responsable",
      desde: "2022",
      hasta: null,
      descripcion:
        "Proyecto acreditado (PROINCE) sobre detección temprana de intrusiones en redes industriales.",
      documento: { nombre: "informe-avance-scada-2026.pdf" },
      doi: "10.1000/scada.2025.114",
    },
    {
      id: "pro-2",
      nombre: "Aprendizaje federado aplicado a datos clínicos",
      rol: "Co-directora",
      desde: "2023",
      hasta: null,
      descripcion:
        "Colaboración con el Hospital Posadas para entrenar modelos sin centralizar datos de pacientes.",
      documento: null,
      doi: "10.1000/fedlearn.2026.007",
    },
    {
      id: "pro-3",
      nombre: "Modernización del sistema de becas",
      rol: "Consultora técnica",
      desde: "2019",
      hasta: "2021",
      descripcion:
        "Migración del sistema de gestión de becas del Departamento a una arquitectura de servicios.",
      documento: { nombre: "informe-becas.pdf" },
      doi: "",
    },
  ],
  habilidades: [
    { termino: "Bases de datos", sugerido: false },
    { termino: "Ciberseguridad", sugerido: false },
    { termino: "Ciencia de datos", sugerido: false },
    { termino: "Sistemas operativos", sugerido: false },
    { termino: "Análisis forense de red", sugerido: true },
  ],
  intereses: [
    { termino: "Machine learning", sugerido: false },
    { termino: "Privacidad diferencial", sugerido: true },
  ],
};

/** Redes e infraestructura. */
const REDES: Seccion = {
  contacto: { telefono: "11-6732-1145", mail: "" },
  cv: { nombre: "cv-ruiz.pdf", fechaCarga: "2026-03-04" },
  experiencia: [
    {
      id: "exp-1",
      puesto: "Jefe de Cátedra de Redes de Computadoras",
      organizacion: "Departamento de Ingeniería · UNLaM",
      desde: "2016",
      hasta: null,
      descripcion: "Coordinación de la cátedra y del laboratorio de redes.",
    },
    {
      id: "exp-2",
      puesto: "Arquitecto de infraestructura",
      organizacion: "Telecom Argentina",
      desde: "2004",
      hasta: "2016",
      descripcion: "Diseño de la red troncal y planes de contingencia.",
    },
  ],
  educacion: [
    {
      id: "edu-1",
      nivel: "Maestría",
      carrera: "Redes de Datos",
      institucion: "Universidad Nacional de La Plata",
      desde: "2006",
      hasta: "2009",
    },
    {
      id: "edu-2",
      nivel: "Grado",
      carrera: "Ingeniería en Telecomunicaciones",
      institucion: "Universidad Nacional de La Matanza",
      desde: "1996",
      hasta: "2003",
    },
  ],
  certificaciones: [
    {
      id: "cert-1",
      nombre: "Cisco Certified Internetwork Expert (CCIE)",
      emisor: "Cisco",
      fecha: "2019-08-21",
      vencimiento: "2026-08-21",
    },
    {
      id: "cert-2",
      nombre: "Juniper Networks Certified Professional",
      emisor: "Juniper",
      fecha: "2018-04-12",
      vencimiento: "2024-04-12",
    },
  ],
  proyectos: [
    {
      id: "pro-1",
      nombre: "Observatorio de tráfico de la red del campus",
      rol: "Director",
      desde: "2017",
      hasta: null,
      descripcion: "Instrumentación y análisis del tráfico de la red académica de la UNLaM.",
      documento: { nombre: "observatorio-red-2025.pdf" },
      doi: "",
    },
  ],
  habilidades: [
    { termino: "Redes de computadoras", sugerido: false },
    { termino: "Ciberseguridad", sugerido: false },
    { termino: "Cloud computing", sugerido: false },
    { termino: "DevOps", sugerido: false },
  ],
  intereses: [{ termino: "Sistemas embebidos", sugerido: false }],
};

/** Desarrollo de software y docencia. */
const SOFTWARE: Seccion = {
  contacto: { telefono: "11-4523-8801", mail: "carla.lopez.doc@gmail.com" },
  cv: null,
  experiencia: [
    {
      id: "exp-1",
      puesto: "Profesora Adjunta de Ingeniería de Software",
      organizacion: "Departamento de Ingeniería · UNLaM",
      desde: "2018",
      hasta: null,
      descripcion: "Dictado de la materia y dirección de trabajos finales de grado.",
    },
    {
      id: "exp-2",
      puesto: "Tech Lead",
      organizacion: "Mercado Libre",
      desde: "2013",
      hasta: "2019",
      descripcion: "Equipo de checkout: arquitectura de servicios y prácticas de testing.",
    },
  ],
  educacion: [
    {
      id: "edu-1",
      nivel: "Especialización",
      carrera: "Ingeniería de Software",
      institucion: "Universidad Nacional de La Matanza",
      desde: "2014",
      hasta: "2016",
    },
    {
      id: "edu-2",
      nivel: "Grado",
      carrera: "Licenciatura en Sistemas",
      institucion: "Universidad Nacional de La Matanza",
      desde: "2004",
      hasta: "2010",
    },
  ],
  certificaciones: [
    {
      id: "cert-1",
      nombre: "Professional Scrum Master I",
      emisor: "Scrum.org",
      fecha: "2020-06-15",
      vencimiento: null,
    },
  ],
  proyectos: [
    {
      id: "pro-1",
      nombre: "Plataforma de seguimiento de trabajos finales",
      rol: "Directora",
      desde: "2021",
      hasta: "2024",
      descripcion: "Sistema interno para el seguimiento de los TFI de la carrera.",
      documento: null,
      doi: "10.1000/tfi.2024.031",
    },
  ],
  habilidades: [
    { termino: "Arquitectura de software", sugerido: false },
    { termino: "Testing y calidad", sugerido: false },
    { termino: "Desarrollo web", sugerido: false },
    { termino: "Programación orientada a objetos", sugerido: false },
  ],
  intereses: [
    { termino: "Machine learning", sugerido: false },
    { termino: "Enseñanza de la programación", sugerido: true },
  ],
};

/**
 * Qué perfil ve cada usuario de la sesión mock. `admin.aulas` no está: queda
 * deliberadamente sin perfil para poder recorrer el estado vacío.
 */
const PERFILES_POR_UPN: Record<string, Seccion> = {
  "marina.diaz@unlam.edu.ar": DATOS,
  "demo@unlam.edu.ar": DATOS,
  "gustavo.ruiz@unlam.edu.ar": REDES,
  "decanato@unlam.edu.ar": REDES,
  "carla.lopez@unlam.edu.ar": SOFTWARE,
  "secretaria.academica@unlam.edu.ar": SOFTWARE,
};

/** Perfil sin nada cargado: solo el bloque institucional. */
export function seccionesVacias(): Seccion {
  return {
    contacto: { telefono: "", mail: "" },
    cv: null,
    experiencia: [],
    educacion: [],
    certificaciones: [],
    proyectos: [],
    habilidades: [],
    intereses: [],
  };
}

/** Perfil completo del docente, con las secciones que le correspondan. */
export function perfilDe(institucional: PerfilInstitucional): PerfilDocente {
  const secciones = PERFILES_POR_UPN[institucional.upn] ?? seccionesVacias();
  return { institucional, ...structuredClone(secciones) };
}
