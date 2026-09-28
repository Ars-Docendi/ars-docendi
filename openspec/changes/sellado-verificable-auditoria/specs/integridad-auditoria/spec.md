# Spec Delta

## Purpose

Provee evidencia verificable fuera de la infraestructura principal para detectar alteraciones, omisiones y retrocesos del historial auditado y de los datos críticos declarados, sin revelar información personal en los testigos.

## ADDED Requirements

### Requirement: Cobertura y línea base explícitas

El sistema SHALL declarar el ambiente, las fuentes auditadas y las tablas críticas sometidas a verificación de estado, junto con una línea base de inicio identificable. MUST distinguir eventos históricos anteriores al primer sello, eventos pendientes, eventos sellados y estado vigente comprobado. MUST NOT afirmar que el historial anterior al primer anclaje era auténtico ni que una tabla excluida está protegida.

#### Scenario: Activación sobre historial existente

- **GIVEN** una base con eventos previos al despliegue
- **WHEN** se activa el sellado por primera vez
- **THEN** el sistema identifica el punto de partida y distingue la línea base no comprobable retrospectivamente de los nuevos lotes anclados

#### Scenario: Cobertura parcial

- **GIVEN** una tabla crítica que no se incluyó en la verificación de estado
- **WHEN** se informa la cobertura
- **THEN** el sistema indica que su estado vigente no fue verificado y no lo presenta como íntegro

### Requirement: Sellos completos y deterministas

El sistema SHALL cerrar lotes de eventos confirmados en un orden total estable y con cobertura completa, aun con transacciones concurrentes o abortadas. Cada sello SHALL contener ambiente, rango/cursor, cantidad, versión de canonicalización, resumen criptográfico del contenido y referencia al sello anterior. MUST detectar omisión, modificación, duplicación y reordenamiento de eventos sellados; los reintentos MUST ser idempotentes y los lotes cerrados MUST NOT cambiar. Si no puede garantizarse un límite seguro de eventos confirmados, MUST suspender el cierre y alertar en lugar de certificar un lote incompleto.

#### Scenario: Escritura concurrente al cierre

- **GIVEN** una transacción de auditoría aún sin confirmar durante el sellado
- **WHEN** se cierra el siguiente lote
- **THEN** esa transacción no puede aparecer tardíamente dentro de un rango ya sellado, ni ser omitida al confirmarse

#### Scenario: Alteración o pérdida

- **GIVEN** un lote previamente sellado
- **WHEN** se cambia o elimina un evento incluido
- **THEN** la comprobación del lote falla con el rango afectado

#### Scenario: Reintento

- **GIVEN** un cierre que se repite después de un fallo parcial
- **WHEN** se reanuda el trabajo
- **THEN** no se generan dos sellos incompatibles para el mismo rango

### Requirement: Testigos independientes sin datos personales

El sistema SHALL publicar el manifiesto verificable en un custodio fuera de PostgreSQL y de la VM de aplicación, y SHALL depositar su huella en un segundo testigo de administración y credenciales separadas. La clave de firma MUST NOT estar disponible para la API ni para código de PR ni compartir control administrativo con los testigos. Los testigos MUST NOT recibir snapshots, identificadores de fila o usuario, ni valores de datos personales. Un fallo de publicación en cualquiera de los testigos MUST impedir declarar el lote completamente anclado y MUST generar alerta; no MUST bloquear las operaciones de negocio ordinarias.

#### Scenario: Alteración del custodio principal

- **GIVEN** un sello publicado en ambos testigos
- **WHEN** el custodio principal devuelve una versión distinta o pierde el manifiesto
- **THEN** la comparación con la huella independiente detecta la discrepancia

#### Scenario: Publicación parcial

- **GIVEN** el custodio acepta el manifiesto pero el segundo testigo no confirma su huella
- **WHEN** se informa la salud del sellado
- **THEN** el lote figura como publicación incompleta y se alerta sin fingir protección doble

#### Scenario: Lectura del manifiesto

- **WHEN** un operador inspecciona el contenido publicado fuera de la base
- **THEN** no encuentra snapshots, identificadores de usuario/fila ni datos personales

### Requirement: Verificación y alertas comprobables

El sistema SHALL verificar la continuidad de sellos y la concordancia de eventos con los manifiestos y testigos; SHALL comprobar periódicamente el estado vigente de las tablas críticas declaradas contra evidencia auditada. MUST reportar último evento y momento efectivamente verificados, cobertura, atrasos, fallos y último resultado; MUST alertar ante alteración, omisión, retroceso, ausencia de nuevos sellos, fallo del verificador o discrepancia de estado. Una comprobación fallida o vencida MUST mostrarse como no verificada, no como saludable. El verificador MUST NOT reparar automáticamente datos o auditoría.

#### Scenario: Cambio directo en datos

- **GIVEN** un evento sellado cuyo estado final corresponde a una fila crítica
- **WHEN** alguien cambia directamente esa fila sin un evento equivalente
- **THEN** la verificación de estado detecta la divergencia y señala la tabla y clave solo en un canal interno protegido

#### Scenario: Restauración atrasada

- **GIVEN** un testigo que conserva sellos posteriores al backup
- **WHEN** se restaura la base desde ese backup
- **THEN** el sistema detecta el retroceso y exige reconciliación antes de declarar íntegra la instalación

#### Scenario: Falta de evidencia reciente

- **GIVEN** un intervalo máximo de sellado o verificación vencido
- **WHEN** se consulta el estado operativo
- **THEN** figura como atrasado/no verificado y se dispara una alerta aunque no se haya detectado una modificación

### Requirement: Aislamiento y operación segura

El sistema SHALL mantener las credenciales de sellado y custodia de producción fuera de `staging` y `pr-N`; MUST limitar a cada identidad técnica a sus acciones y ambiente necesarios. La restauración y la rotación de claves SHALL conservar la verificabilidad de sellos previos y registrar la transición; un reinicio no productivo MUST NOT generar retrocesos o borrados en los testigos de producción.

#### Scenario: Preview intenta publicar en producción

- **GIVEN** un proceso de `pr-N` o un runner de PR
- **WHEN** intenta obtener permisos de firma o escritura sobre sellos de producción
- **THEN** no tiene credenciales ni acceso autorizados

#### Scenario: Rotación de clave

- **GIVEN** sellos válidos emitidos con una clave anterior
- **WHEN** se rota la clave de firma
- **THEN** los sellos históricos siguen verificándose y la nueva clave queda vinculada mediante una transición verificable
