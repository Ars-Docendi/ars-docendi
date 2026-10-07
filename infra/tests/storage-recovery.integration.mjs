// Ensayo real aislado: PostgreSQL 18 + SeaweedFS + volumen privado. Sin binds.
import assert from "node:assert/strict";
import { readFileSync, mkdtempSync, writeFileSync, rmSync, chmodSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import { test } from "node:test";
const repo = new URL("../../", import.meta.url).pathname;
const comun = readFileSync(join(repo, "infra/scripts/_comun.sh"), "utf8");
const image = (name) => comun.match(new RegExp(`${name}=\"\\$\\{${name}:-([^}]+)\\}\"`))[1];
const pgImage = "postgres:18-alpine",
  awsImage = image("AWS_CLI_IMAGE"),
  s3Image = image("SEAWEEDFS_IMAGE");
function cmd(args, options = {}) {
  const r = spawnSync(args[0], args.slice(1), {
    cwd: repo,
    encoding: "utf8",
    timeout: 180000,
    ...options,
  });
  assert.equal(r.status, 0, `${args[0]} ${args[1]}: ${r.stderr}`);
  return r.stdout.trim();
}
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
test(
  "backup/recovery real: filas, objetos, hashes, metadata, persistencia, retención y seed único",
  { skip: process.env.INFRA_DOCKER_TESTS !== "1", timeout: 420000 },
  async () => {
    const uid = randomBytes(6).toString("hex"),
      net = `ars-infra-test-${uid}`,
      pg = `ars-infra-pg-${uid}`,
      s3 = `ars-infra-s3-${uid}`;
    const prefix = `ars-infra-backups-${uid}`,
      volume = `${prefix}-staging`;
    const dir = mkdtempSync(join(tmpdir(), "ars-infra-test-"));
    const env = {
      PATH: process.env.PATH,
      PGHOST: pg,
      PGPORT: "5432",
      PGUSER: "postgres",
      PGPASSWORD: "synthetic-disposable",
      RED_DATOS: net,
      APP_DB_USER: "app_staging",
      APP_DB_PASSWORD: "synthetic-disposable",
      SEAWEEDFS_ROOT_ACCESS_KEY: "root-test",
      SEAWEEDFS_ROOT_SECRET_KEY: "synthetic-disposable",
      BACKUP_VOLUME_PREFIX: prefix,
      BACKUP_RETENTION_DAYS: "7",
      LOCK_PREFIX: `ars-infra-lock-${uid}`,
    };
    const docker = (...a) => cmd(["docker", ...a]);
    const script = (name, ...a) => cmd(["bash", join(repo, "infra/scripts", name), ...a], { env });
    const sql = (base, text) =>
      cmd(
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
          base,
          "-v",
          "ON_ERROR_STOP=1",
          "--set=ambiente=staging",
        ],
        { input: text },
      );
    const aws = (...args) =>
      cmd([
        "docker",
        "run",
        "--rm",
        "-i",
        "--network",
        net,
        "-e",
        "AWS_ACCESS_KEY_ID=root-test",
        "-e",
        "AWS_SECRET_ACCESS_KEY=synthetic-disposable",
        "-e",
        "AWS_DEFAULT_REGION=us-east-1",
        awsImage,
        "--endpoint-url",
        `http://${s3}:8333`,
        ...args,
      ]);
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
        "POSTGRES_PASSWORD=synthetic-disposable",
        pgImage,
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
        s3Image,
        "-ec",
        'printf \'%s\' \'{"identities":[{"name":"admin","credentials":[{"accessKey":"root-test","secretKey":"synthetic-disposable"}],"actions":["Admin","Read","List","Tagging","Write"]}]}\' > /tmp/s3.json; exec weed mini -dir=/data -s3 -s3.config=/tmp/s3.json -master.telemetry=false',
      );
      for (let i = 0; i < 60; i++) {
        const ready = spawnSync("docker", ["exec", pg, "pg_isready", "-U", "postgres"], {
          encoding: "utf8",
        });
        if (ready.status === 0) break;
        await wait(500);
      }
      for (let i = 0; i < 60; i++) {
        const ready = spawnSync(
          "docker",
          ["exec", s3, "wget", "-q", "-O", "/dev/null", "http://127.0.0.1:8333"],
          { encoding: "utf8" },
        );
        // Una respuesta HTTP incluso 403 prueba que S3 está escuchando.
        if (ready.status === 0 || ready.stderr.includes("403")) break;
        await wait(500);
      }
      assert.equal(
        sql("postgres", "SELECT current_database() || ':' || current_user;"),
        "postgres:postgres",
      );
      script("provision-db.sh", "staging");
      sql(
        "arsdocendi_staging",
        "CREATE TABLE testigo(id int PRIMARY KEY, valor text); INSERT INTO testigo VALUES(1,'edición persistente');",
      );
      aws("s3api", "create-bucket", "--bucket", "arsdocendi-staging");
      cmd(
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
          "AWS_SECRET_ACCESS_KEY=synthetic-disposable",
          "-e",
          "AWS_DEFAULT_REGION=us-east-1",
          awsImage,
          "--endpoint-url",
          `http://${s3}:8333`,
          "s3",
          "cp",
          "-",
          "s3://arsdocendi-staging/adjunto/sintetico",
          "--content-type",
          "application/pdf",
          "--metadata",
          '{"fixture":"sintetica"}',
        ],
        { input: "bytes sintéticos PDF" },
      );
      const id = script("backup-storage.sh", "staging");
      assert.match(id, /^[0-9]{8}T[0-9]{6}-[a-f0-9]+$/);
      script("backup-volume.sh", "staging", "verify", id);
      const exportDir = join(dir, "export-sintetico");
      const reciente = script("backup-storage.sh", "staging", exportDir);
      cmd(["python3", join(repo, "infra/scripts/verificar-backup.py"), exportDir]);
      // Alterar una copia sintética prueba rechazo real de bytes corruptos.
      chmodSync(exportDir, 0o700);
      chmodSync(join(exportDir, "objects"), 0o700);
      chmodSync(join(exportDir, "objects/00000001.bin"), 0o600);
      writeFileSync(join(exportDir, "objects/00000001.bin"), "corrupto");
      const corrupto = spawnSync(
        "python3",
        [join(repo, "infra/scripts/verificar-backup.py"), exportDir],
        { encoding: "utf8" },
      );
      assert.notEqual(corrupto.status, 0);
      assert.match(corrupto.stderr, /hash incorrecto/);
      script("backup-volume.sh", "staging", "success", reciente);
      const listing = script("backup-volume.sh", "staging", "list");
      assert.match(listing, /complete retained-manual/);
      // El runner/cliente original ya terminó; otro contenedor puede recuperar.
      env.RECOVERY_AUTHORIZED = `recovery-${uid}`;
      script("restore-storage.sh", env.RECOVERY_AUTHORIZED, `volume:staging:${id}`);
      assert.equal(
        sql(`arsdocendi_recovery_${uid}`, "SELECT valor FROM testigo WHERE id=1;"),
        "edición persistente",
      );
      assert.equal(
        aws(
          "s3",
          "cp",
          `s3://arsdocendi-recovery-${uid}/adjunto/sintetico`,
          "-",
          "--only-show-errors",
        ),
        "bytes sintéticos PDF",
      );
      const head = JSON.parse(
        aws(
          "s3api",
          "head-object",
          "--bucket",
          `arsdocendi-recovery-${uid}`,
          "--key",
          "adjunto/sintetico",
        ),
      );
      assert.equal(head.ContentType, "application/pdf");
      assert.equal(head.Metadata.fixture, "sintetica");
      const repeat = spawnSync(
        "bash",
        [
          join(repo, "infra/scripts/restore-storage.sh"),
          env.RECOVERY_AUTHORIZED,
          `volume:staging:${id}`,
        ],
        { env, encoding: "utf8", timeout: 120000 },
      );
      assert.notEqual(repeat.status, 0);
      assert.match(repeat.stderr, /poblado/);
      // Rotación real de identidad de preview sobre un objeto existente, sin purge.
      const configure = (secret) =>
        cmd(["docker", "exec", "-i", s3, "weed", "shell"], {
          input: `s3.configure -access_key=app_preview -secret_key=${secret} -user=app_preview -buckets=arsdocendi-staging -actions=Read,Write,List,Tagging -apply\n`,
        });
      configure("synthetic-old");
      configure("synthetic-new");
      const app = cmd([
        "docker",
        "run",
        "--rm",
        "--network",
        net,
        "-e",
        "AWS_ACCESS_KEY_ID=app_preview",
        "-e",
        "AWS_SECRET_ACCESS_KEY=synthetic-new",
        "-e",
        "AWS_DEFAULT_REGION=us-east-1",
        awsImage,
        "--endpoint-url",
        `http://${s3}:8333`,
        "s3",
        "cp",
        "s3://arsdocendi-staging/adjunto/sintetico",
        "-",
        "--only-show-errors",
      ]);
      assert.equal(app, "bytes sintéticos PDF");
      // No completar fallos de pg_dump: ni borrar su registro por retención.
      const badEnv = { ...env, PGHOST: "host-sintetico-inexistente" };
      const failed = spawnSync("bash", [join(repo, "infra/scripts/backup-storage.sh"), "staging"], {
        env: badEnv,
        encoding: "utf8",
        timeout: 120000,
      });
      assert.notEqual(failed.status, 0);
      assert.match(script("backup-volume.sh", "staging", "list"), /incomplete retained-manual/);
      script("backup-volume.sh", "staging", "success", id);
      docker(
        "run",
        "--rm",
        "-v",
        `${volume}:/backups`,
        "--entrypoint",
        "/bin/sh",
        pgImage,
        "-ec",
        `printf 1 > /backups/deploys/${id}.success`,
      );
      script("backup-volume.sh", "staging", "retain");
      const retained = script("backup-volume.sh", "staging", "list");
      assert.doesNotMatch(retained, new RegExp(id));
      assert.match(retained, new RegExp(reciente));
      assert.match(retained, /incomplete retained-manual/);
      // Seed guard real: inicialización atómica, edición conservada, versión no reseed.
      const dataset = join(dir, "fixture.sql");
      writeFileSync(
        dataset,
        "BEGIN;\nCREATE TABLE IF NOT EXISTS public.seed_metadata(clave text primary key,valor text not null);\nINSERT INTO testigo VALUES(2,'fixture') ON CONFLICT(id) DO UPDATE SET valor=EXCLUDED.valor;\nCOMMIT;\n",
      );
      const guard = readFileSync(join(repo, "infra/scripts/seed-guard.sql"), "utf8");
      sql("arsdocendi_staging", guard + "\nCOMMIT;");
      const wrapped = cmd(["python3", join(repo, "infra/scripts/seed-transaccion.py"), dataset]);
      sql("arsdocendi_staging", wrapped);
      sql("arsdocendi_staging", "UPDATE testigo SET valor='editado desde app' WHERE id=2;");
      sql("arsdocendi_staging", wrapped);
      assert.equal(
        sql("arsdocendi_staging", "SELECT valor FROM testigo WHERE id=2;"),
        "editado desde app",
      );
      assert.equal(
        sql(
          "arsdocendi_staging",
          "SELECT valor FROM seed_metadata WHERE clave='inicializacion_completada';",
        ),
        "sintetico/v1",
      );
      // El marcador no aparece si se revierte el dataset; retry sólo sobre huella igual.
      sql("postgres", "CREATE DATABASE seed_retry;");
      sql(
        "seed_retry",
        "CREATE TABLE public.bootstrap_metadata(id boolean PRIMARY KEY DEFAULT true, ambiente text,origen text,estado text,huella_inicial text); INSERT INTO bootstrap_metadata VALUES(true,'staging','provision-db/v1','autorizado',null); CREATE TABLE testigo(id int PRIMARY KEY,valor text);",
      );
      sql("seed_retry", guard + "\nCOMMIT;");
      const failure = spawnSync(
        "docker",
        [
          "exec",
          "-i",
          pg,
          "psql",
          "-X",
          "-q",
          "-U",
          "postgres",
          "-d",
          "seed_retry",
          "-v",
          "ON_ERROR_STOP=1",
          "--set=ambiente=staging",
        ],
        {
          encoding: "utf8",
          input: wrapped.replace("INSERT INTO testigo", "SELECT 1/0;\nINSERT INTO testigo"),
        },
      );
      assert.notEqual(failure.status, 0);
      assert.equal(sql("seed_retry", "SELECT estado FROM bootstrap_metadata;"), "autorizado");
      sql("seed_retry", wrapped);
      assert.equal(sql("seed_retry", "SELECT estado FROM bootstrap_metadata;"), "completado");
      sql("postgres", "CREATE DATABASE no_autorizada;");
      const denied = spawnSync(
        "docker",
        [
          "exec",
          "-i",
          pg,
          "psql",
          "-X",
          "-q",
          "-U",
          "postgres",
          "-d",
          "no_autorizada",
          "-v",
          "ON_ERROR_STOP=1",
          "--set=ambiente=staging",
        ],
        { encoding: "utf8", input: wrapped },
      );
      assert.notEqual(denied.status, 0);
      assert.match(denied.stderr, /sin autorización/);
    } finally {
      // Sólo nombres registrados por este fixture; nunca recursos preexistentes.
      spawnSync("docker", ["rm", "-fv", pg, s3]);
      spawnSync("docker", ["network", "rm", net]);
      spawnSync("docker", ["volume", "rm", volume]);
      rmSync(dir, { recursive: true, force: true });
    }
  },
);
