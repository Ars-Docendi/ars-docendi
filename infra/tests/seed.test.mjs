import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
for (const [caso, ambiente, origen] of [
  ["prod", "prod", undefined],
  ["copia de prod hacia staging", "staging", "arsdocendi_prod"],
]) {
  test(`seed rechaza ${caso} antes de invocar Docker`, () => {
    const r = spawnSync("bash", [`${repo}infra/scripts/seed.sh`, ambiente], {
      encoding: "utf8",
      env: { PATH: process.env.PATH, ...(origen ? { SEED_FROM_DB: origen } : {}) },
    });
    assert.notEqual(r.status, 0);
    assert.match(r.stderr, /PROHIBIDO/);
  });
}
