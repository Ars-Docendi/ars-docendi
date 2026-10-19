import assert from "node:assert/strict";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { anterior } from "./harness.mjs";

export function construirAnterior(h, inyectarFallo = false) {
  const raiz = join(h.dir, "anterior"),
    salida = join(h.dir, inyectarFallo ? "publish-fallo" : "publish");
  if (!inyectarFallo) {
    mkdirSync(raiz);
    h.cmd(["tar", "-xf", "-", "-C", raiz], { input: h.cmd(["git", "archive", anterior]) });
  } else {
    const generado = JSON.parse(
      h.cmd([
        "node",
        "scripts/migrations/scaffold.mjs",
        "Identity",
        "FalloSinteticoEnsayo",
        "--root",
        raiz,
      ]),
    );
    writeFileSync(
      join(raiz, generado.archivos.find((x) => x.ruta.endsWith(".sql")).ruta),
      "CREATE TABLE identity.testigo_fallo_upgrade(id integer PRIMARY KEY);\nINSERT INTO identity.testigo_fallo_upgrade VALUES (1);\nSELECT 1/0;\n",
    );
  }
  const build = h.raw(
    [
      "dotnet",
      "publish",
      join(raiz, "backend/src/ArsDocendi.Host/ArsDocendi.Host.csproj"),
      "-c",
      "Release",
      "-o",
      salida,
      "--nologo",
    ],
    { timeout: 600000 },
  );
  const sufijo = inyectarFallo ? "fallo" : "baseline";
  writeFileSync(join(h.dir, `build-${sufijo}.log`), Buffer.concat([build.stdout, build.stderr]));
  assert.equal(build.status, 0, `Falló publish ${sufijo}; ver build-${sufijo}.log`);
  writeFileSync(
    join(salida, "Dockerfile"),
    `FROM mcr.microsoft.com/dotnet/aspnet:10.0\nWORKDIR /app\nCOPY . ./\nLABEL org.opencontainers.image.revision=${anterior}\nCMD ["dotnet","ArsDocendi.Host.dll"]\n`,
  );
  const imagen = `${h.prefijo}-${sufijo}:ensayo`;
  const r = h.raw(["docker", "build", "-t", imagen, "-"], {
    input: h.cmd(["tar", "-cf", "-", "-C", salida, "."]),
    timeout: 600000,
  });
  writeFileSync(join(h.dir, `image-${sufijo}.log`), Buffer.concat([r.stdout, r.stderr]));
  assert.equal(r.status, 0, `Falló imagen ${sufijo}; ver image-${sufijo}.log`);
  h.registrar("image", imagen);
  const id = h.docker("image", "inspect", "--format", "{{.Id}}", imagen).toString().trim();
  h.paso(`${sufijo}-compilado`, {
    shaOrigen: anterior,
    derivadoConFalloSintetico: inyectarFallo,
    imagenId: id,
    publishExit: build.status,
  });
  return id;
}

export function verificarCatalogo(h, base) {
  const datos = JSON.parse(
    h.sql(
      base,
      `SELECT json_build_object(
    'constraints',(SELECT json_agg(c ORDER BY esquema,tabla,nombre) FROM
      (SELECT n.nspname esquema,r.relname tabla,c.conname nombre,c.contype tipo,c.convalidated validada,
       pg_get_constraintdef(c.oid) definicion FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid
       JOIN pg_namespace n ON n.oid=r.relnamespace WHERE n.nspname IN ('identity','storage','designaciones','portal','tareas')) c),
    'indices',(SELECT json_agg(i ORDER BY schemaname,indexname) FROM pg_indexes i WHERE schemaname IN ('identity','portal','tareas','designaciones','audit','storage')),
    'triggers',(SELECT json_agg(t ORDER BY esquema,tabla,nombre) FROM
      (SELECT n.nspname esquema,r.relname tabla,t.tgname nombre,pg_get_triggerdef(t.oid) definicion
       FROM pg_trigger t JOIN pg_class r ON r.oid=t.tgrelid JOIN pg_namespace n ON n.oid=r.relnamespace
       WHERE NOT t.tgisinternal AND n.nspname IN ('identity','storage','designaciones','portal','tareas')) t));`,
    ),
  );
  assert.ok(datos.constraints.every((x) => x.validada));
  assert.ok(datos.constraints.some((x) => x.nombre === "designaciones_sin_solapamiento"));
  writeFileSync(join(h.dir, `catalogo-${base}.json`), JSON.stringify(datos, null, 2));
  return datos;
}
