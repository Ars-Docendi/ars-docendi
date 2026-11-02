import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
for (const [valor, valido] of [
  ["", true],
  ["1", true],
  ["7", true],
  ["100000", true],
  ["999999999999999999999999", true],
  ["0", false],
  ["-1", false],
  ["1.5", false],
  ["abc", false],
]) {
  test(`retención ${valor || "default"}: entero positivo`, () => {
    const r = spawnSync(
      "bash",
      ["-c", "source infra/scripts/_comun.sh; exigir_configuracion_backups"],
      {
        cwd: repo,
        env: { PATH: process.env.PATH, BACKUP_RETENTION_DAYS: valor },
        encoding: "utf8",
      },
    );
    assert.equal(r.status === 0, valido, r.stderr);
  });
}
