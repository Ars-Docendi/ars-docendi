## Context

La frontera de ámbito ya se resuelve en `DocentesController` y `ServicioCatalogosDesignaciones`: los actores acotados reciben un conjunto de materias visibles. El problema es que `ServicioDocentes` mapea las asignaciones y membresías completas antes de filtrar la persona, y el catálogo de Designaciones sólo usa el ámbito para la lista de materias. Ver `proposal.md` y las deltas de `listar-docentes` y `pedidos-designacion` para el contrato resultante.

Los workflows comparten cinco acciones externas (`actions/checkout`, `dorny/paths-filter`, `pnpm/action-setup`, `actions/setup-node` y `actions/setup-dotnet`) mediante tags mutables. `pnpm-workspace.yaml` sólo declara el paquete frontend, por lo que las políticas de supply chain deben agregarse en el mismo nivel raíz.

## Goals / Non-Goals

**Goals:**

- Aplicar el mismo conjunto de materias visibles a personas, asignaciones y membresías antes de construir DTOs acotados.
- Mantener `null` como señal de alcance global y conservar la respuesta completa para Secretaría, Decanato, Administración y otros usuarios con lectura global.
- Mantener el `404` del detalle cuando una persona no tiene ninguna asignación visible.
- Fijar acciones y políticas de instalación sin agregar dependencias, migraciones ni referencias entre módulos.
- Cubrir cada fuga con pruebas de integración reproducibles.

**Non-Goals:**

- No cambiar permisos, roles, forma de autenticación ni la derivación del ámbito en el controlador.
- No modificar DTOs, rutas HTTP, frontend ni la semántica de escritura de docentes.
- No introducir consultas nuevas, cachés, abstracciones de autorización o un mecanismo general de filtrado.
- No incorporar dependencias nuevas ni bloquear la dependencia GitHub directa ya declarada como dependencia principal.

## Decisions

### 1. Proyectar el alcance antes del mapeo

En `ServicioDocentes`, derivar una colección de asignaciones visibles a partir de `materiasVisibles` y usarla tanto para agrupar el listado como para mapear el detalle. Pasar el mismo alcance al mapeo de membresías. Cuando el alcance sea `null`, usar las colecciones completas; cuando sea un conjunto vacío, no devolver docentes ni membresías acotadas.

Esto corrige la causa común de los findings 963789702 y 963789703 y evita que filtros posteriores puedan dejar en el DTO una asignación o membresía que ya no corresponde. Se mantiene la firma pública existente y no se agrega un servicio de autorización de una sola implementación.

Alternativa descartada: filtrar sólo en `DocentesController` o en el frontend. Dejaría otras llamadas al servicio expuestas y no protegería la respuesta HTTP.

### 2. Aplicar el alcance al catálogo de Designaciones

En `ServicioCatalogosDesignaciones`, conservar la resolución actual del actor y usar `visibles` para dos decisiones: incluir una persona sólo si tiene al menos una designación vigente en una materia visible, y proyectar únicamente esas designaciones. Los actores departamentales seguirán usando todas las materias activas y todas las personas elegibles para el catálogo global.

Alternativa descartada: traer sólo nombres de materias visibles y confiar en que el frontend filtre `personas`. El catálogo ya contiene documento, legajo y asignaciones; esos datos no deben salir del backend.

### 3. Pines explícitos y políticas pnpm declarativas

Reemplazar los tags actuales por los commits que corresponden a las versiones en uso:

| Acción                    | Referencia a fijar                         |
| ------------------------- | ------------------------------------------ |
| `actions/checkout@v6`     | `d23441a48e516b6c34aea4fa41551a30e30af803` |
| `dorny/paths-filter@v4`   | `ceb8a2b8f2d89434be7ff52d3de7ec3738c5cc9d` |
| `pnpm/action-setup@v6`    | `0977fd99725f1db4007ccb2928dbb4e90d06cc86` |
| `actions/setup-node@v6`   | `249970729cb0ef3589644e2896645e5dc5ba9c38` |
| `actions/setup-dotnet@v5` | `26b0ec14cb23fa6904739307f278c14f94c95bf1` |

Usar los mismos pines en CI, deploy-staging, deploy-prod, deploy de PR y teardown. Declarar las tres políticas Semgrep en `pnpm-workspace.yaml`, junto con `packages`. La resolución `semver@6.3.1` que requieren paquetes de Babel activa una alerta de confianza; se fija sólo ese rango declarado en `6.3.0`, que conserva la misma API de la línea 6, y se regenera `pnpm-lock.yaml` sin incorporar dependencias nuevas.

Alternativa descartada: fijar sólo los workflows de CI. Los workflows de deploy y teardown también ejecutan código con permisos y secretos, por lo que dejar uno mutable conserva la exposición.

### 4. Regresión en los límites existentes

Ampliar las pruebas de integración de catálogo y autenticación de desarrollo con una persona que tenga una materia visible y otra ajena, y otra persona sólo ajena. Las aserciones deben comprobar la ausencia de cada `MateriaId` no visible en asignaciones y membresías, la ausencia de la persona sólo ajena y el `404` del detalle. Agregar una aserción para el catálogo de Designaciones que verifique tanto la lista de personas como sus designaciones.

Alternativa descartada: una prueba unitaria del método de mapeo aislado. No comprobaría la autorización completa controller → service → datos sintéticos ni la diferencia entre actor global y acotado.

## Risks / Trade-offs

- **[Riesgo]** Un consumidor acotado puede mostrar menos badges de asignaciones o membresías que antes. → **Mitigación:** es el comportamiento requerido; conservar el DTO y cubrirlo con pruebas de contrato/integración.
- **[Riesgo]** `minimumReleaseAge` demora la adopción de una versión urgente. → **Mitigación:** actualizar de forma explícita una versión ya madura y revisar la política en un cambio separado si la operación exige una excepción.
- **[Riesgo]** Un SHA puede quedar obsoleto cuando se publique una nueva versión de una acción. → **Mitigación:** documentar la versión en el comentario o nombre existente del paso y actualizar sólo mediante un cambio revisado que vuelva a fijar el SHA.
- **[Riesgo]** La lista de acciones fijadas queda incompleta si aparece un nuevo workflow. → **Mitigación:** revisar todos los `.github/workflows/*.yml` en la prueba/revisión del cambio y mantener el requisito global de referencias de 40 caracteres.

## Migration Plan

1. Aplicar el filtro de datos y las pruebas de regresión.
2. Agregar las políticas de pnpm y reemplazar todos los pines de acciones; ejecutar validación OpenSpec y los checks proporcionales de backend y CI.
3. Desplegar normalmente. No hay migración de base de datos ni cambio de esquema.
4. Para rollback, revertir el commit completo; la reversión no requiere restaurar datos ni ejecutar scripts compensatorios.
