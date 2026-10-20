# Evidencia y límites de la auditoría

## Procedencia

- Commit auditado: `f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b`, rama `feature/seed-data`.
- Reconstrucción estructural de los SQL declarados por nueve wrappers en PostgreSQL 18.4 desechable, sin red ni puertos publicados. No se inspeccionó el catálogo de bases desplegadas ni se leyó PII.
- Catálogo de aplicación: 39 tablas, 50 FKs, 45 CHECKs, 109 índices y 34 triggers no internos. Excluye histories EF y metadata de bootstrap/seed.
- Fuente completa de trabajo: `.artifacts/auditoria-db/informe.md`, `catalogo.json`, `reproducciones.json`, `planes-rendimiento.json`, `consulta-portal-ef.sql` y `plan-portal-ef.json`. Estos archivos son artefactos locales ignorados: el cambio no depende de que sobrevivan; este resumen conserva la evidencia mínima para implementar tests permanentes.
- Se realizaron doce probes: once finalizaron sin error y el de cambio intradía produjo el error esperado `23P01`. El contenedor fue eliminado y su ausencia verificada.

## Resultados ejecutados

| Probe                              | Resultado observado                                          | Límite                                                    |
| ---------------------------------- | ------------------------------------------------------------ | --------------------------------------------------------- |
| Numeración                         | Secuencias 1000 y 10000 → `2026-1000` en ambos casos         | Función real; no creación HTTP                            |
| Materia inexistente en designación | Inserción aceptada                                           | SQL; servicio administrativo sí valida materia            |
| Persona inexistente en perfil      | Inserción aceptada                                           | SQL; no alta de API demostrada                            |
| UPN equivalente                    | Dos cuentas coexistieron bajo igualdad `lower(upn)`          | Alta administrativa sí normaliza                          |
| Scope modificado                   | Asignación viva sin materia tras global→materia              | API permite editar ámbito; probe SQL                      |
| Usuario inexistente en tarea       | Inserción aceptada                                           | Referencia lógica deliberada; servicio valida responsable |
| Resuelta sin solución              | Update aceptado                                              | API exige solución; garantía retirada del DDL             |
| Ciclo A↔B                          | Update aceptado                                              | API no expone reparenting; brecha SQL                     |
| PK parcial de auditoría            | Dos permisos diferentes, un `row_pk`                         | Historia de grupo legacy, no identidad precisa            |
| Cambio intradía                    | EXCLUDE rechazó intervalos solapados con `23P01`             | Reproduce escrituras del materializador, no circuito HTTP |
| Alta incompleta en revisión        | Cargo/horas NULL aceptados                                   | Validación tardía corroborada en servicio                 |
| Perfil multicolección              | SQL emitido por EF produjo 216 filas para 15 elementos hijos | Fixture 3×3×3×2×4; no tiempo productivo                   |

**Corrección al interpretar la jerarquía:** proyecto NULL heredado de un padre también NULL es válido según la spec actual. No imponer proyecto obligatorio a una hija por ese probe. El ciclo o la discrepancia con el padre son las propiedades a prevenir.

## Rendimiento observado

Sobre 2.000 perfiles y 20.000 experiencias, una consulta por perfil devolvió 10 filas:

- Sin índice: Seq Scan, 19.990 descartadas, 286 bloques shared hit, 1,806 ms.
- Con índice experimental por `perfil_id`: Bitmap Heap Scan, 0 descartadas, 10 bloques shared hit, 0,376 ms.

Una ejecución por caso; los tiempos no definen SLA ni factor de aceleración. Las pruebas de implementación deben registrar repeticiones, SQL real y costo de escritura. El escáner inicial de FK sin índice no incluye cobertura parcial/GiST suficiente y no es una lista autorizada de índices a crear.

## Hallazgos pendientes de reproducción concurrente

A01 carga tardía frente a antivirus; A02 borrado/reemplazo de archivo referenciado; A05 propagación de proyecto; A06 hijas en proyecto cerrado; A11 autorización y updates sobre estado obsoleto; A12 carreras de unicidad. Se verificaron caminos de código, pero no se ejecutaron esas intercalaciones HTTP. La implementación deberá construir pruebas sincronizadas RED antes de afirmar corrección.

## Decisiones del usuario para este change

1. Cambio intradía: actualizar la misma vigencia iniciada ese día, conservando trazabilidad y origen anterior/nuevo en auditoría.
2. Referencias intermodulares: mantener las deliberadamente lógicas, con validación coordinada, protección de borrado y diagnóstico. La FK simple de Designacion a materia sí se incorpora.

Ningún resultado de auditoría autoriza saneamiento destructivo de datos reales, purga de eventos, cambios de normativa o despliegue automático.
