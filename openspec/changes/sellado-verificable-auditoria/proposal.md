# Proposal

## Why

`audit.change_log` registra cambios transaccionales, pero quien controla PostgreSQL puede modificar datos e historial sin dejar una prueba verificable fuera de la misma infraestructura. Se necesita detectar alteraciones y retrocesos con un testigo independiente, sin desplegar una red blockchain ni publicar datos personales.

## What Changes

- Incorporar sellos criptográficos versionados de lotes cerrados de eventos de auditoría, con continuidad verificable y punto de inicio explícito para el historial preexistente.
- Publicar únicamente manifiestos sin contenido personal en un custodio externo protegido y su huella en un segundo testigo bajo administración independiente.
- Verificar periódicamente historial, sellos y una selección declarada de datos vigentes; alertar ante discrepancias, atrasos y fallos de publicación, sin declarar verificados los eventos pendientes.
- Separar identidades y permisos de API, sellador, custodio y verificador; preservar aislamiento de `prod` frente a `staging` y `pr-N`.
- Documentar restauración, rotación de claves, retención, despliegue gradual y límites frente al compromiso conjunto de todos los testigos.

## Capabilities

### New Capabilities

- `integridad-auditoria`: cierre, custodia, verificación y observabilidad de pruebas de integridad del historial auditado y del estado crítico declarado.

### Modified Capabilities

- Ninguna: la API administrativa existente, el DDL SQL versionado y la reconstrucción de ambientes no productivos conservan sus contratos; el nuevo comportamiento se especifica de manera transversal.

## Impact

- `database/audit/`, migraciones y modelo de auditoría en `ArsDocendi.Shared`; nuevo ejecutable/job de sellado y verificación y scripts de operación en `infra/`.
- Credenciales separadas del backend y de los runners de PR, un custodio externo y un testigo de huellas independiente; no se modifica el enrutamiento público ni se agrega una API HTTP.
- Documentación de modelo de datos, dependencias y operación/backup; pruebas de concurrencia, alteración, pérdida, rotación y restauración.
- El grafo de módulos de negocio no cambia: las lecturas de `audit` permanecen en infraestructura transversal; las integraciones externas pertenecen al ejecutable operativo, no a `ArsDocendi.Shared` ni a `*.Contracts`.
- Rollback operativo: detener la publicación/verificación sin borrar los sellos ni volver a declarar sano el período no verificado; revertir cambios de aplicación compatibles, conservar el DDL aditivo y reconciliar antes de reanudar.
