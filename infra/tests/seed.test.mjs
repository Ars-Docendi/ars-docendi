import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { chmodSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
for (const [caso, ambiente, origen] of [
  ["prod", "prod", undefined],
  ["copia de prod hacia staging", "staging", "arsdocendi_prod"],
]) {
  test(`seed rechaza ${caso} antes de invocar Docker`, () => {
    const dir = mkdtempSync(`${tmpdir()}/seed-`);
    const registro = `${dir}/docker.log`;
    try {
      writeFileSync(registro, "");
      writeFileSync(`${dir}/docker`, `#!/bin/sh\necho "$@" >> "${registro}"\n`);
      chmodSync(`${dir}/docker`, 0o755);
      const r = spawnSync("bash", [`${repo}infra/scripts/seed.sh`, ambiente], {
        encoding: "utf8",
        env: { PATH: `${dir}:${process.env.PATH}`, ...(origen ? { SEED_FROM_DB: origen } : {}) },
      });
      assert.notEqual(r.status, 0);
      assert.match(r.stderr, /PROHIBIDO/);
      assert.equal(readFileSync(registro, "utf8"), "");
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
}
