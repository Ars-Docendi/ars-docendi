import {
  construirAnterior,
  verificarCatalogo,
} from "./fixtures/upgrade-post-auditoria/baseline.mjs";
// Runner compilado histórico, PostgreSQL 18 y SeaweedFS reales. Nunca replay de SQL como runner.
import assert from "node:assert/strict";
import { test } from "node:test";
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { crearHarness, esperar, hash, repo } from "./fixtures/upgrade-post-auditoria/harness.mjs";

import { verificarCandidato } from "./fixtures/upgrade-post-auditoria/candidato.mjs";

const fixtures = join(repo, "scripts/tests/fixtures/upgrade-post-auditoria");
const bytes = Buffer.from("%PDF-1.4\nEnsayo sintético aislado, sin personas reales.\n%%EOF\n");
const bucket = "arsdocendi-staging",
  clave = "upgrade/legacy.pdf";
const uid = (n) => `90000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const incompatibles = [
  [
    "upn",
    "identity-upn-duplicado",
    `INSERT INTO identity.users(upn,display_name) VALUES (' UPGRADE@example.test ','Cuenta incompatible sintética');`,
  ],
  [
    "materia",
    "designaciones-materia",
    `UPDATE designaciones.designaciones SET materia_id='${uid(99)}';`,
  ],
  [
    "scope",
    "identity-scope-incoherente",
    `UPDATE identity.roles SET scope='materia' WHERE id='${uid(5)}';`,
  ],
  ["ciclo", "ciclo", `UPDATE tareas.tareas SET tarea_padre_id='${uid(17)}' WHERE id='${uid(16)}';`],
];

test(
  "instalación f10e0a5, backup conjunto, recovery y candidato real opt-in",
  {
    skip: process.env.UPGRADE_DOCKER_TESTS !== "1",
    timeout: 1200000,
  },
  async (t) => {
    const h = crearHarness();
    const base = "arsdocendi_staging",
      pg = h.env.PGHOST,
      s3 = `${h.prefijo}-s3`;
    const comun = readFileSync(join(repo, "infra/scripts/_comun.sh"), "utf8");
    const imagenInfra = (nombre) =>
      comun.match(new RegExp(`${nombre}=\\"\\$\\{${nombre}:-([^}]+)\\}\\"`))[1];
    const aws = (args, input) =>
      h.cmd(
        [
          "docker",
          "run",
          "--rm",
          "-i",
          "--label",
          `ars-upgrade=${h.uid}`,
          "--network",
          h.prefijo,
          "-e",
          "AWS_ACCESS_KEY_ID",
          "-e",
          "AWS_SECRET_ACCESS_KEY",
          "-e",
          "AWS_DEFAULT_REGION",
          imagenInfra("AWS_CLI_IMAGE"),
          "--endpoint-url",
          `http://${s3}:8333`,
          ...args,
        ],
        {
          input,
          env: {
            ...h.env,
            AWS_ACCESS_KEY_ID: "root-test",
            AWS_SECRET_ACCESS_KEY: "synthetic-upgrade",
            AWS_DEFAULT_REGION: "us-east-1",
          },
        },
      );
    const objeto = (b) => {
      const descargado = aws(["s3", "cp", `s3://${b}/${clave}`, "-", "--only-show-errors"]);
      const metadata = JSON.parse(aws(["s3api", "head-object", "--bucket", b, "--key", clave]));
      assert.deepEqual(descargado, bytes);
      assert.equal(metadata.ContentLength, bytes.length);
      assert.equal(metadata.ContentType, "application/pdf");
      assert.equal(metadata.Metadata.fixture, "upgrade-sintetico");
      return {
        sha256: hash(descargado),
        tamano: metadata.ContentLength,
        mime: metadata.ContentType,
        metadata: metadata.Metadata,
      };
    };
    try {
      const baseline = construirAnterior(h);
      h.docker("network", "create", h.prefijo);
      h.registrar("network", h.prefijo);
      h.docker(
        "run",
        "-d",
        "--name",
        pg,
        "--label",
        `ars-upgrade=${h.uid}`,
        "--network",
        h.prefijo,
        "-e",
        "POSTGRES_PASSWORD=synthetic-upgrade",
        "postgres:18-alpine",
      );
      h.registrar("container", pg);
      h.docker(
        "run",
        "-d",
        "--name",
        s3,
        "--label",
        `ars-upgrade=${h.uid}`,
        "--network",
        h.prefijo,
        "--network-alias",
        "seaweedfs-nonprod",
        "--entrypoint",
        "/bin/sh",
        imagenInfra("SEAWEEDFS_IMAGE"),
        "-ec",
        'printf \'%s\' \'{"identities":[{"name":"admin","credentials":[{"accessKey":"root-test","secretKey":"synthetic-upgrade"}],"actions":["Admin","Read","List","Tagging","Write"]}]}\' > /tmp/s3.json; exec weed mini -dir=/data -s3 -s3.config=/tmp/s3.json -master.telemetry=false -volume.max=8 -master.volumeSizeLimitMB=128',
      );
      h.registrar("container", s3);
      // Espera acotada de disponibilidad de recursos propios, no del candidato.
      let listo = false;
      for (let i = 0; i < 60; i++) {
        if (h.raw(["docker", "exec", pg, "pg_isready", "-U", "postgres"]).status === 0) {
          listo = true;
          break;
        }
        await esperar(500);
      }
      assert.ok(listo, "PostgreSQL no inició");
      listo = false;
      for (let i = 0; i < 60; i++) {
        const r = h.raw([
          "docker",
          "exec",
          s3,
          "wget",
          "-q",
          "-O",
          "/dev/null",
          "http://127.0.0.1:8333",
        ]);
        if (r.status === 0 || r.stderr.toString().includes("403")) {
          listo = true;
          break;
        }
        await esperar(500);
      }
      assert.ok(listo, "SeaweedFS no inició");
      assert.equal(
        h.sql("postgres", "BEGIN READ ONLY; SELECT current_database()||':'||current_user; COMMIT;"),
        "postgres:postgres",
      );
      h.paso("infra-real", {
        postgres: h.sql("postgres", "SELECT version()"),
        seaweedfsImage: imagenInfra("SEAWEEDFS_IMAGE"),
      });
      h.script("provision-db.sh", "staging");
      h.cli(baseline, base, ["--validar-recursos"]);
      const vacia = h.snapshot(base);
      const estado = JSON.parse(h.cli(baseline, base, ["--estado-migraciones"]).stdout);
      assert.ok(estado.pendientes > 0);
      const preview = h.cli(baseline, base, ["--script-migraciones", "-"]).stdout;
      writeFileSync(join(h.dir, "preview-baseline.tar"), preview);
      assert.match(h.cmd(["tar", "-tf", "-"], { input: preview }).toString(), /manifiesto.json/);
      assert.deepEqual(h.snapshot(base), vacia);
      h.cli(baseline, base, ["--migrate"]);
      h.sql(base, readFileSync(join(fixtures, "compatible.sql"), "utf8"), [
        ["tamano", bytes.length],
        ["hash", hash(bytes)],
      ]);
      verificarCatalogo(h, base);
      aws(["s3api", "create-bucket", "--bucket", bucket]);
      aws(
        [
          "s3",
          "cp",
          "-",
          `s3://${bucket}/${clave}`,
          "--content-type",
          "application/pdf",
          "--metadata",
          '{"fixture":"upgrade-sintetico"}',
          "--only-show-errors",
        ],
        bytes,
      );
      const esperadoObjeto = objeto(bucket);
      const columnas = h.columnas(base);
      const original = h.snapshot(base, columnas);
      const secuencias = (destino) =>
        h.sql(
          destino,
          `SELECT json_build_array(
      (SELECT json_build_object('last_value',last_value,'is_called',is_called) FROM designaciones.pedidos_numero_seq),
      (SELECT json_build_object('last_value',last_value,'is_called',is_called) FROM tareas.tareas_numero_seq),
      (SELECT json_build_object('last_value',last_value,'is_called',is_called) FROM tareas.proyectos_numero_seq));`,
        );
      const secuenciasOriginales = secuencias(base);
      const negocios = columnas.filter((x) => x.tabla !== "__EFMigrationsHistory");
      const corte = h.sql(base, "SELECT max(id) FROM audit.change_log");
      const protegido = h.snapshot(base, negocios, corte);
      assert.ok(
        JSON.parse(
          h.sql(base, `SELECT json_agg(new_row) FROM audit.change_log WHERE table_name='personas'`),
        )[0].documento,
      );
      h.cli(baseline, base, ["--estado-migraciones"]);
      h.cli(baseline, base, ["--script-migraciones", "-"]);
      h.cli(baseline, base, ["--migrate"]);
      assert.deepEqual(h.snapshot(base, columnas), original);
      assert.equal(secuencias(base), secuenciasOriginales);
      h.paso("baseline-noop-readonly", {
        tablas: original,
        objeto: esperadoObjeto,
        secuencias: JSON.parse(secuenciasOriginales),
      });
      for (const [nombre, , sql] of incompatibles) {
        h.sql(
          "postgres",
          `CREATE DATABASE incompatible_${nombre} TEMPLATE ${base} OWNER app_staging;`,
        );
        h.sql(`incompatible_${nombre}`, sql);
      }
      h.sql(
        "postgres",
        `CREATE DATABASE historia_incompatible TEMPLATE ${base} OWNER app_staging;`,
      );
      h.sql(
        "historia_incompatible",
        `INSERT INTO identity."__EFMigrationsHistory" VALUES ('historia-alpha-sintetica','10.0.11');`,
      );
      const historiaAntes = h.snapshot("historia_incompatible");
      h.cli(baseline, "historia_incompatible", ["--estado-migraciones"], true);
      h.cli(baseline, "historia_incompatible", ["--migrate"], true);
      assert.deepEqual(h.snapshot("historia_incompatible"), historiaAntes);
      h.paso("historia-incompatible-real", { sinStampNiReset: true, tablas: historiaAntes });
      h.registrar("volume", `${h.env.BACKUP_VOLUME_PREFIX}-staging`);
      const backup = h.script("backup-storage.sh", "staging");
      h.script("backup-volume.sh", "staging", "verify", backup);
      const fallido = construirAnterior(h, true);
      h.cli(fallido, base, ["--validar-recursos"]);
      const antesDelFallo = h.snapshot(base);
      const migracionFallida = h.cli(fallido, base, ["--migrate"], true);
      assert.match(migracionFallida.stderr.toString(), /22012/);
      assert.deepEqual(h.snapshot(base), antesDelFallo);
      assert.equal(
        h.sql(base, "SELECT to_regclass('identity.testigo_fallo_upgrade') IS NULL"),
        "t",
      );
      assert.deepEqual(objeto(bucket), esperadoObjeto);
      h.script("backup-volume.sh", "staging", "verify", backup);
      h.paso("rollback-migracion-compilada", {
        sqlState: "22012",
        historialYNegocioSinCambios: true,
        testigoAusente: true,
        backupConservado: true,
      });
      h.env.RECOVERY_AUTHORIZED = `recovery-${h.uid}`;
      h.script("restore-storage.sh", h.env.RECOVERY_AUTHORIZED, `volume:staging:${backup}`);
      const recuperada = `arsdocendi_recovery_${h.uid}`;
      assert.deepEqual(h.snapshot(recuperada, columnas), original);
      assert.equal(secuencias(recuperada), secuenciasOriginales);
      assert.deepEqual(objeto(`arsdocendi-recovery-${h.uid}`), esperadoObjeto);
      assert.equal(
        JSON.parse(h.cli(baseline, recuperada, ["--estado-migraciones"]).stdout).pendientes,
        0,
      );
      h.cli(baseline, recuperada, ["--migrate"]);
      assert.deepEqual(h.snapshot(recuperada, columnas), original);
      h.paso("recovery-compatible-real", { backup, base: recuperada, objeto: esperadoObjeto });
      const repetido = h.raw(
        [
          "bash",
          join(repo, "infra/scripts/restore-storage.sh"),
          h.env.RECOVERY_AUTHORIZED,
          `volume:staging:${backup}`,
        ],
        { env: h.env },
      );
      assert.notEqual(repetido.status, 0);
      assert.match(repetido.stderr.toString(), /poblado/);
      assert.deepEqual(h.snapshot(recuperada, columnas), original);
      const falloProveedor = h.raw(
        ["bash", join(repo, "infra/scripts/backup-storage.sh"), "staging"],
        {
          env: { ...h.env, PGPORT: "1" },
          timeout: 120000,
        },
      );
      assert.ok(
        Number.isInteger(falloProveedor.status) && falloProveedor.status !== 0,
        "El proveedor debe devolver fallo real, no timeout del harness",
      );
      h.script("backup-volume.sh", "staging", "verify", backup);
      assert.match(h.script("backup-volume.sh", "staging", "list"), /incomplete retained-manual/);
      h.paso("fallos-reales-sin-reset", {
        restorePobladoExit: repetido.status,
        proveedorExit: falloProveedor.status,
        backupConservado: true,
      });

      await t.test(
        "candidato consolidado: upgrade, instalación, incompatibles y constraints",
        {
          skip: !process.env.UPGRADE_CANDIDATE_IMAGE,
        },
        async () => {
          await verificarCandidato({
            h,
            base,
            negocios,
            corte,
            protegido,
            esperadoObjeto,
            objeto,
            pg,
            secuencias,
            secuenciasOriginales,
            backup,
            uid,
            incompatibles,
            bucket,
          });
        },
      );
      h.evidencia.estado = process.env.UPGRADE_CANDIDATE_IMAGE
        ? "ensayo-finalizado-consultar-subtests"
        : "baseline-recovery-verificados-candidato-no-ejecutado";
    } catch (error) {
      h.evidencia.estado = "fallido";
      h.evidencia.error = error.message;
      throw error;
    } finally {
      h.guardar();
      h.limpiar();
      console.log(`Evidencia JSON: ${join(h.dir, "resultado.json")}`);
    }
  },
);
