// Verifica la imagen candidata y un upgrade generado por el scaffolder en recursos aislados.
import assert from "node:assert/strict";
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import { test } from "node:test";

const repo = new URL("../../", import.meta.url).pathname;
const helpers = readFileSync(join(repo, "infra/scripts/_comun.sh"), "utf8");
const image = (name) => helpers.match(new RegExp(`${name}=\\"\\$\\{${name}:-([^}]+)\\}\\"`))[1];
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function ejecutar(args, opts = {}) {
  const result = spawnSync(args[0], args.slice(1), {
    cwd: repo,
    encoding: "utf8",
    timeout: 600000,
    ...opts,
  });
  assert.equal(
    result.status,
    0,
    `${args[0]} ${args[1]} falló: ${result.stderr?.toString().slice(-4000)}`,
  );
  return result.stdout;
}

test(
  "imagen real: instalación, preview, seed, backup, upgrade, pings, no-op y conservación",
  {
    skip: process.env.INFRA_DOCKER_TESTS !== "1",
    timeout: 900000,
  },
  async () => {
    const uid = randomBytes(6).toString("hex");
    const net = `ars-candidato-${uid}`,
      pg = `ars-candidato-pg-${uid}`,
      s3 = `ars-candidato-s3-${uid}`;
    const app = `ars-candidato-app-${uid}`,
      upgrade = `ars-candidato-upgrade:${uid}`;
    const raiz = join(repo, ".artifacts", `scaffold-${uid}`);
    const env = {
      ...process.env,
      PGHOST: pg,
      PGPORT: "5432",
      PGUSER: "postgres",
      PGPASSWORD: "synthetic-candidate",
      RED_DATOS: net,
      APP_DB_USER: "app_staging",
      APP_DB_PASSWORD: "synthetic-candidate",
      SEAWEEDFS_ROOT_ACCESS_KEY: "root-test",
      SEAWEEDFS_ROOT_SECRET_KEY: "synthetic-candidate",
      SEAWEEDFS_APP_ACCESS_KEY: "app_staging",
      SEAWEEDFS_APP_SECRET_KEY: "synthetic-app",
      BACKUP_VOLUME_PREFIX: `ars-candidato-backup-${uid}`,
      LOCK_PREFIX: `ars-candidato-lock-${uid}`,
      RELEASE_SHA: "a".repeat(40),
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://+:8080",
      Almacenamiento__Endpoint: "seaweedfs-nonprod:8333",
      Almacenamiento__AccessKey: "app_staging",
      Almacenamiento__SecretKey: "synthetic-app",
      Almacenamiento__Bucket: "arsdocendi-staging",
      Almacenamiento__Ambiente: "staging",
      DevelopmentAuthentication__Enabled: "true",
    };
    env.ConnectionStrings__ArsDocendi = `Host=${pg};Database=arsdocendi_staging;Username=app_staging;Password=synthetic-candidate`;
    const docker = (...args) => ejecutar(["docker", ...args], { env });
    const script = (name, ...args) =>
      ejecutar(["bash", join(repo, "infra/scripts", name), ...args], { env });
    const sql = (text) =>
      ejecutar(
        [
          "docker",
          "exec",
          "-i",
          pg,
          "psql",
          "-X",
          "-qtA",
          "-U",
          "postgres",
          "-d",
          "arsdocendi_staging",
          "-v",
          "ON_ERROR_STOP=1",
        ],
        { env, input: text },
      ).trim();
    const cli = (img, ...args) =>
      ejecutar(
        [
          "docker",
          "run",
          "--rm",
          "--network",
          net,
          "-e",
          "ConnectionStrings__ArsDocendi",
          "-e",
          "RELEASE_SHA",
          "-e",
          "ASPNETCORE_ENVIRONMENT",
          img,
          "dotnet",
          "ArsDocendi.Host.dll",
          ...args,
        ],
        { env },
      );
    const aws = (...args) =>
      ejecutar(
        [
          "docker",
          "run",
          "--rm",
          "-i",
          "--network",
          net,
          "-e",
          "AWS_ACCESS_KEY_ID=root-test",
          "-e",
          "AWS_SECRET_ACCESS_KEY=synthetic-candidate",
          "-e",
          "AWS_DEFAULT_REGION=us-east-1",
          image("AWS_CLI_IMAGE"),
          "--endpoint-url",
          "http://seaweedfs-nonprod:8333",
          ...args,
        ],
        { env },
      ).trim();
    const bytes = () =>
      aws(
        "s3",
        "cp",
        "s3://arsdocendi-staging/archivos/seed/f1000000-0000-4000-8000-000000000001",
        "-",
        "--only-show-errors",
      );
    try {
      docker("network", "create", net);
      docker(
        "run",
        "-d",
        "--name",
        pg,
        "--network",
        net,
        "-e",
        "POSTGRES_PASSWORD=synthetic-candidate",
        "postgres:18-alpine",
      );
      docker(
        "run",
        "-d",
        "--name",
        s3,
        "--network",
        net,
        "--network-alias",
        "seaweedfs-nonprod",
        "--entrypoint",
        "/bin/sh",
        image("SEAWEEDFS_IMAGE"),
        "-ec",
        'printf \'%s\' \'{"identities":[{"name":"admin","credentials":[{"accessKey":"root-test","secretKey":"synthetic-candidate"}],"actions":["Admin","Read","List","Tagging","Write"]}]}\' > /tmp/s3.json; exec weed mini -dir=/data -s3 -s3.config=/tmp/s3.json -master.telemetry=false',
      );
      for (let n = 0; n < 60; n++) {
        if (spawnSync("docker", ["exec", pg, "pg_isready", "-U", "postgres"]).status === 0) break;
        await wait(500);
      }
      for (let n = 0; n < 60; n++) {
        const r = spawnSync(
          "docker",
          ["exec", s3, "wget", "-q", "-O", "/dev/null", "http://127.0.0.1:8333"],
          { encoding: "utf8" },
        );
        if (r.status === 0 || r.stderr.includes("403")) break;
        await wait(500);
      }
      script("provision-db.sh", "staging");
      aws("s3api", "create-bucket", "--bucket", "arsdocendi-staging");
      ejecutar(["docker", "exec", "-i", s3, "weed", "shell"], {
        env,
        input:
          "s3.configure -access_key=app_staging -secret_key=synthetic-app -user=app_staging -buckets=arsdocendi-staging -actions=Read,Write,List,Tagging -apply\n",
      });
      assert.equal(
        JSON.parse(cli("arsdocendi-migraciones:verificacion", "--estado-migraciones")).pendientes,
        4,
      );
      assert.equal(
        JSON.parse(cli("arsdocendi-migraciones:verificacion", "--validar-recursos")).compatible,
        true,
      );
      const preview = join(raiz, "preview");
      mkdirSync(preview, { recursive: true });
      const tar = ejecutar(
        [
          "docker",
          "run",
          "--rm",
          "--network",
          net,
          "-e",
          "ConnectionStrings__ArsDocendi",
          "-e",
          "RELEASE_SHA",
          "arsdocendi-migraciones:verificacion",
          "dotnet",
          "ArsDocendi.Host.dll",
          "--script-migraciones",
          "-",
        ],
        { env, encoding: null },
      );
      writeFileSync(join(preview, "preview.tar"), tar);
      ejecutar(["tar", "-xf", join(preview, "preview.tar"), "-C", preview]);
      assert.equal(JSON.parse(readFileSync(join(preview, "manifiesto.json"))).scripts.length, 4);
      assert.equal(sql("SELECT count(*) FROM pg_namespace WHERE nspname='identity'"), "0");
      cli("arsdocendi-migraciones:verificacion", "--migrate");
      script("seed.sh", "staging");
      sql(
        "UPDATE identity.personas SET nombre='Edición posterior' WHERE id='d0000000-0000-4000-8000-000000000001'",
      );
      const original = bytes();
      assert.ok(original.length > 0);
      script("seed.sh", "staging");
      assert.equal(
        sql("SELECT nombre FROM identity.personas WHERE id='d0000000-0000-4000-8000-000000000001'"),
        "Edición posterior",
      );
      const backup = script("backup-storage.sh", "staging").trim();
      script("backup-volume.sh", "staging", "verify", backup);
      // Scaffold y compilación aislados: el repo real no recibe esta migración de prueba.
      for (const file of ["backend", "database", "global.json", ".dockerignore"]) {
        cpSync(join(repo, file), join(raiz, file), {
          recursive: true,
          filter: (path) => !/(?:^|\/)(?:bin|obj)(?:\/|$)/.test(path),
        });
      }
      const generado = JSON.parse(
        ejecutar([
          "node",
          "scripts/migrations/scaffold.mjs",
          "Identity",
          "IndiceVerificacion",
          "--root",
          raiz,
        ]),
      );
      writeFileSync(
        join(raiz, generado.archivos[0].ruta),
        "CREATE INDEX personas_apellido_verificacion ON identity.personas (apellido);\n",
      );
      ejecutar(["docker", "build", "-t", upgrade, "-f", join(raiz, "backend/Dockerfile"), raiz], {
        env,
      });
      assert.equal(JSON.parse(cli(upgrade, "--validar-recursos")).compatible, true);
      assert.equal(JSON.parse(cli(upgrade, "--estado-migraciones")).pendientes, 1);
      cli(upgrade, "--migrate");
      assert.equal(
        sql("SELECT count(*) FROM pg_indexes WHERE indexname='personas_apellido_verificacion'"),
        "1",
      );
      assert.equal(bytes(), original);
      assert.equal(
        sql("SELECT nombre FROM identity.personas WHERE id='d0000000-0000-4000-8000-000000000001'"),
        "Edición posterior",
      );
      cli(upgrade, "--migrate");
      assert.equal(JSON.parse(cli(upgrade, "--estado-migraciones")).pendientes, 0);
      // El HTTP real del candidato arranca sin volver a aplicar migraciones.
      const flags = Object.keys(env)
        .filter(
          (k) =>
            k.startsWith("Almacenamiento__") ||
            k.startsWith("DevelopmentAuthentication__") ||
            k.startsWith("ASPNETCORE_") ||
            k === "ConnectionStrings__ArsDocendi",
        )
        .flatMap((k) => ["-e", k]);
      docker("run", "-d", "--name", app, "--network", net, ...flags, upgrade);
      for (const modulo of ["designaciones", "aulas", "portal", "tareas"]) {
        let respuesta;
        for (let n = 0; n < 30; n++) {
          const r = spawnSync(
            "docker",
            [
              "run",
              "--rm",
              "--network",
              `container:${app}`,
              "--entrypoint",
              "wget",
              "postgres:18-alpine",
              "-q",
              "-O",
              "-",
              `http://127.0.0.1:8080/api/${modulo}/ping`,
            ],
            { encoding: "utf8" },
          );
          if (r.status === 0) {
            respuesta = JSON.parse(r.stdout);
            break;
          }
          await wait(500);
        }
        assert.equal(respuesta?.status, "ok");
      }
      script("backup-volume.sh", "staging", "success", backup);
      console.log(
        "PASS candidato: 4 baselines, tar real, seed único, backup conjunto, scaffold compilado, upgrade incremental, datos/bytes conservados y cuatro pings",
      );
    } finally {
      spawnSync("docker", ["rm", "-fv", app, pg, s3]);
      spawnSync("docker", ["network", "rm", net]);
      spawnSync("docker", ["volume", "rm", `${env.BACKUP_VOLUME_PREFIX}-staging`]);
      spawnSync("docker", ["image", "rm", upgrade]);
      if (existsSync(raiz)) rmSync(raiz, { recursive: true, force: true });
    }
  },
);
