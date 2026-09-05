// ============================================================
// Padrón mock de personas del sistema — identidad (Azure AD) y datos
// institucionales (Secretaría) de cada docente.
//
// Vive en shared/ porque lo consumen varias features: `docentes` lo usa como
// catálogo al dar de alta, y `portal` lee de acá el bloque de solo lectura del
// perfil. Las features no se importan entre sí (ver react-features-guide).
//
// El `telefono` de este padrón es el histórico que administra Secretaría; el
// Portal NO lo lee: el teléfono de contacto lo mantiene el propio docente.
//
// Incluye los UPN de la sesión mock de desarrollo para que esos logins
// encuentren su perfil.
//
// STUB: reemplazar por la API real cuando exista Modules.Portal.
// ============================================================

export interface PersonaSistema {
  id: string;
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string;
  cuil: string;
  fecha_nacimiento: string;
  telefono: string;
  upn: string;
}

export const PERSONAS_SISTEMA: PersonaSistema[] = [
  {
    id: "p0000000-0000-4000-8000-000000000001",
    nombre: "Carla",
    apellido: "López",
    documento: "28341567",
    legajo: "0421",
    cuil: "27-28341567-3",
    fecha_nacimiento: "1980-03-14",
    telefono: "11-4523-8801",
    upn: "carla.lopez@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000002",
    nombre: "Gustavo",
    apellido: "Ruiz",
    documento: "22156789",
    legajo: "0115",
    cuil: "20-22156789-2",
    fecha_nacimiento: "1975-07-22",
    telefono: "11-6732-1145",
    upn: "gustavo.ruiz@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000003",
    nombre: "Marina",
    apellido: "Díaz",
    documento: "31089234",
    legajo: "0033",
    cuil: "27-31089234-8",
    fecha_nacimiento: "1985-11-05",
    telefono: "11-5548-9900",
    upn: "marina.diaz@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000004",
    nombre: "Paula",
    apellido: "Gómez",
    documento: "35678901",
    legajo: "0058",
    cuil: "27-35678901-9",
    fecha_nacimiento: "1992-06-11",
    telefono: "11-3324-6612",
    upn: "paula.gomez@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000005",
    nombre: "Sofía",
    apellido: "Peralta",
    documento: "38901234",
    legajo: "0387",
    cuil: "27-38901234-1",
    fecha_nacimiento: "1995-04-27",
    telefono: "11-2298-7754",
    upn: "sofia.peralta@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000006",
    nombre: "Federico",
    apellido: "Moreno",
    documento: "29876543",
    legajo: "0202",
    cuil: "20-29876543-1",
    fecha_nacimiento: "1982-08-15",
    telefono: "11-7711-2200",
    upn: "federico.moreno@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000007",
    nombre: "Natalia",
    apellido: "Castro",
    documento: "33445566",
    legajo: "0312",
    cuil: "27-33445566-5",
    fecha_nacimiento: "1988-01-20",
    telefono: "11-4455-7788",
    upn: "natalia.castro@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000008",
    nombre: "Lucía",
    apellido: "Fernández",
    documento: "27654321",
    legajo: "0007",
    cuil: "27-27654321-4",
    fecha_nacimiento: "1979-02-09",
    telefono: "11-4001-2233",
    upn: "secretaria.academica@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000009",
    nombre: "Roberto",
    apellido: "Sosa",
    documento: "17234890",
    legajo: "0001",
    cuil: "20-17234890-6",
    fecha_nacimiento: "1965-10-30",
    telefono: "11-4770-5566",
    upn: "decanato@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000010",
    nombre: "Paula",
    apellido: "Gómez",
    documento: "35678901",
    legajo: "0058",
    cuil: "27-35678901-9",
    fecha_nacimiento: "1992-06-11",
    telefono: "11-3324-6612",
    upn: "admin.aulas@unlam.edu.ar",
  },
];
