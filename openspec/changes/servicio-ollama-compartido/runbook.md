# Runbook del servicio Ollama

La guía implementada y mantenida está en [docs/operations/ollama-compartido.md](../../../docs/operations/ollama-compartido.md).

Contiene comandos administrativos separados de la operación no privilegiada, configuración Docker/NVIDIA, habilitación/reversión de Traefik existente, autenticación manual con revisión, arranque/modelos, Tailscale Serve y subnet router existente, DNS/TLS, sondas Docker temporales, mantenimiento y rollback sin borrar volúmenes.

No incluye cambios de aplicaciones, CI/CD o tests nuevos. Las credenciales se preparan fuera del repositorio y se solicitan interactivamente; el proxy no es un gateway desarrollado.

La validación aislada en CPU no acredita instalación real en pc-prod: la tarea 3.2 conserva pendiente GPU RTX 5070 y acceso HTTPS desde local, vm-dev y redes Docker reales. No ejecutar bloques administrativos desde apply ni considerar su mera documentación evidencia de ejecución.
