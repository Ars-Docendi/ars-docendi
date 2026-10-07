import { existsSync, writeFileSync } from "node:fs";
import { argumentos, leer, manifiestoRuta, principal, verificarRuta } from "./comun.mjs";
import { inventariar } from "./inventario.mjs";
import { comprobarArchivos, hashes, leerManifiesto, validarManifiesto } from "./proteccion.mjs";

principal(() => {
  const { opciones, posiciones, raiz } = argumentos(process.argv.slice(2), [
    "--root",
    "--clasificaciones",
  ]);
  if (opciones["--help"]) {
    console.log(
      "node scripts/migrations/manifest.mjs [--dry-run] [--clasificaciones archivo.json] [--root directorio]",
    );
    return;
  }
  if (posiciones.length) throw new Error("No se admiten argumentos posicionales");
  const destino = verificarRuta(raiz, manifiestoRuta);
  const anterior = existsSync(destino) ? leerManifiesto(raiz) : null;
  if (anterior) comprobarArchivos(raiz, anterior.archivos);
  const clasificados = opciones["--clasificaciones"]
    ? JSON.parse(leer(raiz, opciones["--clasificaciones"]))
    : (anterior?.clasificados ?? {});
  const inventario = inventariar(raiz, clasificados);
  const manifiesto = validarManifiesto({
    version: 1,
    archivos: hashes(raiz, inventario.archivos),
    clasificados,
  });
  if (anterior) {
    for (const ruta of Object.keys(anterior.archivos))
      if (!Object.hasOwn(manifiesto.archivos, ruta))
        throw new Error(`No se puede retirar protección: ${ruta}`);
    for (const [ruta, motivo] of Object.entries(anterior.clasificados))
      if (clasificados[ruta] !== motivo)
        throw new Error(`No se puede reescribir clasificación histórica: ${ruta}`);
  }
  const contenido = JSON.stringify(manifiesto, null, 2) + "\n";
  if (!opciones["--dry-run"]) writeFileSync(destino, contenido);
  console.log(contenido.trimEnd());
});
