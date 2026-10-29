import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

final class _FakeLocal implements LocalPreferences {
  final values = <PreferenceStore, String>{};
  final writes = <(PreferenceStore, String)>[];

  @override
  String? read(PreferenceStore store) => values[store];

  @override
  Future<void> write(PreferenceStore store, String value) async {
    writes.add((store, value));
    values[store] = value;
  }
}

Future<List<String>> _pump(
  WidgetTester tester,
  Map<String, dynamic> state, {
  LocalPreferences? local,
  Future<void> Function(Map<String, dynamic>)? onAction,
}) async {
  final calls = <String>[];
  const name = 'brain-1/settings/pref';
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: NeuronView(
          kind: 'preference',
          name: name,
          load: (kind, n) async => state,
          onAction: (event) async {
            calls.add(jsonEncode(event));
            await onAction?.call(event);
          },
          localPreferences: local,
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return calls;
}

Map<String, dynamic> _state(
  int store,
  int kind, {
  String? value,
  List<Map<String, dynamic>>? choices,
}) => {
  'name': 'brain-1/settings/pref',
  'version': 1,
  'spec': {'store': store, 'label': 'Label', 'kind': kind, 'choices': ?choices},
  'value': value,
};

void main() {
  testWidgets('local_choice_writes_local_preferences', (tester) async {
    final local = _FakeLocal()..values[PreferenceStore.localTheme] = 'light';
    final calls = await _pump(
      tester,
      _state(
        11,
        1,
        choices: [
          {'id': 'light', 'label': 'Light'},
          {'id': 'dark', 'label': 'Dark'},
        ],
      ),
      local: local,
    );
    expect(find.text('Light'), findsOneWidget);
    await tester.tap(find.byKey(const Key('preference-choice')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Dark').last);
    await tester.pumpAndSettle();
    expect(local.writes, [(PreferenceStore.localTheme, 'dark')]);
    expect(calls, isEmpty);
  });

  testWidgets('toggle_sets_through_the_neuron', (tester) async {
    final local = _FakeLocal();
    final state = _state(0, 0, value: 'on');
    final calls = await _pump(
      tester,
      state,
      local: local,
      onAction: (event) async => state['value'] = event['value'],
    );
    final track = find.byType(UiKitSwitch);
    expect(tester.widget<UiKitSwitch>(track).value, isTrue);
    await tester.tap(track);
    await tester.pumpAndSettle();
    expect(calls, [
      jsonEncode({
        'kind': 'preference',
        'name': 'brain-1/settings/pref',
        'action': 'set',
        'value': 'off',
      }),
    ]);
    expect(local.writes, isEmpty);
    expect(tester.widget<UiKitSwitch>(track).value, isFalse);
  });

  testWidgets('disabled_choice_shows_its_detail', (tester) async {
    await _pump(
      tester,
      _state(
        14,
        1,
        choices: [
          {'id': 'auto', 'label': 'Auto, cheapest model with tools'},
          {
            'id': 'gpt',
            'label': 'GPT',
            'enabled': false,
            'detail':
                'Not available. Add a key in API keys or pick another model.',
          },
        ],
      ),
      local: _FakeLocal(),
    );
    await tester.tap(find.byKey(const Key('preference-choice')));
    await tester.pumpAndSettle();
    expect(
      find.text('Not available. Add a key in API keys or pick another model.'),
      findsOneWidget,
    );
  });

  testWidgets('failure_reverts_with_toast', (tester) async {
    await _pump(
      tester,
      _state(1, 0, value: 'on'),
      onAction: (_) async => throw StateError('fixture-value-0001 refused'),
    );
    final track = find.byType(UiKitSwitch);
    await tester.tap(track);
    await tester.pumpAndSettle();
    expect(find.text('Could not save that change.'), findsOneWidget);
    expect(find.textContaining('fixture-value-0001'), findsNothing);
    expect(tester.widget<UiKitSwitch>(track).value, isTrue);
  });
}
