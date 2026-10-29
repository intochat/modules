# syntax=docker/dockerfile:1
# ArtCraft web build (wasm) served by nginx on :80. FilmCraft's page exposes window.filmcraft.
# No binaryen: bookworm's wasm-opt predates reference types and breaks the externref table at load.
ARG RUST_VERSION=1.99
FROM rust:${RUST_VERSION}-slim-bookworm AS builder
ARG APP=photocraft
ARG REF=main
ARG TRUNK_VERSION=0.21.14
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl git pkg-config \
    && rm -rf /var/lib/apt/lists/* && rustup target add wasm32-unknown-unknown
WORKDIR /src
RUN git clone https://github.com/storytold/${APP}.git . && git checkout ${REF}
RUN --mount=type=cache,target=/usr/local/cargo/registry,sharing=locked \
    --mount=type=cache,target=/src/target,id=${APP}-web-target \
    set -eu; mkdir -p /site; \
    if [ "${APP}" = photocraft ]; then \
      curl -fsSL "https://github.com/trunk-rs/trunk/releases/download/v${TRUNK_VERSION}/trunk-x86_64-unknown-linux-musl.tar.gz" | tar -xz -C /usr/local/bin trunk; \
      cd apps/photocraft-web && CARGO_PROFILE_WASM_RELEASE_LTO=thin trunk build --release && cp -R /src/dist/web/. /site/; \
    else \
      v=$(grep -o 'WASM_BINDGEN[^"]*"[0-9.]*"' xtask/src/main.rs | grep -o '[0-9][0-9.]*' | head -1); \
      cargo install wasm-bindgen-cli --version "$v" --locked; \
      cargo xtask web && cp -R target/web/dist/. /site/; \
    fi

FROM nginx:1.28-alpine
RUN printf 'types { application/wasm wasm; }\n' > /etc/nginx/conf.d/wasm.types \
 && sed -i 's|include       /etc/nginx/mime.types;|include /etc/nginx/mime.types; include /etc/nginx/conf.d/wasm.types;|' /etc/nginx/nginx.conf
COPY --from=builder /site /usr/share/nginx/html
EXPOSE 80
