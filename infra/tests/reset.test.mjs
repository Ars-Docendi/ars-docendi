import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
for (const ambiente of ["prod", "staging", "pr-123"]) {
  test(`reset ${ambiente}: exige autorización no productiva antes de Docker`, () => {
    assert.ok(existsSync(`${repo}infra/scripts/reset.sh`), "falta reset manual protegido");
    const r = spawnSync("bash", [`${repo}infra/scripts/reset.sh`, ambiente], {
      encoding: "utf8",
      env: { PATH: process.env.PATH, RESET_AUTHORIZED: "" },
    });
    assert.notEqual(r.status, 0);
    assert.match(r.stderr, ambiente === "prod" ? /PROHIBIDA/ : /RESET_AUTHORIZED/);
  });
}
