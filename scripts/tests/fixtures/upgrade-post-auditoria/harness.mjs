import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { createHash, randomBytes } from "node:crypto";

export const anterior = "f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b";
export const hash = (bytes) => createHash("sha256").update(bytes).digest("hex");
export const repo = new URL("../../../../", import.meta.url).pathname;
export const esperar = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
export function crearHarness() {
  const uid = randomBytes(8).toString("hex");
  const prefijo = `ars-upgrade-${uid}`;
  const dir = join(repo, ".artifacts/upgrade-post-auditoria", uid);
  mkdirSync(dir, { recursive: true });
  const registro = [];
  const evidencia = { anterior, uid, pasos: [], limpieza: [] };
  const guardar = () =>
    writeFileSync(join(dir, "resultado.json"), JSON.stringify(evidencia, null, 2));
  function raw(args, opts = {}) {
    return spawnSync(args[0], args.slice(1), {
      cwd: repo,
      encoding: null,
      maxBuffer: 64 * 1024 * 1024,
      timeout: 180000,
      ...opts,
    });
  }
  function cmd(args, opts = {}) {
    const r = raw(args, opts);
    assert.equal(r.status, 0, `${args[0]} ${args[1]}: ${r.stderr?.toString().slice(-3000)}`);
    return r.stdout;
  }
  function paso(nombre, datos = {}) {
    evidencia.pasos.push({ nombre, ...datos });
    guardar();
  }
  const env = {
    PATH: process.env.PATH,
    HOME: process.env.HOME,
    PGHOST: `${prefijo}-pg`,
    PGPORT: "5432",
    PGUSER: "postgres",
    PGPASSWORD: "synthetic-upgrade",
    RED_DATOS: prefijo,
    APP_DB_USER: "app_staging",
    APP_DB_PASSWORD: "synthetic-upgrade",
    SEAWEEDFS_ROOT_ACCESS_KEY: "root-test",
    SEAWEEDFS_ROOT_SECRET_KEY: "synthetic-upgrade",
    BACKUP_VOLUME_PREFIX: `${prefijo}-backup`,
    LOCK_PREFIX: `${prefijo}-lock`,
  };
  function docker(...args) {
    return cmd(["docker", ...args], { env });
  }
  function sql(base, texto, vars = []) {
    return cmd(
      [
        "docker",
        "exec",
        "-i",
        env.PGHOST,
        "psql",
        "-X",
        "-qtA",
        "-U",
        "postgres",
        "-d",
        base,
        "-v",
        "ON_ERROR_STOP=1",
        ...vars.flatMap(([k, v]) => ["-v", `${k}=${v}`]),
      ],
      { input: texto },
    )
      .toString()
      .trim();
  }
  function script(nombre, ...args) {
    return cmd(["bash", join(repo, "infra/scripts", nombre), ...args], { env, timeout: 300000 })
      .toString()
      .trim();
  }
  function cli(imagen, base, args, rechazar = false) {
    const conexion = `Host=${env.PGHOST};Database=${base};Username=app_staging;Password=synthetic-upgrade`;
    const r = raw(
      [
        "docker",
        "run",
        "--rm",
        "--label",
        `ars-upgrade=${uid}`,
        "--network",
        prefijo,
        "-e",
        "ConnectionStrings__ArsDocendi",
        "-e",
        "ASPNETCORE_ENVIRONMENT",
        "-e",
        "RELEASE_SHA",
        imagen,
        "dotnet",
        "ArsDocendi.Host.dll",
        ...args,
      ],
      {
        env: {
          ...env,
          ConnectionStrings__ArsDocendi: conexion,
          ASPNETCORE_ENVIRONMENT: "Development",
          RELEASE_SHA: imagen === evidencia.candidato ? imagen : anterior,
        },
      },
    );
    const archivo = `${String(evidencia.pasos.length).padStart(3, "0")}-${args[0].replaceAll("--", "")}-${base}`;
    writeFileSync(join(dir, `${archivo}.stdout`), r.stdout);
    writeFileSync(join(dir, `${archivo}.stderr`), r.stderr);
    paso(`cli:${args[0]}`, {
      base,
      imagen,
      exit: r.status,
      archivo,
      stdoutSha256: hash(r.stdout),
      stderrSha256: hash(r.stderr),
    });
    assert.equal(
      r.status,
      rechazar ? 1 : 0,
      `CLI ${args[0]}: ${r.stderr?.toString().slice(-3000)}`,
    );
    return r;
  }
  function columnas(base) {
    return JSON.parse(
      sql(
        base,
        `SELECT coalesce(json_agg(t ORDER BY esquema,tabla),'[]') FROM
      (SELECT table_schema esquema,table_name tabla,json_agg(column_name ORDER BY ordinal_position) columnas
       FROM information_schema.columns WHERE table_schema IN ('identity','audit','storage','designaciones','portal','tareas','public')
       GROUP BY table_schema,table_name) t;`,
      ),
    );
  }
  const q = (s) => '"' + s.replaceAll('"', '""') + '"';
  function snapshot(base, tablas = columnas(base), filtroAudit = null) {
    if (tablas.length === 0) return {};
    const consultas = tablas.map((t) => {
      const filtro =
        t.esquema === "audit" && t.tabla === "change_log" && filtroAudit !== null
          ? ` WHERE id<=${filtroAudit}`
          : "";
      const nombre = `${t.esquema}.${t.tabla}`.replaceAll("'", "''");
      return `SELECT '${nombre}' nombre,coalesce(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text COLLATE "C"),'[]') filas FROM
        (SELECT ${t.columnas.map(q).join(",")} FROM ${q(t.esquema)}.${q(t.tabla)}${filtro}) t`;
    });
    const filas = JSON.parse(
      sql(
        base,
        `BEGIN READ ONLY; SELECT json_agg(r ORDER BY nombre) FROM (${consultas.join(" UNION ALL ")}) r; COMMIT;`,
      ),
    );
    return Object.fromEntries(
      filas.map((r) => [
        r.nombre,
        { filas: r.filas.length, sha256: hash(JSON.stringify(r.filas)) },
      ]),
    );
  }
  function registrar(tipo, nombre) {
    registro.push({ tipo, nombre });
  }
  function limpiar() {
    let fallo;
    for (const { tipo, nombre } of [...registro].reverse()) {
      try {
        const volumenesAnonimos =
          tipo === "container"
            ? JSON.parse(
                cmd(["docker", "container", "inspect", "--format", "{{json .Mounts}}", nombre]),
              )
                .filter((m) => m.Type === "volume")
                .map((m) => m.Name)
            : [];
        const args =
          tipo === "container" ? ["docker", "rm", "-fv", nombre] : ["docker", tipo, "rm", nombre];
        const r = raw(args);
        assert.equal(r.status, 0, r.stderr?.toString());
        const consulta = raw(["docker", tipo, "inspect", nombre]);
        assert.notEqual(consulta.status, 0);
        assert.match(consulta.stderr.toString(), /no such|not found/i);
        evidencia.limpieza.push({ tipo, nombre, ausente: true });
        for (const volumen of volumenesAnonimos) {
          const consultaVolumen = raw(["docker", "volume", "inspect", volumen]);
          assert.notEqual(consultaVolumen.status, 0);
          assert.match(consultaVolumen.stderr.toString(), /no such|not found/i);
          evidencia.limpieza.push({ tipo: "volume-anonimo", nombre: volumen, ausente: true });
        }
      } catch (e) {
        fallo ??= e;
        evidencia.limpieza.push({ tipo, nombre, error: e.message });
      }
    }
    const remanentes = cmd([
      "docker",
      "ps",
      "-a",
      "--filter",
      `label=ars-upgrade=${uid}`,
      "--format",
      "{{.Names}}",
    ]);
    assert.equal(remanentes.toString().trim(), "");
    const nombres = cmd(["docker", "ps", "-a", "--format", "{{.Names}}"]);
    assert.deepEqual(
      nombres
        .toString()
        .split("\n")
        .filter((n) => n.startsWith(prefijo)),
      [],
      "Quedó un cliente o lock propio",
    );
    guardar();
    if (fallo) throw fallo;
  }
  return {
    uid,
    prefijo,
    dir,
    evidencia,
    guardar,
    raw,
    cmd,
    paso,
    env,
    docker,
    sql,
    script,
    cli,
    columnas,
    snapshot,
    registrar,
    limpiar,
  };
}
