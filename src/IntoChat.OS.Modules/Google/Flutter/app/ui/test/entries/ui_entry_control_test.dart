import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

const _value = 'fixture-value-0001';

final class _FakeEntries extends EntriesClient {
  _FakeEntries(this.entries, [String brain = 'brain-1'])
    : super(IntoChatOSUiClient(baseUri: Uri.parse('http://localhost')), brain);
  List<EntrySummary> entries;
  Object? failure;
  final puts = <(String, EntryKind, String)>[];
  final removed = <String>[];
  static int lists = 0;

  @override
  Future<List<EntrySummary>> list() async {
    lists++;
    if (failure case final error?) throw error;
    return entries;
  }

  @override
  Future<void> put(String name, EntryKind kind, String value) async {
    if (failure case final error?) throw error;
    puts.add((name, kind, value));
    entries = [
      ...entries.where((e) => e.name != name),
      EntrySummary(name: name, kind: kind, tail: '0001'),
    ];
  }

  @override
  Future<void> remove(String name) async {
    if (failure case final error?) throw error;
    removed.add(name);
    entries = [...entries.where((e) => e.name != name)];
  }
}

Map<String, dynamic> _key(String name, [String label = 'OpenAI']) => {
  'name': name,
  'version': 1,
  'spec': {'mode': 0, 'label': label, 'entryName': 'provider:openai'},
};

Map<String, dynamic> _collection(String name) => {
  'name': name,
  'version': 1,
  'spec': {'mode': 1, 'label': 'Variables and secrets'},
};

