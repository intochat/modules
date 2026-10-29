# syntax=docker/dockerfile:1
# Headless ArtCraft app (photocraft | filmcraft) as a Streamable-HTTP MCP server on :8000/mcp.
# The CLI speaks MCP over stdio; mcp-proxy holds one SDK client session to it and serves HTTP, so
# request ids stay the proxy's own. (supergateway multiplexed every HTTP session onto the one child
# without rewriting ids, so concurrent sessions received each other's replies.)
ARG RUST_VERSION=1.99
FROM rust:${RUST_VERSION}-slim-bookworm AS builder
ARG APP=photocraft
ARG REF=main
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates git pkg-config \
      libxkbcommon-dev libwayland-dev libx11-dev libxrandr-dev libxi-dev libxcursor-dev libasound2-dev libfontconfig1-dev \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
RUN git clone https://github.com/storytold/${APP}.git . && git checkout ${REF}
RUN --mount=type=cache,target=/usr/local/cargo/registry,sharing=locked \
    --mount=type=cache,target=/src/target,id=${APP}-target \
    cargo build --release --locked -p ${APP}-cli && cp target/release/${APP}-cli /usr/local/bin/craft-cli

FROM python:3.12-slim-bookworm
ARG APP=photocraft
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libfontconfig1 libasound2     && rm -rf /var/lib/apt/lists/* && pip install --no-cache-dir mcp-proxy==0.12.0 mcp==1.29.1 && mkdir -p /work
COPY --from=builder /usr/local/bin/craft-cli /usr/local/bin/craft-cli
ENV CRAFT_APP=${APP}
VOLUME /work
EXPOSE 8000
CMD ["mcp-proxy", "--host", "0.0.0.0", "--port", "8000", "--", "craft-cli", "mcp", "--automation-read-root", "/work", "--automation-write-root", "/work"]
