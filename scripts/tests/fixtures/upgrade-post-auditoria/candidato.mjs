import { verificarCatalogo } from "./baseline.mjs";
import assert from "node:assert/strict";
import { writeFileSync } from "node:fs";
import { join } from "node:path";

export async function verificarCandidato({
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
}) {
  const imagen = h
    .docker("image", "inspect", "--format", "{{.Id}}", process.env.UPGRADE_CANDIDATE_IMAGE)
    .toString()
    .trim();
  h.evidencia.candidato = imagen;
  h.cli(imagen, base, ["--validar-recursos"]);
  const antes = h.snapshot(base);
  const status = JSON.parse(h.cli(imagen, base, ["--estado-migraciones"]).stdout);
  assert.ok(
    status.pendientes > 0,
    "El candidato debe contener incrementales posteriores a f10e0a5",
  );
  const plan = h.cli(imagen, base, ["--script-migraciones", "-"]).stdout;
  writeFileSync(join(h.dir, "preview-candidato.tar"), plan);
  assert.match(h.cmd(["tar", "-tf", "-"], { input: plan }).toString(), /manifiesto.json/);
  h.cli(imagen, base, ["--diagnosticar-integridad"]);
  assert.deepEqual(h.snapshot(base), antes);
  for (const [nombre, categoria] of incompatibles) {
    const destino = `incompatible_${nombre}`,
      cols = h.columnas(destino),
      inicial = h.snapshot(destino, cols);
    const d = JSON.parse(h.cli(imagen, destino, ["--diagnosticar-integridad"], true).stdout);
    assert.ok(JSON.stringify(d.hallazgos).includes(categoria), `Falta categoría ${categoria}`);
    assert.deepEqual(h.snapshot(destino, cols), inicial);
    const protegidas = cols.filter((x) => x.tabla !== "__EFMigrationsHistory");
    const corteCaso = h.sql(destino, "SELECT max(id) FROM audit.change_log");
    const previas = h.snapshot(destino, protegidas, corteCaso);
    h.cli(imagen, destino, ["--migrate"], true);
    assert.deepEqual(h.snapshot(destino, protegidas, corteCaso), previas);
    h.paso(`incompatible:${nombre}`, {
      diagnostico: d,
      negocioLegadoPreservado: true,
      publicacionEjecutada: false,
    });
  }
  h.cli(imagen, base, ["--migrate"]);
  assert.deepEqual(h.snapshot(base, negocios, corte), protegido);
  assert.deepEqual(objeto(bucket), esperadoObjeto);
  const catalogo = verificarCatalogo(h, base);
  assert.ok(
    catalogo.constraints.some(
      (x) => x.tabla === "designaciones" && x.tipo === "f" && x.definicion.includes("materia_id"),
    ),
  );
  assert.ok(
    catalogo.indices.some(
      (x) => x.tablename === "users" && x.indexdef.includes("lower(btrim(upn))"),
    ),
  );
  assert.equal(secuencias(base), secuenciasOriginales);
  const violaciones = [
    [
      "upn-normalizado",
      "23505",
      "upn",
      `INSERT INTO identity.users(upn,display_name) VALUES (' UPGRADE@example.test ','Duplicado sintético');`,
    ],
    [
      "materia-fk",
      "23503",
      "materia",
      `UPDATE designaciones.designaciones SET materia_id='${uid(99)}';`,
    ],
    [
      "scope-vivo",
      "23514",
      "scope",
      `UPDATE identity.roles SET scope='materia' WHERE id='${uid(5)}';`,
    ],
    [
      "ciclo",
      "23514",
      "ciclo",
      `UPDATE tareas.tareas SET tarea_padre_id='${uid(17)}' WHERE id='${uid(16)}';`,
    ],
    [
      "solucion",
      "23514",
      "solucion",
      `UPDATE tareas.tareas SET estado_id=(SELECT id FROM tareas.estados_tarea WHERE codigo='resuelta'),solucion=NULL WHERE id='${uid(17)}';`,
    ],
  ];
  for (const [nombre, sqlState, identificador, texto] of violaciones) {
    const previo = h.snapshot(base);
    const r = h.raw(
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
        "-v",
        "VERBOSITY=verbose",
      ],
      { input: texto },
    );
    assert.notEqual(r.status, 0);
    assert.match(r.stderr.toString(), new RegExp(sqlState));
    assert.match(r.stderr.toString(), new RegExp(identificador, "i"));
    assert.deepEqual(h.snapshot(base), previo);
    h.paso(`constraint-real:${nombre}`, {
      sqlState,
      exit: r.status,
      sinFilasNiAuditoriaParcial: true,
    });
  }
  const completo = h.snapshot(base);
  assert.equal(JSON.parse(h.cli(imagen, base, ["--estado-migraciones"]).stdout).pendientes, 0);
  h.cli(imagen, base, ["--migrate"]);
  assert.deepEqual(h.snapshot(base), completo);
  h.sql("postgres", "CREATE DATABASE nueva_candidato OWNER app_staging;");
  h.cli(imagen, "nueva_candidato", ["--migrate"]);
  h.cli(imagen, "nueva_candidato", ["--diagnosticar-integridad"]);
  verificarCatalogo(h, "nueva_candidato");
  h.script("backup-volume.sh", "staging", "verify", backup);
  h.paso("candidato-verificado", { imagen, objeto: objeto(bucket) });
}