Future<List<String>> _pump(
  WidgetTester tester,
  _FakeEntries entries, {
  bool collection = false,
  List<String>? brains,
  String label = 'OpenAI',
  _FakeEntries Function()? fresh,
}) async {
  final calls = <String>[];
  final name = collection
      ? 'brain-1/settings/variables'
      : 'brain-1/settings/keys-openai';
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: NeuronView(
          key: ValueKey(name),
          kind: 'entrycontrol',
          name: name,
          load: (kind, n) async {
            calls.add(jsonEncode({'load': kind, 'name': n}));
            return collection ? _collection(n) : _key(n, label);
          },
          onAction: (event) async => calls.add(jsonEncode(event)),
          entries: (brain) {
            brains?.add(brain);
            return fresh?.call() ?? entries;
          },
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return calls;
}

void main() {
  testWidgets('entry_control_key_shows_status_and_tail', (tester) async {
    final entries = _FakeEntries([
      const EntrySummary(
        name: 'provider:openai',
        kind: EntryKind.secret,
        tail: 'ab12',
      ),
    ]);
    final brains = <String>[];
    await _pump(tester, entries, brains: brains);
    expect(brains.toSet(), {'brain-1'});
    expect(find.text('OpenAI'), findsOneWidget);
    expect(find.text('Set ••••ab12'), findsOneWidget);
    expect(find.text('Replace'), findsOneWidget);
    expect(find.text('Remove'), findsOneWidget);

    entries.entries = [
      const EntrySummary(name: 'provider:openai', kind: EntryKind.secret),
    ];
    await tester.pumpWidget(const SizedBox());
    await _pump(tester, entries);
    expect(find.text('Set'), findsOneWidget);

    entries.entries = [];
    await tester.pumpWidget(const SizedBox());
    await _pump(tester, entries);
    expect(find.text('Not set'), findsOneWidget);
    expect(find.text('Add'), findsOneWidget);
    expect(find.text('Remove'), findsNothing);
  });

  testWidgets('entry_control_saves_through_the_client_then_calls_changed', (
    tester,
  ) async {
    final entries = _FakeEntries([]);
    final calls = await _pump(tester, entries);
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(EditableText).last, _value);
    await tester.tap(find.text('Save key'));
    await tester.pumpAndSettle();
    expect(find.text('Saved.'), findsOneWidget);
    expect(entries.puts, [('provider:openai', EntryKind.secret, _value)]);
    expect(
      calls,
      contains(
        jsonEncode({
          'kind': 'entrycontrol',
          'name': 'brain-1/settings/keys-openai',
          'action': 'changed',
        }),
      ),
    );
    expect(find.text('Set ••••0001'), findsOneWidget);

    await tester.tap(find.text('Remove'));
    await tester.pumpAndSettle();
    expect(find.text('Remove the OpenAI key?'), findsOneWidget);
    expect(
      find.text(
        'Models from OpenAI stop working in this brain until you add a key again.',
      ),
      findsOneWidget,
    );
    await tester.tap(find.text('Remove').last);
    await tester.pumpAndSettle();
    expect(entries.removed, ['provider:openai']);
    expect(find.text('Not set'), findsOneWidget);
  });

  testWidgets('entry_control_member_sees_owner_only_copy', (tester) async {
    final entries = _FakeEntries([])..failure = const SettingsForbidden();
    await _pump(tester, entries);
    expect(
      find.text('Only the brain owner can change API keys.'),
      findsOneWidget,
    );
    await _pump(tester, entries, collection: true);
    expect(
      find.text('Only the brain owner can change variables and secrets.'),
      findsOneWidget,
    );
  });

  testWidgets('entry_control_forbidden_clears_when_the_brain_changes', (
    tester,
  ) async {
    final forbidden = _FakeEntries([])..failure = const SettingsForbidden();
    final fine = _FakeEntries([], 'brain-2');
    var current = forbidden;
    Future<void> show(String brain) => tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'entrycontrol',
            name: 'brain-1/settings/keys-openai',
            load: (kind, n) async => _key(n),
            onAction: (event) async {},
            entries: (id) => brain == 'a' ? current : fine,
          ),
        ),
      ),
    );
    await show('a');
    await tester.pumpAndSettle();
    expect(
      find.text('Only the brain owner can change API keys.'),
      findsOneWidget,
    );
    current = fine;
    await show('b');
    await tester.pumpAndSettle();
    expect(
      find.text('Only the brain owner can change API keys.'),
      findsNothing,
    );
    expect(
      tester
          .widget<UiKitButton>(find.widgetWithText(UiKitButton, 'Add'))
          .onPressed,
      isNotNull,
    );
  });

  testWidgets('entry_control_never_dispatches_the_value', (tester) async {
    final entries = _FakeEntries([]);
    final calls = await _pump(tester, entries);
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(EditableText).last, _value);
    await tester.tap(find.text('Save key'));
    await tester.pumpAndSettle();
    expect(entries.puts, hasLength(1));
    expect(calls.where((c) => c.contains('"action"')), isNotEmpty);
    expect(calls.where((c) => c.contains(_value)), isEmpty);
    expect(find.textContaining(_value), findsNothing);
  });

  testWidgets('entry_control_collection_hides_provider_entries', (
    tester,
  ) async {
    final entries = _FakeEntries([
      const EntrySummary(name: 'provider:openai', kind: EntryKind.secret),
      const EntrySummary(
        name: 'API_BASE',
        kind: EntryKind.variable,
        value: 'https://example.test',
      ),
      const EntrySummary(name: 'TOKEN', kind: EntryKind.secret, tail: 'x9z8'),
    ]);
    await _pump(tester, entries, collection: true);
    expect(find.text('provider:openai'), findsNothing);
    expect(find.text('API_BASE'), findsOneWidget);
    expect(find.text('https://example.test'), findsOneWidget);
    expect(find.text('Variable'), findsWidgets);
    expect(find.text('TOKEN'), findsOneWidget);
    expect(find.text('Secret'), findsWidgets);

    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    final fields = find.byType(EditableText);
    await tester.enterText(fields.first, 'bad name!');
    await tester.enterText(fields.last, 'fixture-value-0002');
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    expect(find.text('Use letters, digits, _ . - only'), findsOneWidget);
    expect(entries.puts, isEmpty);
    await tester.enterText(fields.first, 'REGION');
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    expect(entries.puts, [
      ('REGION', EntryKind.variable, 'fixture-value-0002'),
    ]);
    expect(find.text('REGION'), findsOneWidget);
  });

  testWidgets('entry_control_failure_is_generic', (tester) async {
    final entries = _FakeEntries([]);
    await _pump(tester, entries);
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    entries.failure = UiRequestException(
      'PUT',
      '/brains/brain-1/entries/provider:openai',
      500,
      '{"detail":"internal detail text"}',
    );
    await tester.enterText(find.byType(EditableText).last, _value);
    await tester.tap(find.text('Save key'));
    await tester.pump();
    expect(find.text('Could not save that change.'), findsWidgets);
    expect(find.textContaining('internal detail'), findsNothing);
    expect(find.textContaining('500'), findsNothing);
    await tester.pumpAndSettle();
  });

  String fieldText(WidgetTester tester, Key key) => tester
      .widget<EditableText>(
        find.descendant(
          of: find.byKey(key),
          matching: find.byType(EditableText),
        ),
      )
      .controller
      .text;

  testWidgets('entry_control_key_dialog_clears_the_secret_on_failure', (
    tester,
  ) async {
    final entries = _FakeEntries([]);
    await _pump(tester, entries);
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    entries.failure = UiRequestException('PUT', '/x', 500, '');
    await tester.enterText(find.byType(EditableText).last, _value);
    await tester.tap(find.text('Save key'));
    await tester.pumpAndSettle();
    expect(find.text('Could not save that change.'), findsWidgets);
    expect(fieldText(tester, const Key('entry-key-field')), isEmpty);
  });

  testWidgets('entry_control_collection_dialog_clears_the_value_on_failure', (
    tester,
  ) async {
    final entries = _FakeEntries([]);
    await _pump(tester, entries, collection: true);
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    entries.failure = UiRequestException('PUT', '/x', 500, '');
    await tester.enterText(find.byType(EditableText).first, 'TOKEN');
    await tester.tap(find.text('Secret').last);
    await tester.pump();
    await tester.enterText(find.byType(EditableText).last, _value);
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    expect(fieldText(tester, const Key('entry-value-field')), isEmpty);
    expect(fieldText(tester, const Key('entry-name-field')), 'TOKEN');
  });

  testWidgets('entry_control_never_dispatches_the_value_across_failures', (
    tester,
  ) async {
    final entries = _FakeEntries([]);
    final calls = await _pump(tester, entries);
    void clean() {
      expect(calls.where((c) => c.contains(_value)), isEmpty);
      expect(find.textContaining(_value), findsNothing);
    }

    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    entries.failure = UiRequestException('PUT', '/x', 500, _value);
    await tester.enterText(find.byType(EditableText).last, _value);
    await tester.tap(find.text('Save key'));
    await tester.pumpAndSettle();
    clean();
    entries.failure = null;
    await tester.enterText(find.byType(EditableText).last, _value);
    await tester.tap(find.text('Save key'));
    await tester.pumpAndSettle();
    clean();
    await tester.tap(find.text('Remove'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Remove').last);
    await tester.pumpAndSettle();
    expect(entries.removed, ['provider:openai']);
    clean();

    final variables = _FakeEntries([]);
    final more = await _pump(tester, variables, collection: true);
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    variables.failure = const SettingsForbidden();
    await tester.enterText(find.byType(EditableText).first, 'TOKEN');
    await tester.tap(find.text('Secret').last);
    await tester.pump();
    await tester.enterText(find.byType(EditableText).last, _value);
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    expect(
      find.text('Only the brain owner can change variables and secrets.'),
      findsNWidgets(2),
    );
    expect(find.text('Could not save that change.'), findsNothing);
    calls.addAll(more);
    clean();
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();
    final add = tester.widget<UiKitButton>(find.byKey(const Key('entry-add')));
    expect(add.onPressed, isNull);
  });

  testWidgets('entry_control_rebuild_with_an_equal_client_does_not_refetch', (
    tester,
  ) async {
    final shared = _FakeEntries([]);
    _FakeEntries.lists = 0;
    await _pump(tester, shared, fresh: () => _FakeEntries(shared.entries));
    final first = _FakeEntries.lists;
    expect(first, 1);
    await _pump(tester, shared, fresh: () => _FakeEntries(shared.entries));
    await tester.pump(const Duration(seconds: 5));
    await tester.pumpAndSettle();
    expect(_FakeEntries.lists, first);
  });

  testWidgets('entry_control_key_label_comes_from_the_known_providers', (
    tester,
  ) async {
    await _pump(tester, _FakeEntries([]), label: 'Your bank password');
    expect(find.text('OpenAI'), findsOneWidget);
    expect(find.text('Your bank password'), findsNothing);
  });
}
