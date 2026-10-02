# Spec Delta

## MODIFIED Requirements

### Requirement: Deploy de prod y staging por rama

GitHub Actions SHALL desplegar `prod` al hacer push/merge a `main` únicamente en Debian y `staging` al hacer push/merge a `develop` únicamente en Proxmox cuando haya código, infraestructura, SQL o dependencias desplegables. Un cambio exclusivamente documental Markdown MUST NOT iniciar build ni deploy. Cada workflow SHALL construir/publicar frontend y backend etiquetados por SHA y ejecutar un único deploy en el host autorizado; MUST NOT distribuir el mismo ambiente a ambos hosts. Producción SHALL publicarse bajo el dominio raíz conservando el Environment `prod`. Un deploy de producción MUST NOT alterar staging/previews y viceversa.

#### Scenario: Merge a main deploya prod

- **WHEN** se mergea a `main` con un cambio desplegable
- **THEN** se construyen las imágenes por SHA y se despliega `prod` únicamente en Debian para el dominio raíz
- **AND** staging y previews de Proxmox quedan intactos

#### Scenario: Merge a develop deploya staging

- **WHEN** se mergea a `develop` con un cambio desplegable
- **THEN** staging se actualiza únicamente en Proxmox
- **AND** producción Debian queda intacta

#### Scenario: Cambio sólo documental no deploya

- **WHEN** se hace push/merge a `main` o `develop` modificando sólo Markdown
- **THEN** no se construyen imágenes ni se despliega producción o staging

#### Scenario: Destino indisponible

- **WHEN** el runner principal no está disponible para un deploy de producción
- **THEN** el job no se ejecuta accidentalmente en el runner secundario
- **AND** no se modifica la ubicación de producción como mecanismo de recuperación automática

### Requirement: Teardown de ambiente pr-N al cerrar el PR

Al cerrarse un PR, el sistema SHALL destruir en Proxmox sus contenedores, base/schema y objetos/credenciales asociados, usando scripts de la rama base confiable. El teardown MUST ser idempotente y MUST NOT ejecutarse en Debian ni borrar recursos productivos.

#### Scenario: Cierre de PR destruye el ambiente y su base

- **WHEN** se cierra o mergea el PR número N
- **THEN** el runner confiable secundario elimina su ambiente, base y objetos en Proxmox
- **AND** `pr-N.example.net` deja de resolver a un contenedor
- **AND** producción Debian no se modifica

#### Scenario: Teardown idempotente

- **WHEN** se solicita eliminar un preview ya inexistente
- **THEN** termina sin error ni recursos residuales
- **AND** no altera producción, staging u otros previews

## ADDED Requirements

### Requirement: Asignación determinística de runners por host

El deploy de producción SHALL usar `self-hosted, arsdocendi, confiable, principal`; staging y teardown de previews SHALL usar `self-hosted, arsdocendi, confiable, secundaria`. El build/deploy de previews SHALL usar `self-hosted, arsdocendi, efimero, secundaria` y conservar el label de maintainer y la aprobación del Environment `pr-preview`, sin usar `pull_request_target`. Las ubicaciones MUST NOT depender de una selección aleatoria entre runners con etiquetas genéricas.

#### Scenario: Un destino confiable por ambiente

- **WHEN** se solicitan despliegues de producción y staging
- **THEN** producción selecciona sólo Debian y staging sólo Proxmox
- **AND** no hay matriz de hosts para ninguno de los dos ambientes

#### Scenario: Preview gated sólo en Proxmox

- **WHEN** un PR pasa los gates de maintainer y Environment
- **THEN** su build/deploy se ejecuta en el efímero secundario
- **AND** al cerrarse, su teardown usa el confiable secundario

### Requirement: Secretos de GitHub conservados por ambiente

Los jobs SHALL consumir las variables/secrets existentes de GitHub del repositorio y del Environment correspondiente, inyectados en runtime sin un segundo inventario local de credenciales de aplicación. MUST NOT exponerlos en logs, archivos versionados o imágenes. El cambio de hostname productivo MUST NOT renombrar el Environment `prod` ni alterar silenciosamente los scopes autorizados.

#### Scenario: Producción con el scope existente

- **WHEN** se despliega `prod` bajo el dominio raíz de Debian
- **THEN** usa `APP_DB_PASSWORD_PROD` de `prod` y `PG*`/`SEAWEEDFS_ROOT_*` del repositorio
- **AND** su conexión y operaciones de storage se dirigen a las instancias locales de Debian

#### Scenario: Preview sin aprobación

- **WHEN** falta la aprobación de `pr-preview`
- **THEN** el job no accede a sus secretos de Environment ni despliega
