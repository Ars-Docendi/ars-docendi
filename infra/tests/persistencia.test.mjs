import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
test("SeaweedFS limita el pool no-prod sin alterar la capacidad de prod", () => {
  for (const [ambiente, esperado] of [
    ["pr-39", "8:128"],
    ["prod", "0:1024"],
  ]) {
    const proceso = spawnSync(
      "bash",
      [
        "-c",
        `source infra/scripts/_comun.sh
configurar_capacidad_seaweedfs "$1"
printf '%s:%s' "$SEAWEEDFS_VOLUME_MAX" "$SEAWEEDFS_VOLUME_SIZE_LIMIT_MB"`,
        "bash",
        ambiente,
      ],
      {
        cwd: repo,
        encoding: "utf8",
        env: { PATH: process.env.PATH },
      },
    );
    assert.equal(proceso.status, 0, proceso.stderr);
    assert.equal(proceso.stdout, esperado);
  }
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
