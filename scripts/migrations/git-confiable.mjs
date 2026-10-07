import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { manifiestoRuta, rutaSegura } from "./comun.mjs";
import { inventariar } from "./inventario.mjs";
import { comprobarArchivos, hashes, leerManifiesto } from "./proteccion.mjs";

function git(raiz, args, opciones = {}) {
  return execFileSync("git", ["-C", raiz, ...args], {
    stdio: ["ignore", "pipe", "pipe"],
    ...opciones,
  });
}
function revision(raiz, sha) {
  if (!/^(?:[a-f0-9]{40}|[a-f0-9]{64})$/.test(sha ?? ""))
    throw new Error("Referencia confiable inválida: se requiere SHA completo de commit");
  const resuelta = git(raiz, ["rev-parse", "--verify", `${sha}^{commit}`], {
    encoding: "utf8",
  }).trim();
  if (resuelta !== sha) throw new Error("La referencia confiable debe ser un commit exacto");
  git(raiz, ["merge-base", "--is-ancestor", sha, "HEAD"]);
  return sha;
}
function tieneManifiesto(raiz, sha) {
  return (
    git(raiz, ["ls-tree", "--name-only", sha, "--", manifiestoRuta], {
      encoding: "utf8",
    }).trim() === manifiestoRuta
  );
}
export function comprobarGit(raiz, base, actual, corte) {
  revision(raiz, base);
  let confiable = base;
  if (!tieneManifiesto(raiz, base)) {
    if (!corte)
      throw new Error(
        "La base confiable no contiene manifiesto. El corte inicial exige un commit revisado explícito (--corte-revisado SHA), nunca un inventario mutable.",
      );
    confiable = revision(raiz, corte);
    git(raiz, ["merge-base", "--is-ancestor", base, confiable]);
    if (!tieneManifiesto(raiz, confiable))
      throw new Error("El commit del corte revisado no contiene inventario protegido");
  } else if (corte)
    throw new Error(
      "El corte revisado sólo se admite una vez, cuando la base aún no tiene manifiesto",
    );
  const temporal = mkdtempSync(join(tmpdir(), "inventario-git-"));
  try {
    const entradas = git(
      raiz,
      ["ls-tree", "-r", "-z", confiable, "--", "database", "backend/src"],
      { encoding: "utf8" },
    )
      .split("\0")
      .filter(Boolean);
    for (const entrada of entradas) {
      const [cabecera, ruta] = entrada.split("\t");
      const [modo, tipo, objeto] = cabecera.split(" ");
      const relevante =
        ruta === manifiestoRuta ||
        (ruta.startsWith("database/") && ruta.endsWith(".sql")) ||
        (ruta.includes("/Migrations/") && ruta.endsWith(".cs"));
      if (!relevante) continue;
      if (!rutaSegura(ruta) || tipo !== "blob" || !["100644", "100755"].includes(modo))
        throw new Error(`Entrada Git insegura: ${ruta}`);
      const destino = join(temporal, ruta);
      mkdirSync(dirname(destino), { recursive: true });
      writeFileSync(destino, git(raiz, ["cat-file", "blob", objeto]));
    }
    const historico = leerManifiesto(temporal);
    comprobarArchivos(temporal, historico.archivos);
    // Protege también adiciones publicadas tras congelar el manifiesto; no exige
    // actualizar una versión global manual para cada migración incremental.
    const inventario = inventariar(temporal, historico.clasificados);
    const protegidos = { ...historico.archivos, ...hashes(temporal, inventario.archivos) };
    comprobarArchivos(raiz, protegidos);
    for (const [ruta, suma] of Object.entries(historico.archivos)) {
      if (actual.archivos[ruta] !== suma)
        throw new Error(`No se puede retirar o reescribir protección histórica: ${ruta}`);
    }
    for (const [ruta, motivo] of Object.entries(historico.clasificados)) {
      if (actual.clasificados[ruta] !== motivo)
        throw new Error(`Clasificación histórica alterada: ${ruta}`);
    }
    return confiable;
  } finally {
    rmSync(temporal, { recursive: true, force: true });
  }
}
