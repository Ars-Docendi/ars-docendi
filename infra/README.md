# Infra

Esta carpeta hospeda **configuración de infraestructura** para despliegues. **Estado actual: skeleton**.

El deploy a producción se hará en VMs ofrecidas por la UNLaM. Los detalles (tipo de VM, OS, herramientas instaladas, proceso de release) están **TBD** hasta que la universidad provisione las máquinas.

## Estructura planeada

```
infra/
├── README.md             # este archivo
├── nginx/                # configs nginx (reverse proxy + TLS)
│   └── ars-docendi.conf
├── systemd/              # service units para correr backend
│   └── ars-docendi.service
├── backup/               # scripts de backup PostgreSQL (TBD)
└── ansible/              # playbooks (TBD si se decide usar Ansible)
```

## Cuando se reciba la VM

Activar el [hardening checklist](../docs/architecture/infrastructure.md#hardening-checklist-vm) de `infrastructure.md` paso a paso.

Adaptar:

- `nginx/ars-docendi.conf` — sample en este directorio, ajustar dominio + puertos reales.
- `systemd/ars-docendi.service` — sample en este directorio, ajustar usuario + paths reales.
- Crear scripts de backup en `backup/`.
- Decidir si usar Ansible para automatizar setup repetible.

## Referencias

- [docs/architecture/infrastructure.md](../docs/architecture/infrastructure.md) — Plan operacional completo
- [docs/workflows/check-deploy.md](../docs/workflows/) — Skill `/check-deploy`
- [docs/workflows/debug-production.md](../docs/workflows/) — Skill `/debug-production`
