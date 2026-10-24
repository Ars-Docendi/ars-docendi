// Falla si un .md versionado enlaza a un archivo relativo inexistente.
// Los changes archivados son históricos y no se revisan.
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";

const archivos = execFileSync("git", ["ls-files", "*.md"], { encoding: "utf8" })
  .split("\n")
  .filter((f) => f && !f.startsWith("openspec/changes/archive/"));

const rotos = [];
for (const archivo of archivos) {
  const texto = readFileSync(archivo, "utf8");
  for (const [, destino] of texto.matchAll(/\]\(([^)\s#]+)(?:#[^)]*)?\)/g)) {
    if (/^[a-z]+:/i.test(destino)) continue;
    if (!existsSync(resolve(dirname(archivo), decodeURI(destino)))) {
      rotos.push(`${archivo} -> ${destino}`);
    }
  }
}

if (rotos.length > 0) {
  console.error(`Links rotos (${rotos.length}):\n${rotos.join("\n")}`);
  process.exit(1);
}
console.log(`Links relativos OK en ${archivos.length} archivos.`);
