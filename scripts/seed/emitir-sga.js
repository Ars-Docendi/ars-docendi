#!/usr/bin/env node
// Emite el SQL de carga del SGA para el modelo académico: carreras, planes y pertenencias
// materia–plan (catálogo informativo, sin FK de negocio), materias canónicas, y personas/cuentas
// con su rol docente (materia_id + carrera_id directos). Ver change rediseno-modelo-academico.
//
// Uso: node scripts/seed/emitir-sga.js --entrada <sga-resultado.json> --salida <sga.sql> [--sin-legajo 1616]
//
// El JSON de entrada lo genera el paso CSV → JSON a partir de materias.csv, materias_docente.csv
// y docentes.csv. Tiene datos personales: no se versiona, y su ruta se pasa por argumento.
//
// Los ids de planes, materias y pertenencias son deterministas (se derivan de sus claves
// naturales), así que volver a generar el SQL no duplica filas.
const fs = require("fs");
const crypto = require("crypto");

const argumentos = process.argv.slice(2);
function opcion(nombre) {
  const indice = argumentos.indexOf(nombre);
  if (indice < 0 || !argumentos[indice + 1]) throw new Error(`Falta ${nombre} <valor>`);
  return argumentos[indice + 1];
}

const entrada = opcion("--entrada");
const salida = opcion("--salida");
// Legajos que ya usa sintetico.sql: la persona del SGA que los tenga se carga sin legajo.
const sinLegajo = new Set(
  argumentos.includes("--sin-legajo")
    ? opcion("--sin-legajo")
        .split(",")
        .map((v) => v.trim())
    : [],
);
const ROL_DOCENTE = "a1000000-0000-4000-8000-000000000001";
const d = JSON.parse(fs.readFileSync(entrada, "utf8"));
for (const persona of d.personas) {
  if (sinLegajo.has(String(persona.legajo))) persona.legajo = null;
}

// UUID determinístico a partir de una clave natural.
function uuidDe(texto) {
  const h = crypto.createHash("md5").update(texto).digest("hex");
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(13, 16)}-8${h.slice(17, 20)}-${h.slice(20, 32)}`;
}
const planId = (carreraId, codigo) => uuidDe(`plan:${carreraId}:${codigo}`);
const canonicaId = (codigo) => uuidDe(`materia:${codigo}`);
const pertenenciaId = (carreraId, codigoPlan, codigoMateria) =>
  uuidDe(`materia_plan:${planId(carreraId, codigoPlan)}:${codigoMateria}`);

const s = (valor) =>
  valor === null || valor === undefined ? "NULL" : `'${String(valor).replace(/'/g, "''")}'`;
const b = (valor) => (valor ? "TRUE" : "FALSE");
const filas = (lista, mapear) => lista.map(mapear).join(",\n    ");

// Carrera de cada plan y código de materia, para resolver la pertenencia de cada plan.
const planPorId = new Map(d.planes.map((p) => [p.id, p]));
const materiasPlanSql = d.materiasPlan.map((mp) => {
  const plan = planPorId.get(mp.planId);
  return {
    id: pertenenciaId(plan.carreraId, plan.codigo, mp.codigo),
    planId: planId(plan.carreraId, plan.codigo),
    codigo: mp.codigo,
    activo: mp.isActive,
  };
});
const planesSql = d.planes.map((p) => ({
  id: planId(p.carreraId, p.codigo),
  carreraId: p.carreraId,
  codigo: p.codigo,
  vigente: p.vigente,
}));

// Rol docente: materia canónica + carrera directa. Ya no pasa por la pertenencia de plan
// (ver change rediseno-modelo-academico): la carrera del docente la da directamente
// materias_docente.csv, sin ambigüedad de plan que resolver.
const rolesSql = d.userRoles.map((rol) => {
  if (rol.roleId !== ROL_DOCENTE)
    throw new Error(`Rol no contemplado en la carga del SGA: ${rol.roleId}`);
  return rol;
});

