import assert from "node:assert/strict";
import { readFileSync, mkdtempSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
const leer = (p) => readFileSync(join(repo, p), "utf8");
test("seed transmite stdin, exige bootstrap y marca dentro de transacción", () => {
  const sh = leer("infra/scripts/seed.sh");
  assert.doesNotMatch(sh, /-v .*:\/seed.sql/);
  assert.match(sh, /bootstrap_metadata/);
  assert.match(sh, /inicializacion_completada/);
  assert.match(sh, /seed-transaccion.py/);
});
test("deploy y teardown comparten lock del daemon independiente de TMPDIR", () => {
  for (const s of ["spin-up", "teardown"])
    assert.match(leer(`infra/scripts/${s}.sh`), /adquirir_lock_ambiente/);
  assert.match(leer("infra/scripts/_comun.sh"), /docker create --name "\$nombre"/);
});
test("preview PR no cancela y verifica abierto antes de publicar", () => {
  assert.match(leer(".github/workflows/pr-env-deploy.yml"), /cancel-in-progress: false/);
  assert.match(leer("infra/scripts/spin-up.sh"), /pulls\/\$\{ambiente#pr-\}/);
});
test("restore nunca droppea destino poblado y requiere autorización", () => {
  const s = leer("infra/scripts/restore-storage.sh");
  assert.doesNotMatch(s, /\/drop-db.sh/);
  assert.match(s, /RECOVERY_AUTHORIZED/);
  assert.match(s, /destino.*poblad/);
});
test("restore verifica content type y metadata además de bytes", () => {
  assert.match(leer("infra/scripts/restore-storage.sh"), /metadata restaurada no coincide/);
});
test("backup default es volumen con streams, no bind del runner", () => {
  const s = leer("infra/scripts/backup-storage.sh");
  assert.match(s, /backup-volume/);
  assert.match(s, /complete/);
});
test("setup local usa marca completa y transacción conjunta, no mera existencia de tabla", () => {
  const s = leer("scripts/setup.sh");
  assert.doesNotMatch(s, /SELECT to_regclass\('public.seed_metadata'\) IS NOT NULL/);
  assert.match(s, /seed-local.sh/);
});
test("estado rechaza destino incorrecto e inventario discontinuo", () => {
  const dir = mkdtempSync(join(tmpdir(), "estado-test-"));
  try {
    const path = join(dir, "estado.json");
    for (const e of [
      { baseDatos: "otra", compatible: true, pendientes: 0, contextos: [] },
      {
        baseDatos: "arsdocendi_staging",
        compatible: true,
        pendientes: 0,
        contextos: [
          { contexto: "Identity", disponibles: ["a", "b"], aplicadas: ["b"], pendientes: [] },
        ],
      },
    ]) {
      writeFileSync(path, JSON.stringify(e));
      assert.notEqual(
        spawnSync("python3", [
          join(repo, "infra/scripts/estado-migraciones.py"),
          path,
          "arsdocendi_staging",
        ]).status,
        0,
      );
    }
  } finally {
    rmSync(dir, { recursive: true });
  }
});
