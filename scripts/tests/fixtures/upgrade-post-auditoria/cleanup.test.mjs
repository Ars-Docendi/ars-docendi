import assert from "node:assert/strict";
import { test } from "node:test";
import { crearHarness } from "./harness.mjs";

test(
  "cleanup comprueba el volumen anónimo PostgreSQL y no confunde errores con inexistencia",
  {
    skip: process.env.UPGRADE_DOCKER_TESTS !== "1",
  },
  () => {
    const h = crearHarness();
    const nombre = `${h.prefijo}-cleanup`;
    h.docker("create", "--name", nombre, "--label", `ars-upgrade=${h.uid}`, "postgres:18-alpine");
    h.registrar("container", nombre);
    h.limpiar();
    assert.ok(h.evidencia.limpieza.some((r) => r.tipo === "volume-anonimo" && r.ausente));
  },
);
