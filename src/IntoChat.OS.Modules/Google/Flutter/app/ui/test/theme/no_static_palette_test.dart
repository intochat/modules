import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('lib sources use no static palette or static UiType styles', () {
    final pattern = RegExp(
      r'UiPalette\b|UiType\.(title|heading|cardTitle|metric|body|bodyMuted|meta|metaStrong|empty)\b',
    );
    final offenders = <String>[];
    // flutter test runs with the package root (app/ui) as cwd.
    final base = Directory.current.path;
    for (final root in ['$base/lib', '$base/../shell/lib']) {
      expect(
        Directory(root).existsSync(),
        isTrue,
        reason: 'cannot find $root (run `flutter test` from app/ui)',
      );
      for (final file in Directory(root).listSync(recursive: true)) {
        if (file is! File || !file.path.endsWith('.dart')) continue;
        final lines = file.readAsLinesSync();
        for (var i = 0; i < lines.length; i++) {
          if (pattern.hasMatch(lines[i])) {
            offenders.add('${file.path}:${i + 1}');
          }
        }
      }
    }
    expect(offenders, isEmpty);
  });
}