const sql = `-- Carga real del SGA (sistema externo): carreras, planes, materias canónicas,
-- pertenencias materia–plan, personas, cuentas provisorias y asignaciones docentes.
--
-- Generado por scripts/seed/emitir-sga.js. No versionar el resultado: contiene datos personales.
--
-- Planes: uno por propuesta y código de plan, con nombre igual al código. \`vigente\` viene de
-- plan_estado = V. Los planes A (activos no vigentes) quedan activos (activo = TRUE) porque tienen alumnos.
-- Materias canónicas: una por código. Las pertenencias materia–plan no tienen duplicados.
--
-- Los azure_oid de identity.users son PROVISORIOS: VinculadorPrimerLogin los reemplaza por el
-- oid real en el primer login con Azure AD, localizando la cuenta por upn.

BEGIN;

-- Carreras
INSERT INTO identity.carreras (id, code, name, is_active) VALUES
    ${filas(Object.entries(d.carreraUuidPorCodigo), ([codigo, id]) => `(${s(id)}, ${s(codigo)}, ${s(d.carreraNombrePorCodigo[codigo])}, TRUE)`)}
ON CONFLICT (id) DO UPDATE SET code = EXCLUDED.code, name = EXCLUDED.name, is_active = EXCLUDED.is_active;

-- Planes
INSERT INTO identity.planes (id, carrera_id, codigo, nombre, vigente, activo) VALUES
    ${filas(planesSql, (p) => `(${s(p.id)}, ${s(p.carreraId)}, ${s(p.codigo)}, ${s(p.codigo)}, ${b(p.vigente)}, TRUE)`)}
ON CONFLICT (id) DO UPDATE SET nombre = EXCLUDED.nombre, vigente = EXCLUDED.vigente, activo = EXCLUDED.activo;

-- Materias canónicas (una por código)
INSERT INTO identity.materias (id, code, name, is_active) VALUES
    ${filas(d.materias, (m) => `(${s(canonicaId(m.code))}, ${s(m.code)}, ${s(m.name)}, ${b(m.isActive)})`)}
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, is_active = EXCLUDED.is_active;

-- Pertenencias materia–plan
INSERT INTO identity.materias_plan (id, plan_id, materia_id, activo) VALUES
    ${filas(materiasPlanSql, (mp) => `(${s(mp.id)}, ${s(mp.planId)}, (SELECT id FROM identity.materias WHERE code = ${s(mp.codigo)}), ${b(mp.activo)})`)}
ON CONFLICT (id) DO UPDATE SET activo = EXCLUDED.activo;

-- Personas
INSERT INTO identity.personas (id, documento, cuil, legajo, nombre, apellido, fecha_nacimiento, telefono) VALUES
    ${filas(d.personas, (p) => `(${s(p.id)}, ${s(p.documento)}, ${s(p.cuil)}, ${s(p.legajo)}, ${s(p.nombre)}, ${s(p.apellido)}, ${s(p.fechaNacimiento)}, ${s(p.telefono)})`)}
ON CONFLICT (id) DO UPDATE SET
    cuil = EXCLUDED.cuil, legajo = EXCLUDED.legajo, nombre = EXCLUDED.nombre, apellido = EXCLUDED.apellido,
    fecha_nacimiento = EXCLUDED.fecha_nacimiento, telefono = EXCLUDED.telefono;

-- Cuentas (oid provisorio)
INSERT INTO identity.users (id, azure_oid, upn, display_name, is_active, persona_id) VALUES
    ${filas(d.usuarios, (u) => `(${s(u.id)}, ${s(u.azureOid)}, ${s(u.upn)}, ${s(u.nombreParaMostrar)}, TRUE, ${s(u.personaId)})`)}
ON CONFLICT (id) DO UPDATE SET display_name = EXCLUDED.display_name, persona_id = EXCLUDED.persona_id;

-- Rol docente: materia canónica + carrera directa
INSERT INTO identity.user_roles (id, user_id, role_id, materia_id, carrera_id) VALUES
    ${filas(rolesSql, (r) => `(${s(r.id)}, ${s(r.userId)}, ${s(r.roleId)}, (SELECT id FROM identity.materias WHERE code = ${s(r.materiaCodigo)}), ${s(r.carreraId)})`)}
ON CONFLICT (user_id, role_id, materia_id, carrera_id) WHERE deleted_at IS NULL DO NOTHING;

COMMIT;
`;

fs.writeFileSync(salida, sql);
console.log(
  `SQL escrito en ${salida}: ${d.materias.length} materias, ${materiasPlanSql.length} pertenencias, ${planesSql.length} planes, ${rolesSql.length} roles docentes.`,
);
