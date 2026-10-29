import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

/// Loads the bundled OFL fonts so goldens render real glyphs instead of Ahem.
/// Registered under both the bare family and the package-qualified family
/// (`UiType.sans` / `UiType.mono`). v1 families (Segoe UI, Cascadia Mono)
/// are not loaded and fall back to the test font. The Lucide icon font is
/// loaded from the pub cache so kit icons render as glyphs.
Future<void> testExecutable(FutureOr<void> Function() testMain) async {
  TestWidgetsFlutterBinding.ensureInitialized();
  // `flutter test` runs with the package root as cwd.
  final root = Directory.current.path;
  Future<void> load(String family, String path, {bool bundled = true}) async {
    final file = File(bundled ? '$root/$path' : path);
    if (!file.existsSync()) {
      if (!bundled) return; // SDK-provided icon font; optional.
      throw StateError(
        'Bundled font missing for "$family": ${file.path}. '
        'Goldens and text metrics need the files under ui/assets/fonts.',
      );
    }
    final loader = FontLoader(family)
      ..addFont(file.readAsBytes().then(ByteData.sublistView));
    await loader.load();
  }

  for (final (family, path) in [
    ('Schibsted Grotesk', 'assets/fonts/SchibstedGrotesk-Variable.ttf'),
    ('JetBrains Mono', 'assets/fonts/JetBrainsMono-Variable.ttf'),
  ]) {
    await load(family, path);
    await load('packages/intochat_os_ui/$family', path);
  }
  final flutterRoot = Platform.environment['FLUTTER_ROOT'];
  if (flutterRoot != null) {
    await load(
      'MaterialIcons',
      '$flutterRoot/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
      bundled: false,
    );
  }
  final lucide = _packageRoot(root, 'lucide_icons_flutter');
  if (lucide != null) {
    await load(
      'packages/lucide_icons_flutter/Lucide',
      '$lucide/assets/lucide.ttf',
      bundled: false,
    );
  }
  await testMain();
}

/// Root directory of [package] from the nearest `package_config.json`
/// (the workspace keeps it above the package), or null when not found.
String? _packageRoot(String start, String package) {
  for (Directory? dir = Directory(start); dir != null;) {
    final config = File('${dir.path}/.dart_tool/package_config.json');
    if (config.existsSync()) {
      final packages =
          (jsonDecode(config.readAsStringSync()) as Map)['packages'] as List;
      for (final entry in packages.cast<Map>()) {
        if (entry['name'] == package) {
          return config.parent.uri
              .resolve(entry['rootUri'] as String)
              .toFilePath();
        }
      }
      return null;
    }
    final parent = dir.parent;
    dir = parent.path == dir.path ? null : parent;
  }
  return null;
}
