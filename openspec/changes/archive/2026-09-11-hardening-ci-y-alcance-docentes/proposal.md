## Why

El análisis de Semgrep confirmó que el pipeline instala acciones y dependencias con controles de supply chain incompletos. Además, usuarios con ámbito acotado pueden recibir PII, asignaciones o membresías docentes de materias ajenas: el backend calcula el ámbito, pero algunos servicios sólo lo aplican después de construir respuestas con datos completos o únicamente al catálogo de materias.

El cambio corrige la frontera de autorización en el punto común de mapeo y fija el pipeline para que sus herramientas no puedan cambiar silenciosamente de contenido.

## What Changes

- Fijar cada referencia `uses:` de los workflows de GitHub Actions a un SHA completo de 40 caracteres.
- Agregar a `pnpm-workspace.yaml` `blockExoticSubdeps: true`, `minimumReleaseAge: 10080`, `trustPolicy: no-downgrade` y el override acotado de `semver` necesario para resolver su alerta de confianza; regenerar `pnpm-lock.yaml` si las resoluciones existentes no cumplen esas políticas, sin incorporar dependencias nuevas.
- Hacer que el catálogo de Designaciones devuelva a un actor acotado sólo personas y designaciones vigentes relacionadas con sus materias permitidas; no debe exponer PII de personas fuera de ese ámbito.
- Filtrar las asignaciones vigentes antes de mapear las respuestas de listado y detalle de docentes.
- Filtrar las membresías docentes antes de mapear las respuestas de listado y detalle de docentes, conservando el comportamiento global para usuarios con acceso departamental.
- Agregar pruebas de regresión con docentes que combinan una materia visible y otra ajena, y con personas fuera del ámbito del actor.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `pipeline-deploy-ci`: el pipeline debe consumir acciones y dependencias con referencias y políticas de instalación verificables.
- `listar-docentes`: listado y detalle deben filtrar asignaciones y membresías según el ámbito efectivo del actor, no sólo filtrar la existencia de la persona.
- `pedidos-designacion`: los catálogos canónicos deben limitar personas y asignaciones vigentes a las materias autorizadas para el actor.

## Impact

- **Backend:** `ServicioCatalogosDesignaciones`, `ServicioDocentes`, sus controladores sólo como frontera existente, y pruebas de integración de Designaciones/Administración.
- **CI y dependencias:** `.github/workflows/ci.yml`, workflows de deploy y ambientes efímeros, `pnpm-workspace.yaml` y `pnpm-lock.yaml` cuando la instalación requiera actualizar resoluciones existentes.
- **Contrato observable:** no cambian los tipos ni las rutas. Para actores globales la respuesta permanece completa; para actores acotados se eliminan datos fuera de ámbito y un detalle sin asignación visible continúa respondiendo como recurso no visible.
- **Arquitectura:** no se agregan referencias entre módulos ni dependencias nuevas; el grafo DAG permanece igual.
- **Rollback:** revertir el commit del cambio restaura el comportamiento anterior y las referencias del pipeline sin migraciones de base de datos.
