import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import test from "node:test";

const carpeta = new URL("./fixtures/rendimiento-candidatos/", import.meta.url);
const candidatos = JSON.parse(readFileSync(new URL("candidatos.json", carpeta)));
const resultado = JSON.parse(readFileSync(new URL("resultado.json", carpeta)));

function nodos(plan) {
  return [plan, ...(plan.Plans ?? []).flatMap(nodos)];
}

function caso(id, n = 100000) {
  return resultado.resultados.find((r) => r.candidato.id === id && r.n === n);
}

test("informe vincula cada candidato con el artefacto real por hash", () => {
  const informe = readFileSync(
    new URL(
      "../../openspec/changes/corregir-integridad-y-rendimiento-post-auditoria/evidence/indices-candidatos-implementacion.md",
      import.meta.url,
    ),
    "utf8",
  );
  for (const c of candidatos) assert.ok(informe.includes(`**${c.id} — `));
  for (const nombre of ["datos.sql", "candidatos.json", "resultado.json"]) {
    const huella = createHash("sha256")
      .update(readFileSync(new URL(nombre, carpeta)))
      .digest("hex");
    assert.ok(informe.includes(huella), nombre);
  }
});

test("benchmark completo: candidatos únicos, dos volúmenes y limpieza real", () => {
  assert.equal(new Set(candidatos.map((c) => c.id)).size, candidatos.length);
  assert.equal(resultado.completo, true);
  assert.equal(resultado.contenedor_eliminado, true);
  assert.equal(resultado.resultados.length, candidatos.length * 2);
  assert.match(resultado.postgres, /PostgreSQL 18\.4/);
  assert.match(resultado.imagen_id, /^sha256:[a-f0-9]{64}$/);
  assert.equal(resultado.sha_baseline, "f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b");
  for (const n of [20000, 100000]) {
    assert.deepEqual(
      resultado.resultados
        .filter((r) => r.n === n)
        .map((r) => r.candidato.id)
        .sort(),
      candidatos.map((c) => c.id).sort(),
    );
  }
});

test("resultados íntegros, SQL sin LIMIT visible y todas las repeticiones reales", () => {
  for (const r of resultado.resultados) {
    assert.deepEqual(
      r.candidato,
      candidatos.find((c) => c.id === r.candidato.id),
    );
    assert.deepEqual(r.fases.antes.contenido, r.fases.despues.contenido);
    if (r.candidato.orden) assert.ok(r.consulta.endsWith(`ORDER BY ${r.candidato.orden}`));
    // LIMIT en subconsultas de catálogos no pagina las grillas.
    if (r.candidato.orden) assert.doesNotMatch(r.consulta, /LIMIT/i);
    for (const f of Object.values(r.fases)) {
      assert.equal(f.warm.length, 5);
      assert.equal(f.inserciones.length, 3);
      assert.equal(f.updates.length, 3);
      assert.equal(f.shared_cold_os_no_controlado.Plan["Actual Rows"], f.contenido.filas);
      for (const p of f.warm) {
        assert.equal(p.Plan["Actual Rows"], f.contenido.filas);
        assert.ok(p["Execution Time"] >= 0);
        assert.ok("Shared Hit Blocks" in p.Plan);
        assert.ok("Shared Read Blocks" in p.Plan);
      }
      for (const p of [...f.inserciones, ...f.updates]) {
        assert.equal(p.Plan["Node Type"], "ModifyTable");
        assert.ok(p.Plan["WAL Bytes"] > 0);
        assert.ok(nodos(p.Plan).some((n) => n["Actual Rows"] === 500));
      }
    }
    assert.ok(r.fases.despues.tamano.candidato_bytes > 0);
  }
});

test("responsable conserva 50 tareas aunque quintuplica el volumen ajeno", () => {
  for (const n of [20000, 100000]) {
    assert.equal(caso("tareas-responsable-orden", n).fases.antes.contenido.filas, 50);
  }
});

test("evidencia demuestra cobertura GiST y parcial, no sólo inventario nominal", () => {
  const persona = caso("designaciones-persona-gist");
  assert.ok(
    nodos(persona.fases.antes.warm[0].Plan).some(
      (n) => n["Index Name"] === "designaciones_sin_solapamiento",
    ),
  );
  const origen = caso("designaciones-origen-parcial");
  assert.ok(
    nodos(origen.fases.antes.warm[0].Plan).some(
      (n) => n["Index Name"] === "designaciones_origen_pedido_idx",
    ),
  );
  const parcial = caso("pedidos-estado-parcial");
  assert.ok(
    parcial.fases.despues.warm.every(
      (p) => !nodos(p.Plan).some((n) => n["Index Name"] === "bench_candidato"),
    ),
  );
});

test("FK selectiva pedido pierde el scan global con el candidato", () => {
  const r = caso("idempotencia-pedido-fk");
  assert.equal(r.fases.antes.warm[0].Plan["Node Type"], "Seq Scan");
  assert.ok(nodos(r.fases.despues.warm[0].Plan).some((n) => n["Index Name"] === "bench_candidato"));
  assert.ok(
    r.fases.despues.resumen_lectura.buffers_mediana < r.fases.antes.resumen_lectura.buffers_mediana,
  );
});

test("ejecutor desechable: sin conexiones externas ni binds; recursos versionados", () => {
  const fuente = readFileSync(new URL("medir.py", carpeta), "utf8");
  assert.ok(fuente.includes("'--network', 'none'"));
  assert.doesNotMatch(fuente, /--mount|--volume|DATABASE_URL|ConnectionStrings/);
  assert.ok(resultado.recursos_sql.length > 20);
  for (const r of resultado.recursos_sql) assert.match(r.sha256, /^[a-f0-9]{64}$/);
  assert.ok(fileURLToPath(carpeta).includes("rendimiento-candidatos"));
});
