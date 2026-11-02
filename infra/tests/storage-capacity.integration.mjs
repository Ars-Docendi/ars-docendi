// Ensayo aislado del límite de volúmenes de SeaweedFS compartido por varios PR.
import assert from "node:assert/strict";
import { randomBytes } from "node:crypto";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

const repo = new URL("../../", import.meta.url).pathname;
const composeFile = join(repo, "infra/compose/compose.storage.yml");
const comun = readFileSync(join(repo, "infra/scripts/_comun.sh"), "utf8");
const imagen = (nombre) => comun.match(new RegExp(`${nombre}="\\$\\{${nombre}:-([^}]+)\\}"`))[1];
const ejecutar = (args, opciones = {}) => {
  const r = spawnSync(args[0], args.slice(1), {
    cwd: repo,
    encoding: "utf8",
    timeout: 120000,
    ...opciones,
  });
  assert.equal(r.status, 0, `${args.slice(0, 4).join(" ")}: ${r.stderr}`);
  return r.stdout.trim();
};

test(
  "SeaweedFS compartido permite escribir en cinco buckets aislados",
  {
    skip: process.env.INFRA_DOCKER_TESTS !== "1",
    timeout: 180000,
  },
  async () => {
    const id = randomBytes(6).toString("hex");
    const red = `ars-capacidad-${id}`;
    const proyecto = `ars-capacidad-${id}`;
    const directorio = mkdtempSync(join(tmpdir(), "ars-capacidad-"));
    const envFile = join(directorio, "storage.env");
    const configuracion = [
      `RED_DATOS=${red}`,
      `STORAGE_SCOPE=capacidad_${id}`,
      "SEAWEEDFS_HOSTNAME=seaweedfs-capacidad",
      `SEAWEEDFS_IMAGE=${imagen("SEAWEEDFS_IMAGE")}`,
      "SEAWEEDFS_ROOT_ACCESS_KEY=root-test",
      "SEAWEEDFS_ROOT_SECRET_KEY=synthetic-disposable",
    ];
    const escribirConfiguracion = (maximo, tamano) =>
      writeFileSync(
        envFile,
        [
          ...configuracion,
          `SEAWEEDFS_VOLUME_MAX=${maximo}`,
          `SEAWEEDFS_VOLUME_SIZE_LIMIT_MB=${tamano}`,
        ].join("\n") + "\n",
        { mode: 0o600 },
      );
    escribirConfiguracion(4, 1024);
    const compose = (...args) =>
      ejecutar([
        "docker",
        "compose",
        "-p",
        proyecto,
        "--env-file",
        envFile,
        "-f",
        composeFile,
        ...args,
      ]);
    const aws = (...args) =>
      ejecutar(
        [
          "docker",
          "run",
          "--rm",
          "-i",
          "--network",
          red,
          "-e",
          "AWS_ACCESS_KEY_ID=root-test",
          "-e",
          "AWS_SECRET_ACCESS_KEY=synthetic-disposable",
          "-e",
          "AWS_DEFAULT_REGION=us-east-1",
          imagen("AWS_CLI_IMAGE"),
          "--endpoint-url",
          "http://seaweedfs-capacidad:8333",
          ...args,
        ],
        { input: "fixture sintética" },
      );
    const esperarSalud = async () => {
      const contenedor = compose("ps", "-q", "seaweedfs");
      assert.ok(contenedor);
      let estado = "";
      for (let intento = 0; intento < 60; intento++) {
        estado = ejecutar([
          "docker",
          "inspect",
          "--format",
          "{{.State.Health.Status}}",
          contenedor,
        ]);
        if (estado === "healthy") break;
        await new Promise((resolver) => setTimeout(resolver, 500));
      }
      assert.equal(estado, "healthy");
      return contenedor;
    };
    try {
      ejecutar(["docker", "network", "create", red]);
      compose("up", "-d", "seaweedfs");
      const anterior = await esperarSalud();
      for (let n = 1; n <= 4; n++) {
        const bucket = `capacidad-${id}-${n}`;
        aws("s3api", "create-bucket", "--bucket", bucket);
        aws("s3", "cp", "-", `s3://${bucket}/fixture`, "--only-show-errors");
      }
      escribirConfiguracion(8, 128);
      compose("up", "-d", "seaweedfs");
      const nuevo = await esperarSalud();
      assert.notEqual(nuevo, anterior, "la nueva configuración debe recrear el servicio");
      assert.equal(
        aws("s3", "cp", `s3://capacidad-${id}-1/fixture`, "-", "--only-show-errors"),
        "fixture sintética",
      );
      const bucket = `capacidad-${id}-5`;
      aws("s3api", "create-bucket", "--bucket", bucket);
      aws("s3", "cp", "-", `s3://${bucket}/fixture`, "--only-show-errors");
    } finally {
      spawnSync(
        "docker",
        [
          "compose",
          "-p",
          proyecto,
          "--env-file",
          envFile,
          "-f",
          composeFile,
          "down",
          "-v",
          "--remove-orphans",
        ],
        { cwd: repo },
      );
      spawnSync("docker", ["network", "rm", red]);
      rmSync(directorio, { recursive: true, force: true });
    }
  },
);
