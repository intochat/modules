#!/usr/bin/env bash
# Regenerates the Dart client for the Neurons service from the Platform proto. Run from app/core.
# protoc runs the Dart plugin from this package's dev dependency (tool/protoc-gen-dart.bat), so
# no global activation is needed.
set -euo pipefail
PROTO=../../../../../IntoChat.OS.Authority/IntoChat.OS.Authority/Neurons
mkdir -p lib/src/generated
protoc --plugin="protoc-gen-dart=$(cygpath -w "$PWD/tool/protoc-gen-dart.bat" 2>/dev/null || echo "$PWD/tool/protoc-gen-dart.bat")" --proto_path="$PROTO" --dart_out=grpc:lib/src/generated "$PROTO/neurons.proto"
