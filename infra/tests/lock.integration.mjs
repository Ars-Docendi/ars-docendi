// Exclusión real entre procesos con workspaces separados, sobre el mismo daemon.
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const terminar = (p) => new Promise((resolve) => p.on("exit", resolve));
test(
  "lock Docker serializa deploy y teardown manual entre runners",
  { skip: process.env.INFRA_DOCKER_TESTS !== "1", timeout: 30000 },
  async () => {
    const dir = mkdtempSync(join(tmpdir(), "infra-lock-"));
    const env = {
      PATH: process.env.PATH,
      LOCK_PREFIX: `ars-infra-lock-${randomBytes(6).toString("hex")}`,
      LOG: join(dir, "orden"),
    };
    const body =
      'source infra/scripts/_comun.sh; adquirir_lock_ambiente pr-987654; trap liberar_lock_ambiente EXIT; printf "%s-start\\n" "$ROL" >> "$LOG"; sleep "$ESPERA"; printf "%s-end\\n" "$ROL" >> "$LOG"';
    const a = spawn("bash", ["-c", body], {
      cwd: repo,
      env: { ...env, ROL: "deploy", ESPERA: "2" },
      stdio: ["ignore", "ignore", "pipe"],
    });
    const pa = terminar(a);
    try {
      for (let i = 0; i < 100; i++) {
        try {
          if (readFileSync(env.LOG, "utf8").includes("deploy-start")) break;
        } catch {}
        await wait(100);
      }
      const b = spawn("bash", ["-c", body], {
        cwd: repo,
        env: { ...env, ROL: "teardown", ESPERA: "0" },
        stdio: ["ignore", "ignore", "pipe"],
      });
      const pb = terminar(b);
      assert.equal(await pa, 0);
      assert.equal(await pb, 0);
      assert.equal(
        readFileSync(env.LOG, "utf8"),
        "deploy-start\ndeploy-end\nteardown-start\nteardown-end\n",
      );
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  },
);
