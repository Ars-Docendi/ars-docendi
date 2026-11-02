## Purpose

Preparar una instancia de inferencia local independiente, privada y persistente mediante infraestructura Docker y documentación operativa, sin modificar las aplicaciones de Ars Docendi.

## ADDED Requirements

### Requirement: Servicio independiente con GPU y persistencia

La infraestructura MUST permitir ejecutar Ollama con GPU NVIDIA en pc-prod y conservar modelos al recrear sus contenedores. Su arranque/parada MUST operar exclusivamente sobre el proyecto dedicado, sin afectar proyectos o datos prod/staging/pr-N.

#### Scenario: Recreación sin pérdida de modelos

- **GIVEN** un modelo descargado en el volumen persistente
- **WHEN** el operador recrea o detiene el proyecto sin borrar volúmenes
- **THEN** el modelo permanece disponible al iniciar nuevamente y los proyectos de aplicación no se modifican.

#### Scenario: Comprobación GPU

- **GIVEN** driver, toolkit y modelo local compatibles
- **WHEN** el operador realiza una inferencia y consulta el estado de Ollama
- **THEN** puede comprobar respuesta real y uso de GPU, distinguiéndolo de la mera visibilidad del dispositivo.

### Requirement: Endpoint privado y autenticado

La infraestructura MUST exponer únicamente el proxy autenticado, sin publicar Ollama directamente. Fuera de pc-prod MUST ser alcanzable sólo por un hostname dedicado del túnel Cloudflare principal protegido por Cloudflare Access con Service Token e IP de origen permitida, y cloudflared MUST exigir el JWT de Access. MUST permitir rutas/métodos documentados de inferencia y consulta de versión/modelos, denegando administración remota de modelos. Credenciales MUST quedar fuera de archivos versionados e imágenes.

#### Scenario: Origen sin Service Token o IP no permitida

- **GIVEN** el hostname de IA publicado por el túnel
- **WHEN** se solicita sin Service Token válido o desde una IP fuera de la política
- **THEN** Cloudflare Access rechaza la petición antes de llegar al origen.

#### Scenario: Acceso sin credencial

- **GIVEN** un origen que superó Cloudflare Access
- **WHEN** solicita una ruta permitida sin credencial válida
- **THEN** recibe HTTP 401 y no accede a inferencia.

#### Scenario: Inferencia y administración separadas

- **GIVEN** un operador con credencial válida y un modelo ya instalado
- **WHEN** solicita inferencia y después intenta descargar o borrar modelos por el endpoint remoto
- **THEN** inferencia puede responder, pero la administración remota es rechazada y queda reservada a operación local del contenedor.

### Requirement: Reutilizar proxy existente sin exposición pública

La infraestructura MUST reutilizar Traefik existente, sin agregar otro proxy productivo, y separar inferencia en un entrypoint privado distinto de `web`, con límites de tasa y concurrencia por origen. Su configuración MUST preservar routing de aplicaciones. Habilitación del proxy compartido MUST documentar ventana y reversión; apply MUST NOT reiniciarlo sin operación aprobada en host real.

#### Scenario: API IA no se enruta por ingreso público

- **GIVEN** router privado activo y routing público de aplicaciones
- **WHEN** se solicita una ruta Ollama por entrypoint web
- **THEN** no se entrega Ollama por el hostname de aplicaciones, mientras sus peticiones autorizadas por el entrypoint privado pueden responder.

#### Scenario: Exceso de peticiones

- **GIVEN** un origen que supera el límite de tasa configurado
- **WHEN** continúa enviando peticiones
- **THEN** recibe HTTP 429 sin llegar a Ollama.

#### Scenario: Rotación autenticación sin reiniciar proxy compartido

- **GIVEN** un usuario vigente y otro cuya credencial se retira
- **WHEN** operador actualiza archivo, revisión y configuración según guía
- **THEN** el usuario retirado deja de acceder y el vigente conserva acceso, sin reiniciar Traefik ni cambiar aplicaciones.

### Requirement: Accesibilidad por ambiente

Staging/pr-N en vm-dev MUST alcanzar el endpoint sólo por el túnel Cloudflare principal con Service Token y credencial propia. Prod MUST alcanzarlo sólo localmente por la red Docker de Traefik, sin salir del host y sin Service Token. El hostname de IA MUST NOT ser servido por el túnel de Proxmox ni por el entrypoint `web`. MUST NOT requerir cambios en aplicaciones.

#### Scenario: Cliente temporal en red Docker de vm-dev

- **GIVEN** Service Token, credencial `staging` y la IP de egreso de vm-dev autorizada
- **WHEN** el operador usa una sonda temporal desde la red del consumidor
- **THEN** obtiene respuesta por HTTPS sin cambiar código, imagen o configuración del consumidor.

#### Scenario: Prod por ruta local

- **GIVEN** el backend prod en la red Docker de Traefik en pc-prod
- **WHEN** una sonda temporal en su namespace solicita el entrypoint privado con credencial `prod`
- **THEN** obtiene respuesta sin atravesar Cloudflare, y esa credencial sola no habilita acceso desde Internet.

### Requirement: Archivos y alcance limitados a infraestructura

El cambio MUST entregar Compose, configuración declarativa del proxy, ejemplo de variables propio y documentación operativa. MUST NOT modificar backend, frontend, Compose/scripts de aplicación, CI/CD o scopes GitHub; MUST NOT desarrollar gateway, cliente IA o automatización del ciclo pr-N; MUST NOT crear tests.

#### Scenario: Revisión de archivos entregados

- **GIVEN** el cambio implementado y su diff
- **WHEN** se revisan los archivos modificados
- **THEN** sólo están los archivos de infraestructura/documentación permitidos por la propuesta, sin cambios de aplicaciones, workflows ni archivos de tests.

### Requirement: Instalación y operación documentadas

La guía MUST detallar prerrequisitos, configuración, arranque, GPU, carga de modelos, conectividad privada, autenticación manual, diagnóstico, mantenimiento y rollback preservando volúmenes. MUST separar pasos administrativos del operador de acciones no privilegiadas, e indicar resultados esperados sin presentarlos como ejecutados. MUST explicitar que la instancia no ofrece prioridad ni cuotas por ambiente.

#### Scenario: Operación desde la guía

- **GIVEN** un operador que completa el inventario real
- **WHEN** sigue los pasos en el host correspondiente
- **THEN** encuentra comandos compatibles con los archivos entregados, condiciones previas y resultados esperados; puede detener/revertir la instancia sin borrar modelos ni tocar aplicaciones.
