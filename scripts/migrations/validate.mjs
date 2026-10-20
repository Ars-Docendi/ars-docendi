import { argumentos, principal } from "./comun.mjs";
import { inventariar } from "./inventario.mjs";
import { comprobarArchivos, leerManifiesto } from "./proteccion.mjs";
import { comprobarGit } from "./git-confiable.mjs";

principal(() => {
  const { opciones, posiciones, raiz } = argumentos(process.argv.slice(2), [
    "--root",
    "--base-ref",
    "--corte-revisado",
  ]);
  if (opciones["--help"]) {
    console.log(
      "node scripts/migrations/validate.mjs [--base-ref SHA] [--corte-revisado SHA] [--root directorio]",
    );
    return;
  }
  if (posiciones.length || opciones["--dry-run"]) throw new Error("Argumentos no admitidos");
  if (opciones["--corte-revisado"] && !opciones["--base-ref"])
    throw new Error("El corte revisado requiere --base-ref");
  const manifiesto = leerManifiesto(raiz);
  const revision = opciones["--base-ref"]
    ? comprobarGit(raiz, opciones["--base-ref"], manifiesto, opciones["--corte-revisado"])
    : null;
  comprobarArchivos(raiz, manifiesto.archivos);
  const inventario = inventariar(raiz, manifiesto.clasificados);
  console.log(
    JSON.stringify(
      {
        migraciones: inventario.migraciones.length,
        archivos: inventario.archivos.length,
        proteccionGit: !!revision,
        revisionConfiable: revision,
      },
      null,
      2,
    ),
  );
});
