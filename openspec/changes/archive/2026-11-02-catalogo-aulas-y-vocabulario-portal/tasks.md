## 1. Aulas

- [x] 1.1 Migración `aulas`: catálogo, `aula_id`, CHECK y exclusión de superposición; entidad, mapeo y snapshot EF. Verificar: `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`, `--validar-recursos`.
- [x] 1.2 Migración `identity`: descripción de `aulas.gestionar`.
- [x] 1.3 API del catálogo (`api/aulas/catalogo`) y asignación por `aulaId` con 409 `aula-ocupada`. Verificar: tests de integración de superposición, concurrencia, aula inactiva, capacidad excedida, permisos y aula en uso.
- [x] 1.4 Frontend: pantalla Catálogo de aulas y selector con advertencia de capacidad. Verificar: Vitest.

## 2. Portal

- [x] 2.1 Migración `portal` con el vocabulario base y migración `identity` con `portal.vocabulario.administrar`.
- [x] 2.2 API de vocabulario y curaduría; `ReemplazarTagsAsync` resuelve términos fusionados. Verificar: tests de integración de fusión, renombre en conflicto, resolución al canónico y permisos.
- [x] 2.3 Frontend: selector desde la API y pantalla de curaduría. Verificar: Vitest.

## 3. Designaciones vigentes en Mi Portal

- [x] 3.1 `GET /api/designaciones/vigentes/mias`. Verificar: test de integración que sólo devuelve las del usuario.
- [x] 3.2 Sección "Designaciones vigentes" en Mi Portal. Verificar: Vitest.

## 4. Documentación y cierre

- [x] 4.1 `docs/business-rules/aulas.md` y `portal.md` con mapping a tests; tags `BR-*` en specs y tests existentes.
- [x] 4.2 Docs de dominio, contratos de API, modelo de datos, design specs y Centro de ayuda.
- [x] 4.3 Verificación completa (migraciones, build, `--validar-recursos`, tests backend y frontend, lint, build, formato, links, `openspec validate --all`).
