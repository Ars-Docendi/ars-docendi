import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, writeFileSync, rmSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
for (const fallo of ["", "imagen", "ping", "db"]) {
  test(`smoke ${fallo || "correcto"}: SHA + cuatro pings + identidad DB`, () => {
    assert.ok(existsSync(join(repo, "infra/scripts/smoke.sh")), "falta smoke verificable");
    const dir = mkdtempSync(join(tmpdir(), "smoke-"));
    mkdirSync(join(dir, "bin"));
    writeFileSync(
      join(dir, "bin/docker"),
      `#!/bin/bash
case "$*" in
  *'ps -q'*) [[ "$*" = *frontend* ]] && echo frontend-candidato || echo backend-candidato ;;
  *'inspect --format'*) if [[ "$FAIL" = imagen ]]; then echo old; else [[ "$*" = *frontend* ]] && echo "$REGISTRO/arsdocendi-frontend:$TAG_FRONTEND" || echo "$REGISTRO/arsdocendi-backend:$TAG_BACKEND"; fi ;;
  *wget*) [[ "$FAIL" != ping ]] || exit 1; for m in designaciones aulas portal tareas; do [[ "$*" != *"/api/$m/ping"* ]] || echo "{\\\"module\\\":\\\"$m\\\",\\\"status\\\":\\\"ok\\\"}"; done ;;
  *psql*) [[ "$FAIL" != db ]] && echo 'arsdocendi_staging:app_staging:ok' || echo incorrecta ;;
esac
`,
      { mode: 0o755 },
    );
    const r = spawnSync("bash", [join(repo, "infra/scripts/smoke.sh"), "staging"], {
      encoding: "utf8",
      env: {
        PATH: join(dir, "bin") + ":" + process.env.PATH,
        FAIL: fallo,
        REGISTRO: "test",
        TAG_FRONTEND: "sha-test",
        TAG_BACKEND: "sha-test",
        APP_DB_USER: "app_staging",
        APP_DB_PASSWORD: "synthetic",
        PGHOST: "synthetic",
        PGUSER: "synthetic",
        PGPASSWORD: "synthetic",
        SMOKE_ATTEMPTS: "1",
      },
    });
    rmSync(dir, { recursive: true });
    assert.equal(r.status === 0, fallo === "", r.stderr);
  });
}
