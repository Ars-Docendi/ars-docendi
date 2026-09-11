## 1. Regresiones de autorización

- [x] 1.1 Agregar en `CatalogosDesignacionesTests` un caso para un actor acotado con una persona asignada a una materia visible y otra ajena, y otra persona sólo ajena; verificar primero que el test falla contra el comportamiento actual con `dotnet test backend/ArsDocendi.slnx --filter FullyQualifiedName~CatalogosDesignacionesTests`.
- [x] 1.2 Extender la prueba de acceso de Jefe de Cátedra para comprobar que listado y detalle sólo contienen asignaciones y membresías visibles, que una persona sólo ajena no aparece y que su detalle responde `404`; verificar primero el fallo con `dotnet test backend/ArsDocendi.slnx --filter FullyQualifiedName~AutenticacionDesarrolloTests`.

## 2. Corrección del alcance en backend

- [x] 2.1 Filtrar en `ServicioCatalogosDesignaciones` las personas y designaciones vigentes por las materias visibles del actor, manteniendo completas las respuestas globales; verificar que pasan las regresiones de `CatalogosDesignacionesTests`.
- [x] 2.2 Proyectar las asignaciones visibles antes de mapear listado y detalle en `ServicioDocentes`, y aplicar el mismo conjunto a `Membresias`; conservar el `404` para personas sin asignación visible y el comportamiento `null` global; verificar que pasan las regresiones de `AutenticacionDesarrolloTests` y `AdministracionDocentesTests`.

## 3. Hardening de CI y dependencias

- [x] 3.1 Reemplazar los tags mutables por los cinco SHA completos definidos en el diseño en todos los workflows de CI, deploy, ambientes PR y teardown; verificar que `rg -n '^\\s*uses:' .github/workflows` sólo muestra referencias `@[0-9a-f]{40}`.
- [x] 3.2 Declarar `blockExoticSubdeps: true`, `minimumReleaseAge: 10080` y `trustPolicy: no-downgrade` en `pnpm-workspace.yaml`, con el override acotado de `semver` requerido por la verificación; regenerar el lockfile sólo si las resoluciones existentes incumplen las políticas, sin incorporar dependencias nuevas; verificar la instalación con `pnpm install --frozen-lockfile`.

## 4. Verificación final

- [x] 4.1 Ejecutar `dotnet test backend/ArsDocendi.slnx` y confirmar que el conjunto completo de pruebas queda verde.
- [x] 4.2 Ejecutar `pnpm format:check` y `pnpm exec openspec validate --all --strict`; corregir cualquier formato o delta inválido antes de entregar el change.
- [x] 4.3 Revisar `git diff --check` y confirmar que no se modificaron DTOs, rutas, esquema de base de datos ni el grafo de dependencias.
