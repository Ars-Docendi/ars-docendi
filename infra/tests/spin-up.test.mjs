// Ejecuta el spin-up real con dependencias simuladas: nunca toca Docker ni DB reales.
import assert from "node:assert/strict";
import { copyFileSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { test } from "node:test";

const repo = new URL("../../", import.meta.url).pathname;

for (const [ambiente, hostname] of [
  ["prod", "example.net"],
  ["staging", "staging.example.net"],
  ["pr-123", "pr-123.example.net"],
]) {
  test(`spin-up ${ambiente}: env de Compose e identidad correctos, sin servicios reales`, () => {
    const dir = mkdtempSync(join(repo, ".tmp-spin-up-"));
    try {
      const scripts = join(dir, "infra/scripts");
      mkdirSync(scripts, { recursive: true });
      mkdirSync(join(dir, "infra/compose"), { recursive: true });
      mkdirSync(join(dir, "bin"));
      for (const nombre of ["spin-up.sh", "_comun.sh"]) {
        copyFileSync(join(repo, "infra/scripts", nombre), join(scripts, nombre));
      }
      copyFileSync(
        join(repo, "infra/compose/compose.base.yml"),
        join(dir, "infra/compose/compose.base.yml"),
      );
      const log = join(dir, "dependencias.log");
      for (const nombre of [
        "drop-db.sh",
        "purge-storage.sh",
        "provision-storage.sh",
        "provision-db.sh",
        "seed.sh",
      ]) {
        writeFileSync(
          join(scripts, nombre),
          '#!/bin/bash\nprintf "%s\\n" "$(basename "$0") $*" >> "$TEST_LOG"\n',
          { mode: 0o755 },
        );
      }
      writeFileSync(
        join(dir, "bin/docker"),
        '#!/bin/bash\nset -eu\nprintf "docker %s\\n" "$*" >> "$TEST_LOG"\nwhile (( $# )); do\n  if [[ "$1" == "--env-file" ]]; then\n    cp -- "$2" "$TEST_ENV"\n    printf "%s" "$URL_BASE_DATOS" > "$TEST_CONN"\n  fi\n  shift\ndone\n',
        { mode: 0o755 },
      );
      const resultado = spawnSync("bash", [join(scripts, "spin-up.sh"), ambiente], {
        cwd: dir,
        encoding: "utf8",
        env: {
          PATH: `${join(dir, "bin")}:${process.env.PATH}`,
          TMPDIR: dir,
          DOMINIO: "example.net",
          REGISTRO: "ghcr.io/prueba",
          TAG_FRONTEND: "sha-test",
          TAG_BACKEND: "sha-test",
          PGHOST: "arsdocendi-postgres",
          PGUSER: "postgres",
          PGPASSWORD: "credencial-sintetica-no-real",
          APP_DB_USER: "app_test",
          APP_DB_PASSWORD: "credencial-sintetica-no-real",
          SEAWEEDFS_ROOT_ACCESS_KEY: "prueba",
          SEAWEEDFS_ROOT_SECRET_KEY: "credencial-sintetica-no-real",
          SEAWEEDFS_APP_ACCESS_KEY: "prueba_app",
          SEAWEEDFS_APP_SECRET_KEY: "credencial-sintetica-no-real",
          TEST_LOG: log,
          TEST_ENV: join(dir, "captura.env"),
          TEST_CONN: join(dir, "captura-conn"),
        },
      });
      assert.equal(resultado.status, 0, resultado.stderr);
      const env = readFileSync(join(dir, "captura.env"), "utf8");
      const operaciones = readFileSync(log, "utf8");
      assert.ok(env.includes(`HOST_PUBLICO=${hostname}\n`));
      assert.ok(env.includes(`AMBIENTE=${ambiente}\n`));
      assert.ok(env.includes(`ALMACENAMIENTO_BUCKET=arsdocendi-${ambiente}\n`));
      assert.ok(operaciones.includes(`docker compose -p ${ambiente} `));
      assert.ok(
        readFileSync(join(dir, "captura-conn"), "utf8").includes(
          `Database="arsdocendi_${ambiente.replaceAll("-", "_")}"`,
        ),
      );
      if (ambiente === "prod") {
        assert.doesNotMatch(operaciones, /drop-db\.sh|purge-storage\.sh|seed\.sh|down -v/);
      } else {
        assert.ok(operaciones.includes(`seed.sh ${ambiente}`));
        assert.ok(operaciones.includes(`drop-db.sh ${ambiente}`));
      }
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
}
