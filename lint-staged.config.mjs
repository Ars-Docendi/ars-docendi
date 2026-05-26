/**
 * lint-staged config — Ars Docendi
 *
 * Cubre ambos stacks del monorepo:
 * - Backend .NET: dotnet format sobre archivos .cs staged
 * - Frontend React/Vite: eslint --fix + prettier --write
 * - Resto (md/json/yml/yaml/css): prettier --write
 *
 * lint-staged pasa los paths como relativos a la raíz del repo.
 */
export default {
  "backend/**/*.cs": (files) =>
    `dotnet format backend/ArsDocendi.slnx --include ${files.join(" ")}`,

  "frontend/**/*.{ts,tsx,js,jsx}": (files) => {
    // eslint corre dentro del workspace frontend; necesita paths relativos a frontend/
    const relative = files.map((f) => f.replace(/^frontend\//, ""));
    return [
      `pnpm --filter frontend exec eslint --fix ${relative.join(" ")}`,
      `prettier --write ${files.join(" ")}`,
    ];
  },

  "**/*.{md,json,yml,yaml,css,html}": (files) => `prettier --write ${files.join(" ")}`,
};
