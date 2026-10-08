// Prueba el orquestador real con fronteras externas sintéticas.
import assert from "node:assert/strict";
import { copyFileSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
function ejecutar(ambiente, opciones = {}) {
  const dir = mkdtempSync(join(repo, ".tmp-spin-up-"));
  const scripts = join(dir, "infra/scripts");
  mkdirSync(scripts, { recursive: true });
  mkdirSync(join(dir, "infra/compose"), { recursive: true });
  mkdirSync(join(dir, "bin"));
  for (const n of ["spin-up.sh", "_comun.sh", "estado-migraciones.py"]) {
    try {
      copyFileSync(join(repo, "infra/scripts", n), join(scripts, n));
    } catch {}
  }
  copyFileSync(
    join(repo, "infra/compose/compose.base.yml"),
    join(dir, "infra/compose/compose.base.yml"),
  );
  for (const n of [
    "drop-db.sh",
    "purge-storage.sh",
    "provision-storage.sh",
    "provision-db.sh",
    "seed.sh",
    "backup-storage.sh",
    "backup-volume.sh",
    "smoke.sh",
  ]) {
    writeFileSync(
      join(scripts, n),
      `#!/bin/bash\nprintf '%s\\n' '${n}' >> "$TEST_LOG"\n[[ "$FAIL_STEP" != '${n}' ]] || exit 42\n[[ '${n}' != backup-storage.sh ]] || echo snapshot-test\n[[ '${n}' != backup-volume.sh || \"$2\" != receipt && \"$2\" != preview ]] || cat > /dev/null\n`,
      { mode: 0o755 },
    );
  }
  writeFileSync(
    join(dir, "bin/docker"),
    `#!/bin/bash
set -eu
printf 'docker %s\\n' "$*" >> "$TEST_LOG"
if [[ "$*" == *--estado-migraciones* ]]; then
  count="$PENDING"
  [[ ! -f "$TEST_APPLIED" ]] || count=0
  printf '{"baseDatos":"arsdocendi_%s","contextos":[{"contexto":"Identity","disponibles":["baseline"],"aplicadas":%s,"pendientes":%s}],"pendientes":%s,"compatible":true}\\n' "$BASE_SUFFIX" "$([[ "$count" = 0 ]] && echo '["baseline"]' || echo '[]')" "$([[ "$count" = 0 ]] && echo '[]' || echo '["baseline"]')" "$count" | tee "$TEST_OBSERVED"
elif [[ "$*" == *--script-migraciones* ]]; then
  d=$(mktemp -d)
  python3 - "$TEST_OBSERVED" "$d/manifiesto.json" <<'PY'
import json,os,sys
with open(sys.argv[1]) as f: x=json.load(f)
x.update(formato='arsdocendi-migraciones/v1', sha='incorrecto' if os.environ['BAD_PREVIEW']=='true' else os.environ['RELEASE_SHA'], scripts=[])
with open(sys.argv[2],'w') as f:json.dump(x,f)
PY
  tar -C "$d" -cf - .; rm -rf "$d"
elif [[ "$*" == *--migrate* ]]; then
  [[ "$FAIL_STEP" != migrate ]] || exit 42
  touch "$TEST_APPLIED"
elif [[ "$*" == *'SELECT 1 FROM pg_database'* ]]; then
  [[ "$NEW_DB" = true ]] || echo 1
fi
`,
    { mode: 0o755 },
  );
  writeFileSync(join(dir, "bin/curl"), `#!/bin/bash\nprintf '{"state":"%s"}\\n' "$PR_STATE"\n`, {
    mode: 0o755,
  });
  const result = spawnSync("bash", [join(scripts, "spin-up.sh"), ambiente], {
    cwd: dir,
    encoding: "utf8",
    env: {
      PATH: `${join(dir, "bin")}:${process.env.PATH}`,
      TMPDIR: dir,
      DOMINIO: "example.net",
      REGISTRO: "ghcr.io/prueba",
      TAG_FRONTEND: "sha-0123456789012345678901234567890123456789",
      TAG_BACKEND: "sha-0123456789012345678901234567890123456789",
      PGHOST: "postgres-test",
      PGUSER: "test",
      PGPASSWORD: "synthetic",
      APP_DB_USER: `app_${ambiente.replaceAll("-", "_")}`,
      APP_DB_PASSWORD: "synthetic",
      SEAWEEDFS_ROOT_ACCESS_KEY: "synthetic",
      SEAWEEDFS_ROOT_SECRET_KEY: "synthetic",
      SEAWEEDFS_APP_ACCESS_KEY: "synthetic",
      SEAWEEDFS_APP_SECRET_KEY: "synthetic",
      ...(opciones.prState
        ? {
            GITHUB_API_URL: "https://api.sintetica.invalid",
            GITHUB_REPOSITORY: "synthetic/repo",
            GH_TOKEN: "synthetic",
            PR_STATE: opciones.prState,
          }
        : {}),
      TEST_OBSERVED: join(dir, "observed.json"),
      BAD_PREVIEW: String(opciones.badPreview ?? false),
      TEST_LOG: join(dir, "log"),
      TEST_APPLIED: join(dir, "applied"),
      PENDING: String(opciones.pending ?? 1),
      NEW_DB: String(opciones.newDb ?? false),
      FAIL_STEP: opciones.fail ?? "",
      BASE_SUFFIX: ambiente.replaceAll("-", "_"),
    },
  });
  const log = readFileSync(join(dir, "log"), "utf8");
  rmSync(dir, { recursive: true, force: true });
  return { ...result, log };
}
for (const ambiente of ["prod", "staging", "pr-123"]) {
  test(`${ambiente}: conserva datos y hace backup antes de migrar`, () => {
    const r = ejecutar(ambiente);
    assert.equal(r.status, 0, r.stderr);
    assert.doesNotMatch(r.log, /drop-db|purge-storage|down -v/);
    assert.ok(r.log.indexOf("stop backend") < r.log.indexOf("backup-storage.sh"));
    assert.ok(r.log.indexOf("backup-storage.sh") < r.log.indexOf("--migrate"));
    assert.ok(r.log.indexOf("--script-migraciones") < r.log.indexOf("--migrate"));
    assert.ok(r.log.includes("smoke.sh"));
    if (ambiente === "prod") assert.doesNotMatch(r.log, /seed.sh/);
  });
}
test("PR cerrado bajo lock no provisiona ni publica", () => {
  const r = ejecutar("pr-123", { prState: "closed" });
  assert.notEqual(r.status, 0);
  assert.doesNotMatch(r.log, /provision-db|provision-storage|--migrate|up -d/);
  assert.match(r.stderr, /PR cerrado/);
});
test("preview de otra release bloquea antes de mantenimiento", () => {
  const r = ejecutar("staging", { badPreview: true });
  assert.notEqual(r.status, 0);
  assert.doesNotMatch(r.log, /--migrate|stop backend|up -d/);
});
test("sin pendientes omite backup y migrate", () => {
  const r = ejecutar("staging", { pending: 0 });
  assert.equal(r.status, 0, r.stderr);
  assert.doesNotMatch(r.log, /backup-storage.sh|--migrate/);
});
test("base nueva no inventa backup", () => {
  const r = ejecutar("staging", { newDb: true });
  assert.equal(r.status, 0, r.stderr);
  assert.doesNotMatch(r.log, /backup-storage.sh/);
  assert.match(r.log, /--migrate/);
});
for (const fail of ["backup-storage.sh", "migrate", "smoke.sh"]) {
  test(`falla ${fail}: sin recibo exitoso y backend detenido`, () => {
    const r = ejecutar("staging", { fail });
    assert.notEqual(r.status, 0);
    assert.doesNotMatch(r.stderr, /spin-up OK/);
    assert.match(r.log, /stop backend/);
    if (fail === "backup-storage.sh") assert.doesNotMatch(r.log, /--migrate|up -d/);
    if (fail === "migrate") assert.doesNotMatch(r.log, /up -d/);
  });
}
