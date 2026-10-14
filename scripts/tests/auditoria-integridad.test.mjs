import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { escenarios } from "./fixtures/auditoria-integridad/escenarios.mjs";

const raiz = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const fixtures = resolve(raiz, "scripts/tests/fixtures/auditoria-integridad");
const anterior = "f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b";
const sha256 = (texto) => createHash("sha256").update(texto).digest("hex");
function ejecutar(comando, argumentos, input, permitirFallo = false) {
  const r = spawnSync(comando, argumentos, {
    cwd: raiz,
    input,
    encoding: "utf8",
    timeout: 120_000,
    maxBuffer: 16 * 1024 * 1024,
  });
  if (!permitirFallo && (r.error || r.status !== 0)) {
    throw new Error(`${comando} ${argumentos.join(" ")}: ${r.error?.message ?? r.stderr}`);
  }
  return r;
}
function git(ruta) {
  return ejecutar("git", ["show", `${anterior}:${ruta}`]).stdout;
}
function recursosAnteriores() {
  const rutas = ejecutar("git", ["ls-tree", "-r", "--name-only", anterior, "backend/src"])
    .stdout.trim()
    .split("\n");
  const proyectos = [
    "ArsDocendi.Shared",
    "ArsDocendi.Storage",
    "Modules.Designaciones",
    "Modules.Portal",
    "Modules.Tareas",
  ];
  const migraciones = [];
  for (const proyecto of proyectos) {
    const declaraciones = rutas
      .filter(
        (p) =>
          p.startsWith(`backend/src/${proyecto}/`) &&
          /\/Migrations\/\d{14}_.*\.cs$/.test(p) &&
          !p.endsWith(".Designer.cs"),
      )
      .map((ruta) => {
        const contenido = git(ruta);
        const id = contenido.match(/\[Migration\("([^"]+)"\)\]/)?.[1];
        const atributo = contenido.match(/\[RecursosMigracionSql\(([\s\S]*?)\)\]/)?.[1];
        assert.ok(id && atributo, `Metadata literal faltante: ${ruta}`);
        const recursos = [...atributo.matchAll(/"([^"]+\.sql)"/g)].map((m) => m[1]);
        assert.ok(recursos.length > 0);
        for (const recurso of recursos) assert.match(recurso, /^[a-z]+\/[a-zA-Z0-9_-]+\.sql$/);
        return { id, wrapper: ruta, wrapperSha256: sha256(contenido), recursos };
      })
      .sort((a, b) => a.id.localeCompare(b.id));
    migraciones.push(...declaraciones);
  }
  assert.equal(migraciones.length, 9, "Inventario anterior esperado de nueve wrappers");
  const aplicados = new Set();
  return migraciones.map((m) => ({
    ...m,
    recursos: m.recursos.map((ruta) => {
      assert.ok(!aplicados.has(ruta), `Recurso duplicado: ${ruta}`);
      aplicados.add(ruta);
      const sql = git(`database/${ruta}`);
      return { ruta, sha256: sha256(sql), sql };
    }),
  }));
}

test("Inventario persistente: SHA anterior y clasificación explícita sin artefactos ignorados", () => {
  assert.equal(new Set(escenarios.map((e) => e.id)).size, escenarios.length);
  for (const e of escenarios) {
    assert.equal(e.clasificacion, "SQL");
    assert.ok(e.limite && e.esperado && e.sql && e.hallazgos.length);
    assert.ok(!e.sql.includes(".artifacts") && !e.sql.includes("sga.sql"));
  }
  assert.equal(escenarios.length, 13);
  recursosAnteriores();
});

test(
  "PostgreSQL desechable: reproducciones SQL auditadas de f10e0a5",
  {
    skip:
      process.env.AUDITORIA_DOCKER !== "1"
        ? "Activar AUDITORIA_DOCKER=1 para ejecución real"
        : false,
  },
  async (t) => {
    const salida = process.env.AUDITORIA_EVIDENCIA;
    if (salida)
      assert.equal(
        dirname(resolve(raiz, salida)),
        fixtures,
        "Evidencia solo dentro del directorio propio",
      );
    const nombre = `auditoria-integridad-${randomUUID()}`;
    const informe = {
      anterior,
      inicio: new Date().toISOString(),
      clasificacion: "SQL",
      comandos: [
        "git show <SHA>:<wrapper/recurso>",
        "docker run -d --name <aleatorio> --network none --tmpfs /var/lib/postgresql -e POSTGRES_HOST_AUTH_METHOD=trust postgres:18.4",
        "docker exec -i <aleatorio> psql -X -q -A -t -v ON_ERROR_STOP=1 -U postgres -d postgres",
        "docker rm -f -v <aleatorio>",
        "docker inspect <aleatorio> (debe fallar: No such object)",
      ],
      resultados: [],
      coberturaNoEjecutada: [
        "API/HTTP y circuito C#; runner EF, histories y recursos compilados",
        "Carreras A01/A02/A05/A06/A11/A12 y proveedor SeaweedFS real",
        "SQL realmente emitido por EF para P01 (solo JOIN análogo)",
        "Planes/repeticiones/costo de escritura P02/P03/P04 y catálogo global de auditoría",
        "Correcciones GREEN, upgrade/no-op y diagnóstico operativo del candidato",
      ],
    };
    let creado = false;
    const psql = (sql, permitirFallo = false) =>
      ejecutar(
        "docker",
        [
          "exec",
          "-i",
          nombre,
          "psql",
          "-X",
          "-q",
          "-A",
          "-t",
          "-v",
          "ON_ERROR_STOP=1",
          "-U",
          "postgres",
          "-d",
          "postgres",
        ],
        sql,
        permitirFallo,
      );
    try {
      const migraciones = recursosAnteriores();
      informe.migraciones = migraciones.map((m) => ({
        ...m,
        recursos: m.recursos.map(({ sql, ...r }) => r),
      }));
      ejecutar("docker", [
        "run",
        "-d",
        "--name",
        nombre,
        "--network",
        "none",
        "--tmpfs",
        "/var/lib/postgresql",
        "-e",
        "POSTGRES_HOST_AUTH_METHOD=trust",
        "postgres:18.4",
      ]);
      creado = true;
      let listo = false;
      for (let intento = 0; intento < 60; intento++) {
        if (psql("SELECT 1;", true).status === 0) {
          listo = true;
          break;
        }
        await new Promise((r) => setTimeout(r, 1000));
      }
      assert.ok(listo, "PostgreSQL no estuvo listo en 60 segundos");
      informe.servidor = psql("SHOW server_version;").stdout.trim();
      informe.imagen = ejecutar("docker", [
        "inspect",
        "--format",
        "{{.Image}}",
        nombre,
      ]).stdout.trim();
      for (const m of migraciones)
        psql(`BEGIN;\n${m.recursos.map((r) => r.sql).join("\n")}\nCOMMIT;`);
      const preparacion = readFileSync(resolve(fixtures, "preparacion.sql"), "utf8");
      informe.preparacionSha256 = sha256(preparacion);
      psql(`BEGIN;\n${preparacion}\nCOMMIT;`);
      for (const e of escenarios) {
        await t.test(`${e.id} [${e.clasificacion}]`, () => {
          const registro = {
            id: e.id,
            hallazgos: e.hallazgos,
            clasificacion: e.clasificacion,
            limite: e.limite,
            sqlSha256: sha256(e.sql),
            esperado: e.esperado,
          };
          informe.resultados.push(registro);
          const r = psql(`BEGIN; SET LOCAL TIME ZONE 'UTC';\n${e.sql}\nROLLBACK;`, true);
          registro.psqlExit = r.status;
          if (r.error || r.status !== 0) registro.error = r.error?.message ?? r.stderr;
          assert.equal(r.status, 0, registro.error);
          const lineas = r.stdout.split("\n").filter((linea) => linea.startsWith("{"));
          assert.equal(lineas.length, 1, "El probe debe emitir exactamente un resultado JSON");
          registro.observado = JSON.parse(lineas[0]);
          assert.deepEqual(registro.observado, e.esperado);
          registro.reproducido = true;
        });
      }
    } finally {
      if (creado) {
        ejecutar("docker", ["rm", "-f", "-v", nombre]);
        const inspeccion = ejecutar("docker", ["inspect", nombre], undefined, true);
        informe.cleanup = {
          contenedor: nombre,
          exit: inspeccion.status,
          ausente: inspeccion.status !== 0 && /no such object/i.test(inspeccion.stderr),
        };
        assert.equal(informe.cleanup.ausente, true, "Ausencia del contenedor no verificada");
      }
      informe.fin = new Date().toISOString();
      informe.total = informe.resultados.length;
      informe.reproducidos = informe.resultados.filter((r) => r.reproducido).length;
      if (salida) writeFileSync(resolve(raiz, salida), `${JSON.stringify(informe, null, 2)}\n`);
      t.diagnostic(
        `SQL reproducidos: ${informe.reproducidos}/${escenarios.length}; cleanup: ${informe.cleanup?.ausente ?? false}`,
      );
    }
  },
);
