# Makefile de operación manual de ambientes Ars Docendi.
#
# Envuelve los scripts de infra/scripts/. Las credenciales se leen de variables
# de entorno (libpq + las del ambiente); este Makefile NO contiene secretos.
#
# Uso:
#   make up AMBIENTE=pr-123
#   make down AMBIENTE=pr-123
#   make logs AMBIENTE=pr-123
#   make ps AMBIENTE=pr-123
#
# Variables de entorno requeridas para `up`: ver infra/scripts/spin-up.sh.

SHELL := /usr/bin/env bash
SCRIPTS := scripts
COMPOSE := compose/compose.base.yml

# AMBIENTE es obligatorio en todos los targets.
ifndef AMBIENTE
$(error Falta AMBIENTE. Uso: make <target> AMBIENTE=pr-123)
endif

.PHONY: up down logs ps

up:
	@$(SCRIPTS)/spin-up.sh $(AMBIENTE)

down:
	@$(SCRIPTS)/teardown.sh $(AMBIENTE)

logs:
	@docker compose -p $(AMBIENTE) -f $(COMPOSE) logs -f --tail=100

ps:
	@docker compose -p $(AMBIENTE) -f $(COMPOSE) ps
