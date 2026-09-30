# Operación del sellado verificable (implementación local, no habilitada en prod)

## Estado y gates de activación

Los tests usan PostgreSQL 18 descartable y testigos HTTP en memoria con RSA real.
**No demuestran retención inmutable, independencia administrativa ni entrega de
alertas externas. No activar el timer productivo con esta sola evidencia.**

Implementado y ejercitado:

- Cierre por cursor transaccional; reintentos de firma/publicación; rechazo de
  alteración de un lote ya completo antes de publicar su sucesor. Verificación
  y cierre usan el mismo snapshot. El coste actual crece con todo el histórico
  local porque el cierre vuelve a verificar la cadena completa.
- Números JSON normalizados sin conversión a `double`/`decimal`: no se pierden
  dígitos grandes ni valores pequeños. Vector SHA-256 contrastado con Python.
  El formato v1 sigue en desarrollo: **no reutilizar sellos experimentales
  anteriores al cambio numérico como si fueran compatibles**. No se ha migrado
  ninguna base persistente en esta ejecución.
- Verificador de historia local y ancla remota; digest firmado RSA-PSS/SHA-256
  con clave pública fijada por el operador, nunca obtenida del custodio o DB.
- Inventario inicial deliberadamente parcial: `identity.roles`, todas sus
  columnas, clave `id`, soft-delete `is_active`. No cubre otras tablas ni bytes
  de adjuntos. El snapshot privado se persiste en el lote y su digest va dentro
  del manifiesto firmado; al verificar se reproducen eventos posteriores y se
  coteja el estado completo. No enviar el snapshot al firmador ni a los testigos.
- Sonda de frescura sin acceso DB; ausencia/fallo de evidencia implica salida 2.
- Digest del histórico legacy observado al corte, incluido en el primer
  manifiesto y en sucesores. `seal_baseline.status` cambia a `anchored` sólo tras
  ambos acuses; no afirma autenticidad antes de ese anclaje.
- Heartbeat firmado de rango vacío cuando no hay eventos, sin inventar cambios.
- Verificación de cada firma local histórica y de transiciones de clave v1→v2
  autorizadas mediante firma RSA-PSS de la clave previa sobre la huella pública
  nueva, ambiente e IDs. La retención de ambas claves públicas sigue obligatoria.
- Ensayo de `pg_dump -Fc`/`pg_restore` por stream entre bases Testcontainer
  descartables; el testigo GET-only más reciente detecta el backup atrasado.

- El job `--sellar-auditoria` exige lectura GET de ambos testigos y claves
  públicas fijadas **antes de preparar/publicar**. Sólo admite génesis si no
  hay lotes locales, el baseline está `observed` y ambos testigos responden 404.
  Con historial compara hash/rango/cursor; permite reintentar un acuse parcial,
  pero un retroceso, desaparición o divergencia impide publicar. Se ejercitó
  sobre un `pg_restore` atrasado: el publicador no llegó a enviar un POST.
  La garantía depende de retención real y respuestas `latest`/404 honestas.

Faltantes **de implementación**, además del aprovisionamiento externo:

1. El inventario institucional completo y su frecuencia deben acordarse; sólo
   `identity.roles` tiene checkpoint anclado al manifiesto de lote. Los roles
   iniciales sin evento se incluyen en el snapshot observado, sin inferir que
   eran correctos antes del primer anclaje.
2. Publicación/retención independiente del documento de transición firmado y
   runbook de revocación del firmador real. El verificador local puede validar
   transiciones fijadas por el operador, pero no las aprovisiona.

Gates externos: custodios de dos administraciones, firmador separado, políticas
inmutables reales, scopes por ambiente, revisión de owners/ACL en prod, benchmark
representativo y SLO aprobado, host externo, canal de alertas y rotación aprobada.

## Jobs y configuración

Desde el directorio publicado del backend, sin abrir un listener:

```sh
dotnet ArsDocendi.Host.dll --verificar-auditoria
dotnet ArsDocendi.Host.dll --verificar-restauracion
dotnet ArsDocendi.Host.dll --verificar-estado-auditoria
dotnet ArsDocendi.Host.dll --sondear-auditoria
```

Los dos primeros sólo leen PostgreSQL y ambos testigos. El inventario de estado
retorna **2 siempre** mientras no tenga ancla independiente: un digest observado
no debe convertirse en un resultado verde. La sonda no requiere conexión DB.
La salida 0 de verificación de historia **no** certifica estado de toda la base.
No combinar con `--migrate` ni con `--sellar-auditoria`.

