## Why

El job de backend ejecuta 148 tests de integración en Release y la fase de tests tarda aproximadamente 3m29s en el entorno de referencia. Quince clases comparten una única colección xUnit con PostgreSQL, aunque cada clase crea una base aislada; esa serialización evita aprovechar el paralelismo disponible. Además, los filtros de CI y deploy no son coherentes: el CI de backend puede ejecutarse por cambios exclusivos de pnpm, no se activa ante cambios en `database/`, y staging/PR pueden desplegar cambios sólo documentales pese a que sus comentarios indican lo contrario.

## What Changes

- Reorganizar el fixture de PostgreSQL para compartir el contenedor a nivel de assembly y conservar una base aislada por clase, permitiendo ejecutar en paralelo las clases independientes sin eliminar casos de prueba ni debilitar el aislamiento.
- Ajustar el filtro del job Backend para incluir `database/**`, excluir cambios exclusivos de pnpm y evitar trabajo para cambios documentales; mantener el filtro de Frontend para sus propias dependencias.
- Ajustar los triggers de deploy de staging y de entornos PR para que los cambios documentales no ejecuten builds ni despliegues, manteniendo gates, reset de base, secretos, tags y seguridad existentes.
- Agregar cache de paquetes NuGet con una clave basada en el SDK y los manifiestos de proyectos, con restore normal como fallback ante un miss.
- Medir el tiempo antes y después y documentar la nueva conducta. No se eliminan tests de negocio, no se paralizan builds de imágenes ni se agrega cache remoto Docker en este cambio.

## Capabilities

### New Capabilities

- Ninguna.

### Modified Capabilities

- `pipeline-deploy-ci`: agrega requisitos de ejecución eficiente y segura de la suite backend, cache de dependencias y filtrado preciso de CI/deploy sin alterar los gates ni el aislamiento de datos.

## Impact

- Workflows afectados: `.github/workflows/ci.yml`, `.github/workflows/deploy-staging.yml` y `.github/workflows/pr-env-deploy.yml`.
- Tests afectados: `backend/tests/ArsDocendi.IntegrationTests`, especialmente el fixture y las anotaciones de colección PostgreSQL.
- Documentación afectada: `docs/architecture/infrastructure.md` y el delta OpenSpec de `pipeline-deploy-ci`.
- No hay cambios de API pública, schema, contratos entre módulos ni grafo de dependencias; no hay consumidores cross-module que actualizar.
- El rollback consiste en revertir los cambios de workflow y restaurar la colección xUnit original. El cache es prescindible y un miss no debe bloquear el restore.
