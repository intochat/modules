import 'dart:convert';

import 'package:intochat_os_flutter_shell/workspace/workspace_store.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('only local preferences are saved and restored', () async {
    String? saved;
    final store = WorkspaceStore(
      readPreferences: () async => saved,
      writePreferences: (value) async {
        saved = value;
      },
    );
    await store.load();
    store.settings.reducedMotion = true;
    store.settings.theme = 'paper';
    await store.save();
    expect((jsonDecode(saved!) as Map).keys, [
      'defaultModelProfile',
      'displayName',
      'role',
      'theme',
      'reducedMotion',
      'compactDensity',
    ]);
    final restored = WorkspaceStore(readPreferences: () async => saved);
    await restored.load();
    expect(restored.settings.reducedMotion, isTrue);
    expect(restored.settings.theme, 'paper');
    store.dispose();
    restored.dispose();
  });
}
