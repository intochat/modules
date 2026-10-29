import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:intochat_os_flutter_shell/workspace/workspace_store.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  paletteTests();
  testWidgets('declared select options send selection to their neuron', (
    tester,
  ) async {
    final events = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'select',
            name: 'model',
            load: (_, _) async => {
              'label': 'Model',
              'selected': null,
              'options': [
                {'id': 'profile:fast', 'label': 'Fast', 'enabled': true},
              ],
            },
            onAction: (event) async => events.add(event),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Model'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Fast'));
    await tester.pumpAndSettle();
    expect(events.single, {
      'kind': 'select',
      'name': 'model',
      'value': 'profile:fast',
    });
    await tester.pumpWidget(const SizedBox());
  });
}

void paletteTests() {
  test('old theme values load as palette names and palettes round-trip', () {
    expect(WorkspacePreferences.fromJson({'theme': 'dark'}).theme, 'midnight');
    expect(WorkspacePreferences.fromJson({'theme': 'light'}).theme, 'paper');
    expect(WorkspacePreferences.fromJson({}).theme, 'intochat');
    expect(
      WorkspacePreferences.fromJson({'theme': 'system'}).theme,
      'intochat',
    );
    expect(
      WorkspacePreferences.fromJson({'theme': 'midnight'}).theme,
      'midnight',
    );
    expect(WorkspacePreferences.fromJson({'theme': 'lumen'}).theme, 'lumen');
    expect(WorkspacePreferences().toJson()['theme'], 'intochat');
  });
}
