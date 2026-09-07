# syntax=docker/dockerfile:1
#
# Imagen del frontend Ars Docendi (React 19 + Vite, servido por nginx).
# Contexto de build = raíz del monorepo para usar su único lockfile.

# --- Stage build: Node + pnpm ---
FROM node:22-alpine AS build
WORKDIR /app

# git: necesario para resolver la dependencia `@ars-docendi/ui` (referencia
# `github:Ars-Docendi/ui-lib`, repo público).
RUN apk add --no-cache git

# corepack activa la versión de pnpm fijada en package.json (campo packageManager).
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
RUN corepack enable

# Restore cacheable: copiar manifiestos antes del código.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY frontend/package.json frontend/package.json
RUN pnpm install --frozen-lockfile --filter frontend...

COPY frontend frontend
ARG VITE_DEVELOPMENT_AUTH_ENABLED=false
ENV VITE_DEVELOPMENT_AUTH_ENABLED=${VITE_DEVELOPMENT_AUTH_ENABLED}
RUN pnpm --filter frontend build

# --- Stage runtime: nginx sirviendo el bundle estático ---
FROM nginx:alpine AS runtime

# Reemplaza el server block default por el nuestro (fallback SPA + /api fuera de juego).
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/frontend/dist /usr/share/nginx/html

# Coincide con la label de Traefik (loadbalancer.server.port=80) de compose.base.yml.
EXPOSE 80
