import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

test("ingress Debian publica exclusivamente el dominio raíz", () => {
  const config = leer("infra/cloudflared/config-principal.yml");
  assert.match(config, /hostname: example\.net\n\s+service: http:\/\/traefik:80/);
  assert.equal((config.match(/hostname:/g) ?? []).length, 1);
  assert.match(config, /service: http_status:404/);
});

test("ingress Proxmox rechaza prod antes del wildcard y conserva staging", () => {
  const config = leer("infra/cloudflared/config.yml");
  assert.match(config, /hostname: prod\.example\.net\n\s+service: http_status:404/);
  assert.match(config, /hostname: staging\.example\.net\n\s+service: http:\/\/traefik:80/);
  assert.ok(
    config.indexOf("hostname: prod.example.net") < config.indexOf('hostname: "*.example.net"'),
  );
  assert.match(config, /hostname: "\*\.example\.net"\n\s+service: http:\/\/traefik:80/);
});

const leer = (ruta) => readFileSync(new URL(`../../${ruta}`, import.meta.url), "utf8");

for (const [ambiente, destino, rama, buildArg, claveApp] of [
  ["prod", "principal", "main", "false", "APP_DB_PASSWORD_PROD"],
  ["staging", "secundaria", "develop", "true", "APP_DB_PASSWORD_STAGING"],
]) {
  test(`${ambiente}: construcción por SHA y destino exclusivo ${destino}`, () => {
    const workflow = leer(`.github/workflows/deploy-${ambiente}.yml`);
    const [cabecera, jobs] = workflow.split("jobs:\n");
    const [build, deploy] = jobs.split("  deploy:\n");
    assert.match(cabecera, new RegExp(`branches: \\[${rama}\\]`));
    for (const filtro of [
      "!backend/**/*.md",
      "!frontend/**/*.md",
      "!infra/**/*.md",
      "!database/**/*.md",
    ]) {
      assert.ok(cabecera.includes(filtro));
    }
    assert.equal((build.match(/docker build /g) ?? []).length, 2);
    assert.equal((build.match(/docker push /g) ?? []).length, 2);
    assert.ok(build.includes(`VITE_DEVELOPMENT_AUTH_ENABLED=${buildArg}`));
    assert.match(
      deploy,
      new RegExp(`runs-on: \\[self-hosted, arsdocendi, confiable, ${destino}\\]`),
    );
    assert.doesNotMatch(workflow, /matrix:|matrix\.destino|fail-fast:/);
    assert.match(deploy, new RegExp(`environment: ${ambiente}`));
    assert.ok(deploy.includes(`secrets.${claveApp}`));
    for (const secret of [
      "PGHOST",
      "PGPORT",
      "PGUSER",
      "PGPASSWORD",
      "SEAWEEDFS_ROOT_ACCESS_KEY",
      "SEAWEEDFS_ROOT_SECRET_KEY",
    ]) {
      assert.ok(deploy.includes(`secrets.${secret}`));
    }
    assert.match(deploy, /packages: read/);
    assert.match(build, /packages: write/);
    assert.doesNotMatch(deploy, /docker build |docker push /);
    assert.equal((deploy.match(/infra\/scripts\/spin-up\.sh /g) ?? []).length, 1);
    assert.match(deploy, /esperado=.*sha-\$\{GITHUB_SHA\}/);
  });
}

test("previews y teardown permanecen en Proxmox con ambos gates", () => {
  const deploy = leer(".github/workflows/pr-env-deploy.yml");
  const teardown = leer(".github/workflows/pr-env-teardown.yml");
  assert.match(deploy, /runs-on: \[self-hosted, arsdocendi, efimero, secundaria\]/);
  assert.match(deploy, /needs: gate/);
  assert.match(deploy, /environment: pr-preview/);
  assert.match(
    deploy,
    /if: contains\(github\.event\.pull_request\.labels\.\*\.name, 'deploy-preview'\)/,
  );
  assert.match(teardown, /runs-on: \[self-hosted, arsdocendi, confiable, secundaria\]/);
  assert.match(teardown, /ref: \$\{\{ github\.event\.pull_request\.base\.sha \}\}/);
  assert.match(teardown, /infra\/scripts\/teardown\.sh/);
  assert.doesNotMatch(deploy, /^  pull_request_target:/m);
});
