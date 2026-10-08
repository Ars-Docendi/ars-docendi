import test from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const generador = new URL("../seed/emitir-sga.js", import.meta.url).pathname;

test("el ID de materia canónica usa SHA-256 y permanece determinista", (t) => {
  const raiz = mkdtempSync(join(tmpdir(), "seed-sga-"));
  t.after(() => rmSync(raiz, { recursive: true, force: true }));
  const entrada = join(raiz, "entrada.json");
  const salida = join(raiz, "salida.sql");
  writeFileSync(
    entrada,
    JSON.stringify({
      carreraUuidPorCodigo: {},
      carreraNombrePorCodigo: {},
      planes: [],
      materiasPlan: [],
      personas: [],
      usuarios: [],
      userRoles: [],
      materias: [{ code: "01032", name: "Álgebra", isActive: true }],
    }),
  );
  const generar = () =>
    execFileSync(process.execPath, [generador, "--entrada", entrada, "--salida", salida]);
  generar();
  const sql = readFileSync(salida, "utf8");
  assert.match(sql, /INSERT INTO identity\.materias[^;]*cd7d7198-647f-8355-8f7b-3c8591a295ec/s);
  generar();
  assert.equal(readFileSync(salida, "utf8"), sql);
});
