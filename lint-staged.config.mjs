/**
 * lint-staged config — Ars Docendi
 *
 * Cubre ambos stacks del monorepo:
 * - Backend .NET: dotnet format sobre archivos .cs staged
 * - Frontend React/Vite: eslint --fix + prettier --write
 * - Scripts (raíz): solo prettier (no hay eslint configurado fuera de frontend)
 * - Resto (md/json/yml/yaml/css/html): prettier --write
 *
 * lint-staged pasa los paths como relativos a la raíz del repo.
 */

// lint-staged separa el comando por espacios: cada path va entre comillas para que
// un nombre con espacios llegue como un solo argumento.
const rutas = (files) => files.map((f) => JSON.stringify(f)).join(" ");

export default {
  "backend/**/*.cs": (files) => `dotnet format backend/ArsDocendi.slnx --include ${rutas(files)}`,

  "frontend/**/*.{ts,tsx,js,jsx}": (files) => {
    // eslint corre dentro del workspace frontend; necesita paths relativos a frontend/
    const relative = files.map((f) => f.replace(/^frontend\//, ""));
    return [
      `pnpm --filter frontend exec eslint --fix ${rutas(relative)}`,
      `prettier --write ${rutas(files)}`,
    ];
  },

  // Scripts y configs Node/TS fuera de frontend (no tienen eslint configurado por ahora).
  "scripts/**/*.{ts,mjs,cjs,js}": (files) => `prettier --write ${rutas(files)}`,
  "*.{mjs,cjs,js}": (files) => `prettier --write ${rutas(files)}`,

  "**/*.{md,json,yml,yaml,css,html}": (files) => `prettier --write ${rutas(files)}`,
};
