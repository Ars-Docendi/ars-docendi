FROM mcr.microsoft.com/devcontainers/dotnet:10.0-noble

# Codex uses Bubblewrap for its Linux command sandbox. Use Ubuntu's packaged
# binary instead of the bundled fallback helper.
RUN apt-get update \
    && apt-get install -y --no-install-recommends bubblewrap \
    && rm -rf /var/lib/apt/lists/*

# Language/tool versions are installed as Dev Container features in
# devcontainer.json.
