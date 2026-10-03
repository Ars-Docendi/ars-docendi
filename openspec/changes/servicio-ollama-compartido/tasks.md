# Tasks

Alcance exclusivo: infraestructura/documentación, reutilizando Traefik existente. Sin backend/frontend/CI/CD, otro proxy o tests nuevos. Comprobaciones operativas aisladas, no suites de aplicaciones. La evidencia previa del proxy descartado no acredita esta corrección.

## 1. Infraestructura Ollama y Traefik

- [x] 1.1 Corregir `infra/ollama/compose.yml` para sólo Ollama, GPU/volumen, red externa privada y labels Traefik con router únicamente en `ollama`; validar Compose y ausencia de puertos del servicio, otro proxy y router `web`.
- [x] 1.2 Agregar entrypoint privado en `infra/traefik/traefik.yml` conservando web/dashboard/providers; verificar parser y routing/auth/streaming en Traefik aislado sin reiniciar compartido, distinguiendo configuración materializada de descubrimiento Docker real.
- [x] 1.3 Corregir `.env.example`, retirar configuración del proxy descartado y `nginx.conf`; verificar todas las variables propias y ausencia de secretos en labels/Git, sin variables de aplicación/GitHub.

## 2. Documentación operativa

- [x] 2.1 Conservar guía Debian/Docker/NVIDIA/toolkit contrastada con fuentes oficiales; verificar comandos/condiciones y no ejecutar provisioning privilegiado.
- [x] 2.2 Documentar red externa, mounts/puerto/imagen existentes y habilitación/reversión de Traefik en ventana aprobada, arranque/modelos y auth manual con revisión explícita; verificar coherencia con archivos y sintaxis Bash sin ejecutar administración.
- [x] 2.3 Actualizar topología Serve→Traefik→Ollama y vm-dev sin cliente vía VM router, DNS/TLS y sonda Docker temporal; verificar separación de routing público/privado sin modificar consumidores.
- [x] 2.4 Documentar mantenimiento y rollback sólo IA frente al rollback inicial del Traefik compartido, preservando modelos/red/mappings, y actualizar enlace; verificar referencias y limitaciones, sin prometer cuotas/prioridad o reload automático del archivo auth.

## 3. Verificación y aceptación

- [x] 3.1 Revisar scope/diff/whitespace, ausencia de cambios de aplicaciones/workflows/tests y archivo del proxy descartado; ejecutar validación OpenSpec estricta y registrar sólo evidencia real de Traefik, preservando `.gitignore` ajeno.
- [ ] 3.2 Operador: habilitar Traefik/Ollama en pc-prod, comprobar GPU RTX 5070, descubrimiento Docker/rotación real, HTTPS local/subred/Docker y routing público intacto; dejar pendiente si no hay acceso, sin modificar aplicaciones ni crear tests.

## Evidencia de la corrección Traefik

- Compose validado (exit 0): sólo Ollama, imagen fijada, GPU solicitada, volumen persistente, sin ports ni otro proxy, red externa y labels/router exclusivo de `ollama`. Todas las variables documentadas; retirado `nginx.conf` y configuración de su contenedor.
- Traefik estático: única diferencia respecto de HEAD es nuevo entrypoint privado; web/dashboard/providers intactos. No hubo reinicio del compartido: `StartedAt` conservado antes/después.
- Traefik real v3.7.5 ejecutado aislado, con mismo bloque estático y configuración dinámica materializada desde labels mediante file provider, sin socket real/Docker discovery ni secretos de otras aplicaciones. Parser sin errores; esto NO acredita el corte/descubrimiento Docker de pc-prod.
- HTTP privado observado: 401 sin credencial/inválida, 200 con bcrypt válido, 404 en administración/métodos no admitidos. Las rutas Ollama por entrypoint `web` devolvieron 404.
- Inferencia real `/api/generate` normal/streaming y `/api/chat` con `smollm2:135m`: 200 y NDJSON; `ollama ps`: **100% CPU**. Modelo persistió tras recreación y upstream continuó respondiendo. No evidencia GPU.
- Relectura auth comprobada: editar sólo htpasswd dejó credencial retirada en 200; revisión de config provocó 401 para retirado y 200 para vigente. El equivalente vía labels/evento Docker queda en aceptación manual.
- Recursos temporales eliminados y ausencia verificada. Documentación Bash validada sólo sintácticamente, versión CLI con entrypoint explícito ejercida. Scope/whitespace/OpenSpec revisados; `.gitignore` ajeno conservado; sin suites ni tests nuevos.
- **3.2 pendiente:** operador debe habilitar Traefik existente en ventana, verificar descubrimiento Docker/rotación, RTX 5070 y HTTPS real local/subred/Docker, además de routing público intacto. No sincronizar/archivar como si estuviera aceptado.
