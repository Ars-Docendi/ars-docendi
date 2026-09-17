import type {
  Certificacion,
  DatosCertificacion,
  DatosEducacion,
  DatosExperiencia,
  DatosProyecto,
  Educacion,
  Experiencia,
  Proyecto,
  Tag,
} from "./types";

export const VOCABULARIO_EXPERTICIA = [
  "Algoritmos",
  "Arquitectura de software",
  "Bases de datos",
  "Ciberseguridad",
  "Ciencia de datos",
  "Cloud computing",
  "Computación gráfica",
  "Desarrollo web",
  "DevOps",
  "Machine learning",
  "Matemática discreta",
  "Programación orientada a objetos",
  "Redes de computadoras",
  "Robótica",
  "Sistemas embebidos",
  "Sistemas operativos",
  "Testing y calidad",
];
export const agregarExperiencia = (
  lista: Experiencia[],
  datos: DatosExperiencia,
): Experiencia[] => [...lista, { ...datos, id: crypto.randomUUID() }];
export const editarExperiencia = (
  lista: Experiencia[],
  id: string,
  datos: DatosExperiencia,
): Experiencia[] => lista.map((x) => (x.id === id ? { ...datos, id } : x));
export const agregarEducacion = (lista: Educacion[], datos: DatosEducacion): Educacion[] => [
  ...lista,
  { ...datos, id: crypto.randomUUID() },
];
export const editarEducacion = (
  lista: Educacion[],
  id: string,
  datos: DatosEducacion,
): Educacion[] => lista.map((x) => (x.id === id ? { ...datos, id } : x));
export const agregarCertificacion = (
  lista: Certificacion[],
  datos: DatosCertificacion,
): Certificacion[] => [...lista, { ...datos, id: crypto.randomUUID() }];
export const editarCertificacion = (
  lista: Certificacion[],
  id: string,
  datos: DatosCertificacion,
): Certificacion[] => lista.map((x) => (x.id === id ? { ...datos, id } : x));
export const agregarProyecto = (lista: Proyecto[], datos: DatosProyecto): Proyecto[] => [
  ...lista,
  { ...datos, id: crypto.randomUUID() },
];
export const editarProyecto = (lista: Proyecto[], id: string, datos: DatosProyecto): Proyecto[] =>
  lista.map((x) => (x.id === id ? { ...datos, id } : x));
export const eliminarPorId = <T extends { id: string }>(lista: T[], id: string): T[] =>
  lista.filter((x) => x.id !== id);
export const agregarTag = (lista: Tag[], termino: string): Tag[] => {
  const limpio = termino.trim();
  if (!limpio || lista.some((x) => x.termino.toLowerCase() === limpio.toLowerCase())) return lista;
  return [
    ...lista,
    {
      termino: limpio,
      sugerido: !VOCABULARIO_EXPERTICIA.some((x) => x.toLowerCase() === limpio.toLowerCase()),
    },
  ];
};
export const quitarTag = (lista: Tag[], termino: string): Tag[] =>
  lista.filter((x) => x.termino !== termino);
export const vocabularioDisponible = (lista: Tag[]): string[] => {
  const usados = new Set(lista.map((x) => x.termino.toLowerCase()));
  return VOCABULARIO_EXPERTICIA.filter((x) => !usados.has(x.toLowerCase()));
};
