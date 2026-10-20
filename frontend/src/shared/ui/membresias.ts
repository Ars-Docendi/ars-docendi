export interface RolMembresiaOpcion {
  id: string;
  codigo: string;
  nombre: string;
  ambito: string;
}

/**
 * Fila de la UI. Para Docente, `materiaId` y `carreraId` van juntos (una materia dictada en
 * más de una carrera exige elegir una); para Jefe de Cátedra, sólo `materiaId`; para
 * Coordinador, sólo `carreraId`.
 */
export interface MembresiaFila {
  rolId: string;
  materiaId: string;
  carreraId: string;
}

/** Forma que recibe la API: qué campo lleva depende del rol. */
export interface MembresiaGuardar {
  rolId: string;
  materiaId: string | null;
  carreraId: string | null;
}

/**
 * Docente: materia y carrera juntas. Jefe de Cátedra: sólo la materia canónica.
 * Coordinador: sólo la carrera. El código de rol decide qué campos lleva.
 */
export function membresiaAGuardar(
  fila: { rolId: string; materiaId: string | null; carreraId: string | null },
  roles: RolMembresiaOpcion[],
): MembresiaGuardar {
  const rol = roles.find((opcion) => opcion.id === fila.rolId);
  const esDocente = rol?.codigo === "docente";
  const llevaMateria = esDocente || rol?.ambito === "materia";
  const llevaCarrera = esDocente || rol?.ambito === "carrera";
  return {
    rolId: fila.rolId,
    materiaId: llevaMateria ? fila.materiaId || null : null,
    carreraId: llevaCarrera ? fila.carreraId || null : null,
  };
}
