import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { test } from "node:test";

const repo = new URL("../../", import.meta.url).pathname;
const consultar = (ambiente) =>
  spawnSync(
    "bash",
    [
      "-c",
      'source infra/scripts/_comun.sh; hostname_publico "$1" "$2"',
      "prueba",
      ambiente,
      "example.net",
    ],
    { cwd: repo, encoding: "utf8", env: { PATH: process.env.PATH } },
  );

for (const ambiente of ["staging", "pr-123"]) {
  test(`${ambiente} conserva su subdominio`, () => {
    const resultado = consultar(ambiente);
    assert.equal(resultado.status, 0, resultado.stderr);
    assert.equal(resultado.stdout, `${ambiente}.example.net`);
  });
}

test("identificador inválido no produce un hostname público", () => {
  const resultado = consultar("preview-arbitrario");
  assert.notEqual(resultado.status, 0);
  assert.equal(resultado.stdout, "");
});

test("producción usa dominio raíz y conserva identidades internas", () => {
  const resultado = consultar("prod");
  assert.equal(resultado.status, 0, resultado.stderr);
  assert.equal(resultado.stdout, "example.net");
  const identidades = spawnSync(
    "bash",
    [
      "-c",
      'source infra/scripts/_comun.sh; printf "%s\\n" "$(nombre_base prod)" "$(storage_project_for prod)" "$(seaweedfs_app_access_for prod)"',
    ],
    { cwd: repo, encoding: "utf8", env: { PATH: process.env.PATH } },
  );
  assert.equal(identidades.status, 0, identidades.stderr);
  assert.equal(identidades.stdout, "arsdocendi_prod\narsdocendi-storage-prod\napp_prod\n");
});
