import test from "node:test";
import assert from "node:assert/strict";
import {
  mkdtempSync,
  rmSync,
  readdirSync,
  readFileSync,
  mkdirSync,
  writeFileSync,
  symlinkSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { execFileSync } from "node:child_process";

const cli = new URL("../migrations/scaffold.mjs", import.meta.url).pathname;
const validador = new URL("../migrations/validate.mjs", import.meta.url).pathname;
const generador = new URL("../migrations/manifest.mjs", import.meta.url).pathname;
function temporal(t) {
  const raiz = mkdtempSync(join(tmpdir(), "migraciones-"));
  t.after(() => rmSync(raiz, { recursive: true, force: true }));
  return raiz;
}
function ejecutar(archivo, raiz, args = []) {
  return execFileSync(process.execPath, [archivo, "--root", raiz, ...args], {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  });
}

test("scaffold dry-run no escribe y propone SQL y wrapper con metadata única", (t) => {
  const raiz = temporal(t);
  const plan = JSON.parse(ejecutar(cli, raiz, ["Identity", "AgregarIndice", "--dry-run"]));
  assert.equal(plan.archivos.length, 2);
  assert.match(plan.id, /^\d{14}_AgregarIndiceIdentity$/);
  assert.match(plan.archivos[0].ruta, /^database\/identity\/\d{14}_AgregarIndiceIdentity.sql$/);
  assert.match(plan.archivos[1].contenido, /\[DbContext\(typeof\(IdentityDbContext\)\)\]/);
  assert.match(plan.archivos[1].contenido, /\[RecursosMigracionSql\("identity\//);
  assert.match(
    plan.archivos[1].contenido,
    /migrationBuilder.AplicarRecursosSql\(typeof\(AgregarIndiceIdentity\)\)/,
  );
  assert.deepEqual(readdirSync(raiz), []);
});

function escribir(raiz, ruta, texto) {
  mkdirSync(join(raiz, ruta, ".."), { recursive: true });
  writeFileSync(join(raiz, ruta), texto);
}
function crear(t, contexto = "Identity", nombre = "AgregarIndice") {
  const raiz = temporal(t);
  const plan = JSON.parse(ejecutar(cli, raiz, [contexto, nombre]));
  return { raiz, plan };
}

test("inventario reconoce la asociación de SQL generado sin conectar a bases", async (t) => {
  const { raiz, plan } = crear(t);
  const { inventariar } = await import("../migrations/inventario.mjs");
  const inventario = inventariar(raiz);
  assert.equal(inventario.migraciones.length, 1);
  assert.equal(inventario.migraciones[0].id, plan.id);
  assert.deepEqual(inventario.migraciones[0].recursos, [
    plan.archivos[0].ruta.slice("database/".length),
  ]);
  assert.equal(inventario.archivos.length, 2);
});

test("manifiesto protege archivos y validate permite adiciones incrementales", (t) => {
  const { raiz, plan } = crear(t);
  ejecutar(generador, raiz);
  const manifiesto = JSON.parse(readFileSync(join(raiz, "database/migraciones-protegidas.json")));
  assert.equal(manifiesto.version, 1);
  assert.equal(Object.keys(manifiesto.archivos).length, 2);
  assert.match(manifiesto.archivos[plan.archivos[0].ruta], /^[a-f0-9]{64}$/);
  ejecutar(cli, raiz, ["Portal", "AgregarIndice"]);
  assert.match(ejecutar(validador, raiz), /"migraciones": 2/);
});

function git(raiz, ...args) {
  return execFileSync("git", ["-C", raiz, ...args], {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}
function congelar(raiz) {
  git(raiz, "init", "-q");
  git(raiz, "add", ".");
  git(
    raiz,
    "-c",
    "user.name=Prueba",
    "-c",
    "user.email=prueba@example.invalid",
    "commit",
    "-qm",
    "Inventario de prueba",
  );
  return git(raiz, "rev-parse", "HEAD");
}
function falla(archivo, raiz, args, patron) {
  assert.throws(
    () => ejecutar(archivo, raiz, args),
    (error) => {
      assert.equal(error.status, 1);
      assert.match(String(error.stderr), patron);
      return true;
    },
  );
}

test("Git confiable rechaza SQL alterado aunque el PR actualice su hash", (t) => {
  const { raiz, plan } = crear(t);
  ejecutar(generador, raiz);
  const base = congelar(raiz);
  escribir(raiz, plan.archivos[0].ruta, "-- DDL adulterado\n");
  const manifiesto = JSON.parse(readFileSync(join(raiz, "database/migraciones-protegidas.json")));
  // Falsificar el manifiesto mutable no autoriza reescribir historia.
  const nuevoHash = execFileSync("sha256sum", [join(raiz, plan.archivos[0].ruta)], {
    encoding: "utf8",
  }).split(" ")[0];
  manifiesto.archivos[plan.archivos[0].ruta] = nuevoHash;
  escribir(raiz, "database/migraciones-protegidas.json", JSON.stringify(manifiesto));
  falla(validador, raiz, ["--base-ref", base], /protegido modificado|protección histórica/);
});

test("scaffold rechaza colisión de clase aunque el archivo existente use otro nombre", (t) => {
  const { raiz, plan } = crear(t);
  rmSync(join(raiz, plan.archivos[1].ruta));
  rmSync(join(raiz, plan.archivos[0].ruta));
  const carpeta = plan.archivos[1].ruta.slice(0, plan.archivos[1].ruta.lastIndexOf("/"));
  escribir(raiz, `${carpeta}/NombreAnterior.cs`, plan.archivos[1].contenido);
  falla(cli, raiz, ["Identity", "AgregarIndice", "--dry-run"], /Colisión/);
});

for (const [contexto, nombre] of [
  ["../Identity", "Nombre"],
  ["Identity", "../Nombre"],
  ["Identity", "nombre"],
  ["Identity", "Nombre.sql"],
  ["toString", "Nombre"],
  ["Identity", "Nombre_Separado"],
]) {
  test(`scaffold rechaza tokens inseguros: ${contexto}/${nombre}`, (t) => {
    const raiz = temporal(t);
    falla(cli, raiz, [contexto, nombre], /contexto conocido|Nombre inválido/);
    assert.deepEqual(readdirSync(raiz), []);
  });
}

test("scaffold no sobrescribe una migración existente y distingue contextos", (t) => {
  const { raiz, plan } = crear(t);
  falla(cli, raiz, ["Identity", "AgregarIndice"], /Colisión/);
  assert.equal(readFileSync(join(raiz, plan.archivos[0].ruta), "utf8"), plan.archivos[0].contenido);
  const otro = JSON.parse(ejecutar(cli, raiz, ["Portal", "AgregarIndice"]));
  assert.notEqual(plan.id, otro.id);
});

for (const [descripcion, transformar, patron] of [
  ["SQL faltante", (raiz, plan) => rmSync(join(raiz, plan.archivos[0].ruta)), /Recurso faltante/],
  [
    "SQL huérfano",
    (raiz) => escribir(raiz, "database/identity/huerfano.sql", "-- auxiliar"),
    /sin consumidor/,
  ],
  [
    "metadata comentada",
    (raiz, plan) =>
      escribir(
        raiz,
        plan.archivos[1].ruta,
        plan.archivos[1].contenido.replace("[RecursosMigracionSql(", "// [RecursosMigracionSql("),
      ),
    /atributo RecursosMigracionSql/,
  ],
  [
    "path traversal",
    (raiz, plan) =>
      escribir(
        raiz,
        plan.archivos[1].ruta,
        plan.archivos[1].contenido.replace(/identity\//, "../identity/"),
      ),
    /ruta inválida/,
  ],
  [
    "DbContext incorrecto",
    (raiz, plan) =>
      escribir(
        raiz,
        plan.archivos[1].ruta,
        plan.archivos[1].contenido.replace("typeof(IdentityDbContext)", "typeof(PortalDbContext)"),
      ),
    /DbContext incorrecto/,
  ],
  [
    "recursos duplicados",
    (raiz, plan) =>
      escribir(
        raiz,
        plan.archivos[1].ruta,
        plan.archivos[1].contenido.replace(
          /\[RecursosMigracionSql\("([^"]+)"\)\]/,
          '[RecursosMigracionSql("$1", "$1")]',
        ),
      ),
    /Recurso duplicado/,
  ],
  [
    "IDs duplicados",
    (raiz, plan) =>
      escribir(raiz, plan.archivos[1].ruta.replace(".cs", "Copia.cs"), plan.archivos[1].contenido),
    /ID duplicado/,
  ],
  [
    "timestamp inválido",
    (raiz, plan) =>
      escribir(
        raiz,
        plan.archivos[1].ruta,
        plan.archivos[1].contenido.replace(plan.id, `20261305000000_${plan.id.slice(15)}`),
      ),
    /Timestamp inválido/,
  ],
  [
    "Up no usa metadata",
    (raiz, plan) =>
      escribir(
        raiz,
        plan.archivos[1].ruta,
        plan.archivos[1].contenido.replace(
          "migrationBuilder.AplicarRecursosSql",
          "migrationBuilder.Sql",
        ),
      ),
    /Up debe consumir metadata/,
  ],
]) {
  test(`inventario rechaza ${descripcion}`, async (t) => {
    const { raiz, plan } = crear(t);
    transformar(raiz, plan);
    const { inventariar } = await import("../migrations/inventario.mjs");
    assert.throws(() => inventariar(raiz), patron);
  });
}

test("inventario preserva orden de recursos y exige excepciones exactas motivadas", async (t) => {
  const { raiz, plan } = crear(t);
  escribir(raiz, "database/audit/auxiliar.sql", "-- attach diferido");
  escribir(raiz, "database/identity/bootstrap.sql", "-- soporte no migratorio");
  escribir(
    raiz,
    plan.archivos[1].ruta,
    plan.archivos[1].contenido.replace(
      /\[RecursosMigracionSql\("([^"]+)"\)\]/,
      '[RecursosMigracionSql("audit/auxiliar.sql", "$1")]',
    ),
  );
  const { inventariar } = await import("../migrations/inventario.mjs");
  const clasificados = {
    "identity/bootstrap.sql": "Soporte explícito de bootstrap, no se aplica desde EF.",
  };
  const inventario = inventariar(raiz, clasificados);
  assert.deepEqual(inventario.migraciones[0].recursos, [
    "audit/auxiliar.sql",
    plan.archivos[0].ruta.slice(9),
  ]);
  assert.throws(
    () => inventariar(raiz, { "identity/*.sql": "Excepción global prohibida" }),
    /sin consumidor|Clasificación inválida/,
  );
  assert.throws(
    () => inventariar(raiz, { ...clasificados, "audit/auxiliar.sql": "Recurso ya consumido" }),
    /también clasificado/,
  );
});

for (const tipo of ["sql", "wrapper", "manifiesto"]) {
  test(`Git confiable rechaza eliminación de ${tipo}`, (t) => {
    const { raiz, plan } = crear(t);
    ejecutar(generador, raiz);
    const base = congelar(raiz);
    const ruta =
      tipo === "manifiesto"
        ? "database/migraciones-protegidas.json"
        : plan.archivos[tipo === "sql" ? 0 : 1].ruta;
    rmSync(join(raiz, ruta));
    falla(validador, raiz, ["--base-ref", base], /protegido eliminado|ENOENT/);
  });
}

test("Git confiable impide retirar entradas del manifiesto", (t) => {
  const { raiz, plan } = crear(t);
  ejecutar(generador, raiz);
  const base = congelar(raiz);
  const manifiesto = JSON.parse(readFileSync(join(raiz, "database/migraciones-protegidas.json")));
  delete manifiesto.archivos[plan.archivos[0].ruta];
  escribir(raiz, "database/migraciones-protegidas.json", JSON.stringify(manifiesto));
  falla(validador, raiz, ["--base-ref", base], /protección histórica/);
});

test("Git protege adiciones publicadas aunque no se actualice el manifiesto global", (t) => {
  const { raiz } = crear(t);
  ejecutar(generador, raiz);
  const base = congelar(raiz);
  const adicion = JSON.parse(ejecutar(cli, raiz, ["Portal", "OtroIndice"]));
  assert.match(ejecutar(validador, raiz, ["--base-ref", base]), /"proteccionGit": true/);
  const publicada = congelar(raiz);
  escribir(raiz, adicion.archivos[0].ruta, "-- cambio histórico");
  falla(validador, raiz, ["--base-ref", publicada], /protegido modificado/);
});

test("generador no rehasha historia alterada y dry-run no escribe manifiesto", (t) => {
  const { raiz, plan } = crear(t);
  ejecutar(generador, raiz, ["--dry-run"]);
  assert.throws(() => readFileSync(join(raiz, "database/migraciones-protegidas.json")), /ENOENT/);
  ejecutar(generador, raiz);
  escribir(raiz, plan.archivos[0].ruta, "-- cambio histórico");
  falla(generador, raiz, [], /protegido modificado/);
});

test("corte inicial sólo acepta inventario de commit explícito y no se puede repetir", (t) => {
  const raiz = temporal(t);
  escribir(raiz, "database/identity/alpha.sql", "-- historia alpha");
  const alpha = congelar(raiz);
  rmSync(join(raiz, "database/identity/alpha.sql"));
  ejecutar(cli, raiz, ["Identity", "Baseline"]);
  ejecutar(generador, raiz);
  const corte = congelar(raiz);
  falla(validador, raiz, ["--base-ref", alpha], /commit revisado explícito/);
  falla(
    validador,
    raiz,
    ["--base-ref", alpha, "--corte-revisado", alpha],
    /no contiene inventario/,
  );
  assert.match(
    ejecutar(validador, raiz, ["--base-ref", alpha, "--corte-revisado", corte]),
    /"proteccionGit": true/,
  );
  falla(
    validador,
    raiz,
    ["--base-ref", corte, "--corte-revisado", corte],
    /sólo se admite una vez/,
  );
  falla(validador, raiz, ["--base-ref", "HEAD"], /SHA completo/);
  falla(validador, raiz, ["--force"], /Opción desconocida/);
});

test("CI selecciona tooling, conserva historia Git y verifica recursos compilados", () => {
  const workflow = readFileSync(new URL("../../.github/workflows/ci.yml", import.meta.url), "utf8");
  assert.match(workflow, /- 'scripts\/migrations\/\*\*'/);
  assert.match(workflow, /- 'scripts\/tests\/migrations\.test\.mjs'/);
  assert.match(workflow, /migrations:\n[\s\S]*?fetch-depth: 0/);
  assert.match(workflow, /github\.event\.pull_request\.base\.sha/);
  assert.match(workflow, /node scripts\/migrations\/validate\.mjs/);
  assert.match(workflow, /--validar-recursos/);
  for (const referencia of workflow.matchAll(/uses: ([^\s]+)/g))
    assert.match(referencia[1], /@[a-f0-9]{40}$/);
});

test("inventario rechaza directorio usado como recurso SQL", async (t) => {
  const { raiz, plan } = crear(t);
  rmSync(join(raiz, plan.archivos[0].ruta));
  mkdirSync(join(raiz, plan.archivos[0].ruta));
  const { inventariar } = await import("../migrations/inventario.mjs");
  assert.throws(() => inventariar(raiz), /Recurso faltante|no es archivo/);
});

test("snapshot EF representa el modelo actual y puede evolucionar con una adición", (t) => {
  const { raiz } = crear(t);
  const snapshot =
    "backend/src/ArsDocendi.Shared/Identity/Migrations/IdentityDbContextModelSnapshot.cs";
  escribir(raiz, snapshot, "// snapshot inicial\n");
  ejecutar(generador, raiz);
  const base = congelar(raiz);
  ejecutar(cli, raiz, ["Identity", "OtraTabla"]);
  escribir(raiz, snapshot, "// snapshot con OtraTabla\n");
  assert.match(ejecutar(validador, raiz, ["--base-ref", base]), /"proteccionGit": true/);
});

test("scaffold de todos los contextos tiene identidades distintas e inventario válido", async (t) => {
  const raiz = temporal(t);
  const planes = ["Identity", "Storage", "Designaciones", "Portal", "Aulas", "Tareas"].map(
    (contexto) => JSON.parse(ejecutar(cli, raiz, [contexto, "AgregarIndice"])),
  );
  assert.equal(new Set(planes.map((plan) => plan.id)).size, 6);
  const { inventariar } = await import("../migrations/inventario.mjs");
  assert.equal(inventariar(raiz).migraciones.length, 6);
});

test("scaffold rechaza symlinks sin escribir fuera del workspace", (t) => {
  const raiz = temporal(t);
  const externo = temporal(t);
  symlinkSync(externo, join(raiz, "database"));
  falla(cli, raiz, ["Identity", "AgregarIndice", "--dry-run"], /Symlink no permitido/);
  falla(cli, raiz, ["Identity", "AgregarIndice"], /Symlink no permitido/);
  assert.deepEqual(readdirSync(externo), []);
});

test("Git confiable rechaza wrapper alterado aunque se rehashée el manifiesto mutable", async (t) => {
  const { raiz, plan } = crear(t);
  ejecutar(generador, raiz);
  const base = congelar(raiz);
  escribir(raiz, plan.archivos[1].ruta, plan.archivos[1].contenido + "// Reescritura histórica\n");
  const manifiesto = JSON.parse(readFileSync(join(raiz, "database/migraciones-protegidas.json")));
  const { hash } = await import("../migrations/proteccion.mjs");
  manifiesto.archivos[plan.archivos[1].ruta] = hash(
    readFileSync(join(raiz, plan.archivos[1].ruta)),
  );
  escribir(raiz, "database/migraciones-protegidas.json", JSON.stringify(manifiesto));
  falla(validador, raiz, ["--base-ref", base], /protegido modificado/);
});