Configuración .NET (usar `__` en variables de ambiente):

| Clave                                                                                      | Uso                                                                                                                                                                                                                                    |
| ------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:AuditVerify`                                                            | Login DB dedicado, sólo SELECT; no reutilizar migrador/API/sellador                                                                                                                                                                    |
| `AuditoriaVerificacion:Ambiente`                                                           | Dominio exacto del manifiesto: prod, staging o pr-N                                                                                                                                                                                    |
| `AuditoriaVerificacion:PrimarioEndpoint` / `SecundarioEndpoint`                            | GET HTTPS del último checkpoint del ambiente; sin redirects                                                                                                                                                                            |
| `AuditoriaVerificacion:PrimarioToken` / `SecundarioToken`                                  | Credenciales distintas, scopes exclusivamente de lectura                                                                                                                                                                               |
| `AuditoriaVerificacion:ClavesPublicasPem:<id>`                                             | Claves públicas RSA >=2048 bits previamente aprobadas                                                                                                                                                                                  |
| `AuditoriaVerificacion:Transiciones:<n>:{Ambiente,IdAnterior,IdNuevo,FirmaAnteriorBase64}` | Transición firmada por la clave vieja sobre SHA-256 del JSON UTF-8 con dominio `ars-docendi:transicion-clave-auditoria:v1`, ambiente, IDs y huella SHA-256 hex de SubjectPublicKeyInfo DER de la clave nueva; preservar fuera de la DB |
| `AuditoriaVerificacion:DesdeCursor`                                                        | Opcional, sólo cotejo de filas tocadas después del cursor; omitir para scan completo del inventario                                                                                                                                    |
| `AuditoriaVerificacion:ReporteLocalRecibidoEn`                                             | Fecha de recepción registrada por el colector **externo**, no un heartbeat autodeclarado por la VM                                                                                                                                     |
| `AuditoriaVerificacion:ReporteLocalValido`                                                 | Resultado local recibido explícitamente válido; ausente = false                                                                                                                                                                        |
| `AuditoriaVerificacion:MaximoAtrasoSegundos`                                               | Obligatorio para sonda, positivo y acordado por operaciones                                                                                                                                                                            |

Para `--sellar-auditoria`, además de `AuditSeal` y los endpoints de escritura,
`compose.audit-seal.yml` exige `AUDIT_SEAL_WITNESS_PRIMARY_READ_URL/TOKEN`,
`AUDIT_SEAL_WITNESS_SECONDARY_READ_URL/TOKEN` y `AUDIT_SEAL_PUBLIC_KEYS_JSON`
(mapa ID→PEM público con saltos de línea escapados en JSON). Para rotaciones,
`AUDIT_SEAL_KEY_TRANSITIONS_JSON` contiene las transiciones firmadas. Los tokens
lectores no son los de escritura ni los del otro custodio. Sin cualquiera de
estos valores obligatorios, el job no prepara ni publica lotes.

Mantener secretos fuera del repo, imagen, salida de `compose config` y runners.
Las excepciones de los jobs sólo registran su tipo; no imprimen conexión/cuerpo
HTTP. Los logs de estado son agregados, no contienen claves de fila ni snapshots.
El proceso debe ser supervisado: una caída antes de emitir reporte también alerta.

## Contrato de lectura de testigos (requiere adaptación del proveedor)

GET autenticado, JSON camelCase. Ambos retornan `ambiente`, `primeraSecuencia`,
`ultimaSecuencia`, `hashManifiesto` hexadecimal SHA-256 y `registradoEn` UTC.
El primario agrega `idClaveFirma`, `firmaBase64`, `manifiestoBase64`; el secundario
puede omitir el manifiesto. La firma RSA-PSS/SHA-256 es sobre el hash SHA-256 de los
bytes exactos del manifiesto. No se acepta una clave pública en la respuesta.
Los metadatos de rango/ambiente se cotejan contra los bytes firmados.

`registradoEn` procede del custodio autenticado por TLS; **no es un timestamp
notarial ni queda firmado por el formato v1 actual**. El hash de ambos testigos
ha de coincidir. Testigo ausente, error HTTP, firma inválida, clave desconocida,
ambiente distinto o hash distinto => NO VERIFICADO. El adaptador de proveedor
ha de ofrecer latest consistente, retención contra borrado/reemplazo y publicación
idempotente por hash; un acuse local no prueba esas propiedades.
Para la compuerta de publicación, cada GET `/latest` devuelve el último sello
o HTTP 404 si no existe ninguno para ese ambiente. Sólo la pareja 404/404 con
baseline local `observed` y cero lotes permite la génesis; 5xx/timeout o un
solo 404 no autorizan a reiniciar el histórico. Con un lote pendiente, ambos
testigos pueden estar en el último completo o en ese pendiente por un acuse
perdido: no se permite un hash/rango fuera de esos dos valores.

## Roles y hardening

`003_audit_seal_privileges.sql` es una migración nueva forward-only para quienes
ya ejecutaron 002. Fija `search_path=pg_catalog` y SECURITY DEFINER para el trigger;
revoca ejecución pública de `next_seal_seq`, `attach` y `log_change`.
El owner debe ser un rol migrador confiable, sin entrega de credenciales a API.
No conceder ownership, CREATE de schema, TRUNCATE, DDL, ni DML de audit a la API.
La API conserva sólo DML de negocio; el trigger preinstalado escribe como owner.

El verificador necesita USAGE de `audit` y SELECT de `change_log`, `seal_cursor`,
`seal_batches`; para estado, USAGE de `identity` y SELECT de `identity.roles`.
El sellador tiene SELECT de audit y INSERT de `seal_batches`, UPDATE sólo de
firma/clave/acuses, UPDATE(`status`) de `seal_baseline` y SELECT de
`identity.roles` para el checkpoint; no debe poder modificar hashes/rangos,
cursor/eventos ni borrar.
El script de aprovisionamiento no demuestra ausencia de grants heredados de un
rol existente: auditar membresías, owners y privilegios efectivos antes de activar.
La prueba negativa local cubre DML API válido y denegación de UPDATE/DELETE del
log, cambio del cursor, borrado de sellos, TRUNCATE y desactivación de triggers.

## Métricas, alerta y respuesta

Registrar cursor local/remoto, última secuencia comprobada, pendientes, cobertura,
filas sin evidencia/divergencias, recepción externa del reporte y tiempo de cada
testigo. Nunca usar una fecha de reporte local como prueba de recepción externa.

Ejemplo sintético permitido para el colector externo:

```json
{
  "ambiente": "prod",
  "estado": "no-verificado",
  "alertas": ["verificador-silencioso-o-vencido"],
  "contenidoIndependientementeVerificado": false
}
```

El timer de sellado existente propone cinco minutos y jitter hasta treinta
segundos. No hay SLO aprobado: el umbral debe incluir intervalo, jitter y timeout,
y debe probarse con escrituras representativas antes de prod. El scan completo
no se reemplaza por el cotejo incremental: una fila no tocada puede ser alterada
sin aparecer en el rango incremental.

Ante alerta:

1. Preservar evidencias y detener publicación/reanudación; no reparar logs,
   volver a calcular sellos para sustituirlos, ni eliminar testigos.
2. Distinguir indisponibilidad/frescura de divergencia/firma/retroceso. Revisar
   fuera de la VM timestamps de recepción y ambos custodios con cuentas lectoras.
3. Mantener operaciones de negocio según el protocolo institucional, mostrando
   el intervalo como no verificado; no borrar el DDL para resolver la alerta.
4. Restaurar sólo en destino aislado; invocar `--verificar-restauracion` con
   lectoras de testigos del ambiente fuente. Salida 2 bloquea autorización humana
   de reanudar. Nunca montar credenciales de firma/publicación en el restaurado.
5. Tras reconciliación documentada, preservar anclas previas y reanudar con
   aprobación. No convertir la reconciliación en autenticación retroactiva.

Retener claves públicas antiguas. No reemplazarlas por la nueva con igual ID.
Antes de rotar, exigir transición firmada, procedimiento de revocación y ensayo
histórico; agregar otra clave al diccionario por sí solo no acredita transición.

**Entrega de alertas:** implementar el scheduler/colector en otro host y cuenta;
observar allí salida no cero, reporte vencido y caída del propio scheduler.
Enviar al canal independiente y exigir acuse humano de un simulacro. Esta entrega
no fue probada: no existen destino ni credenciales operativas en el workspace.
