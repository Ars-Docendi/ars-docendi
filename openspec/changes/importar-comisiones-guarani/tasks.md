## 1. Contrato de Guaraní

- [ ] 1.1 Recibir los dos endpoints (comisiones por período y períodos lectivos) con docentes, propuestas y bandas como listas, y guardar respuestas de ejemplo con datos ocultados como fixtures JSON en `backend/tests/ArsDocendi.IntegrationTests/Designaciones/Guarani/`; verificar que los documentos de los fixtures son ficticios y no hay CUIL ni correos
- [ ] 1.2 Registrar en `design.md` los paths confirmados (cierra las Open Questions) y verificar que cada campo de la spec `importar-comisiones-guarani` tiene origen identificado

## 2. Datos y permisos

- [x] 2.1 Generar con `node scripts/migrations/scaffold.mjs Designaciones ComisionesGuarani` la migración incremental (SQL en `database/comisiones/`, schema propio `comisiones`) y completar su SQL con `sga_importaciones` (índice único parcial `en_curso`, contadores de no vinculados, `audit.attach`), `sga_comisiones` (PK `comision_guarani_id`, índice por período) y sus hijas `sga_comision_carreras`, `sga_comision_docentes` (índice por `persona_id`) y `sga_comision_horarios` (`CHECK` de día y horario), todas con `ON DELETE CASCADE` y sin `audit.attach`; verificar con `dotnet ef migrations has-pending-model-changes` y aplicando `--migrate` sobre una base local
- [x] 2.2 Generar con `node scripts/migrations/scaffold.mjs Identity PermisosComisiones` la migración incremental de permisos `comisiones.ver` (`b2000000-…-033`) y `comisiones.importar` (`-034`) con sus `rol_permisos` (con `ON CONFLICT`, como `012_identity_permisos_tareas.sql`) y las constantes/políticas en `ArsDocendi.Shared/Auth/Permisos.cs`; verificar con un test de integración que cada rol de sistema recibe exactamente los permisos del design
- [x] 2.3 Mapear las cinco entidades en `DesignacionesDbContext` con `ExcludeFromMigrations()`; verificar que el test de modelo sin cambios pendientes sigue pasando

## 3. Lecturas de identidad para vincular

- [x] 3.1 Agregar `BuscarPersonasPorDocumentosAsync` (y la lectura de carreras si no existe) a `IConsultasIdentity` y su implementación en Shared, normalizando documentos; verificar con tests de integración: coincidencia, documento inexistente, documentos con puntos y lista vacía, y que ningún consumidor existente cambia (`dotnet build`)

## 4. Cliente de Guaraní

- [x] 4.1 Agregar `OpcionesGuarani` y la sección `Guarani` (valores vacíos) a `appsettings.json`; verificar que el Host arranca sin la sección completa
- [x] 4.2 Implementar `ClienteGuarani` (HttpClient tipado, usuario/contraseña, timeout, respuesta única sin paginación, mapeo a registros propios con documento normalizado, traducción de errores a causas genéricas); verificar con tests que usan un `HttpMessageHandler` falso y los fixtures de 1.1: mapeo completo, campos faltantes vacíos, 401 → `autenticacion`, timeout → `timeout`, JSON inválido → `respuesta_invalida`, y que ningún log contiene credenciales ni documentos

## 5. Servicio y API

- [x] 5.1 Implementar `RepositorioComisiones` (reserva, reemplazo por período en una transacción sobre las cuatro tablas, cierre exitoso/fallido con contadores, caducidad de reservas, listado y períodos importados); verificar con tests de integración que reimportar un período no toca otro, que un fallo deja el período intacto y que una comisión de otro período en el lote falla sin reemplazar
- [x] 5.2 Implementar `ServicioComisiones` con las tres fases, la validación del lote y la vinculación de docentes, carreras y materias; verificar con tests los escenarios de la spec `importar-comisiones-guarani`: reemplazo, Guaraní falla, lista vacía, 409 concurrente, 503 no configurado, docente vinculado / sin vincular / vinculado al reimportar, y que el documento no se persiste en ninguna tabla
- [x] 5.3 Implementar `ComisionesController` con los cuatro endpoints y sus políticas; verificar con tests de integración 403 sin permiso, 200 con permiso y que los GET de consulta no invocan al cliente de Guaraní
- [x] 5.4 Registrar `HttpClient`, servicio y repositorio en `ModuleExtensions.cs`; verificar con `dotnet test backend/ArsDocendi.slnx`

## 6. Frontend

- [x] 6.1 Crear `api/comisionesApi.ts`, tipos y `hooks/useComisiones.ts` (invalidación tras importar); verificar con tests de la capa API
- [x] 6.2 Crear `TablaComisiones` y `filtrosComisiones.ts` sobre los componentes de grilla compartidos (bandas como día + rango, docentes con su responsabilidad, modalidad por descripción, carreras con código, "—" en vacíos, sin acciones por fila); verificar con tests de render y de filtros por materia, carrera, docente y modalidad
- [x] 6.3 Crear `ModalImportarComisiones` (año → períodos de Guaraní → confirmar; botón deshabilitado sin período; mensajes para 409/502/503/504; resumen con docentes sin vincular); verificar con tests de interacción
- [x] 6.4 Crear `ComisionesPage` (selector de períodos importados, default el más reciente, estado vacío, "Actualizado el … por …", aviso de último intento fallido, botón sólo con `comisiones.importar`), la ruta con `RequirePermission` y la entrada en `nav.ts`; verificar con tests de página y de navegación por permisos
- [x] 6.5 Verificar `pnpm --filter frontend test:run`, `lint` y `build`

## 7. Integración con el asistente

Fuera del alcance de este change: la resuelve la rama del asistente (`feature/asistente-modelo-local`) al integrarse, siguiendo la decisión 10 del design (exponer el schema `comisiones`, clasificar las tablas en su manifiesto, RLS por `comisiones.ver` y `COMMENT ON`).

## 8. Datos de desarrollo

- [x] 8.1 Agregar a `infra/scripts/seed-data/sintetico.sql` una importación exitosa con algunas comisiones ficticias (con docentes vinculados y uno sin vincular) para un período, para ver la pantalla en dev, staging y efímeros sin Guaraní; verificar que no contienen datos reales

## 9. Documentación

- [x] 9.1 Actualizar `docs/architecture/domains/designaciones.md` (entidades, endpoints, dependencia externa y la decisión revisada sobre Guaraní), `api-contracts-designaciones.md`, `data-model.md` (cinco tablas, FKs nullable `SET NULL` hacia identity, PII parcial y excepción de auditoría), `dependency-graph.md` (nueva lectura de `IConsultasIdentity`), `infrastructure.md` (secretos `Guarani__*`, sólo producción) y el README del módulo; verificar con `pnpm format:check`
- [x] 9.2 Verificar `pnpm exec openspec validate --all --strict`
