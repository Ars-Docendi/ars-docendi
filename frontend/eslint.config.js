import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
  globalIgnores(["dist"]),
  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
    },
  },
  {
    // La composición no alcanza el interior de una feature.
    //
    // `app/` compone: monta rutas y arma el shell. Todo lo que necesita de una
    // feature entra por su frontera —`<feature>` o `<feature>/routes`— y nada
    // más. Un import a `<feature>/components/…` ata el shell a la organización
    // interna de la feature: cualquier reorganización de esa carpeta lo rompe, y
    // la feature deja de poder mover sus archivos sin mirar quién la espía.
    //
    // Era convención tácita: ocho de las nueve features ya se importaban así, y
    // la novena no. Como regla, deja de depender de que alguien se acuerde.
    files: ["src/app/**/*.{ts,tsx}"],
    rules: {
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              group: ["**/features/*/**", "!**/features/*/routes"],
              message:
                "La composición entra por la frontera de la feature: importá de " +
                "`features/<x>` o `features/<x>/routes`, nunca de su interior.",
            },
          ],
        },
      ],
    },
  },
]);
