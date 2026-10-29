@Tags(['golden'])
library;

import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

final class _FakeEntries extends EntriesClient {
  _FakeEntries(this.entries, {this.failure})
    : super(
        IntoChatOSUiClient(baseUri: Uri.parse('http://localhost')),
        'brain-1',
      );
  final List<EntrySummary> entries;
  final Object? failure;

  @override
  Future<List<EntrySummary>> list() async {
    if (failure case final error?) throw error;
    return entries;
  }
}

final class _FakeLocal implements LocalPreferences {
  @override
  String? read(PreferenceStore store) => 'auto';

  @override
  Future<void> write(PreferenceStore store, String value) async {}
}

const _brain = 'brain-1';

Widget _entry(String scene) {
  final collection = scene == 'collection';
  final entries = switch (scene) {
    'key_set' => _FakeEntries([
      const EntrySummary(
        name: 'provider:openai',
        kind: EntryKind.secret,
        tail: 'ab12',
      ),
    ]),
    'member' => _FakeEntries(const [], failure: const SettingsForbidden()),
    'collection' => _FakeEntries([
      const EntrySummary(
        name: 'API_BASE',
        kind: EntryKind.variable,
        value: 'https://example.test',
      ),
      const EntrySummary(name: 'WEBHOOK', kind: EntryKind.secret, tail: '0001'),
    ]),
    _ => _FakeEntries(const []),
  };
  final name = collection
      ? '$_brain/settings/variables'
      : '$_brain/settings/keys-openai';
  return NeuronView(
    key: ValueKey(name),
    kind: 'entrycontrol',
    name: name,
    load: (kind, n) async => {
      'name': n,
      'version': 1,
      'spec': collection
          ? {'mode': 1, 'label': 'Variables and secrets'}
          : {'mode': 0, 'label': 'OpenAI', 'entryName': 'provider:openai'},
    },
    onAction: (event) async {},
    entries: (brain) => entries,
  );
}

Widget _preference(String scene) {
  final toggle = scene == 'toggle';
  return NeuronView(
    kind: 'preference',
    name: '$_brain/settings/pref',
    load: (kind, n) async => {
      'name': n,
      'version': 1,
      'spec': toggle
          ? {'store': 1, 'label': 'Notifications', 'kind': 0}
          : {
              'store': 14,
              'label': 'Default model',
              'kind': 1,
              'choices': [
                {'id': 'auto', 'label': 'Auto, cheapest model with tools'},
                {
                  'id': 'gpt',
                  'label': 'GPT',
                  'enabled': false,
                  'detail': 'Not available. Add a key in API keys or pick another model.',
                },
              ],
            },
      'value': toggle ? 'on' : 'auto',
    },
    onAction: (event) async {},
    localPreferences: _FakeLocal(),
  );
}

Widget _copy() => NeuronView(
  kind: 'view',
  name: 'share',
  load: (kind, name) async => {
    'generation': 1,
    'version': 1,
    'root': {
      'kind': 2,
      'key': 'root',
      'children': [
        {
          'kind': 1,
          'key': 'copy',
          'text': 'Copy link',
          'action': 'copy:https://example.test/#/open?app=fixture-app',
          'icon': 'copy',
        },
      ],
    },
  },
  watch: (kind, name) => const Stream.empty(),
  onAction: (event) async {},
);

void main() {
  if (!Platform.isWindows) {
    test(
      'kit control goldens',
      () {},
      skip: 'Goldens are Windows-rendered; Windows-local regression net only.',
    );
    return;
  }
  final themes = <String, ThemeData Function()>{
    'light': () => UiTheme.light('intochat'),
    'dark': () => UiTheme.dark('intochat'),
  };
  final sizes = <String, Size>{
    'wide': const Size(1440, 900),
    'narrow': const Size(390, 844),
  };
  final scenes = <String, Widget Function()>{
    'entry_key_not_set': () => _entry('key_not_set'),
    'entry_key_set': () => _entry('key_set'),
    'entry_member': () => _entry('member'),
    'entry_collection': () => _entry('collection'),
    'preference_toggle': () => _preference('toggle'),
    'preference_choice': () => _preference('choice'),
    'copy_toast': _copy,
  };
  for (final theme in themes.entries) {
    for (final size in sizes.entries) {
      for (final scene in scenes.entries) {
        final name = '${scene.key}_${size.key}_${theme.key}';
        testWidgets('kit control $name', (tester) async {
          tester.view.physicalSize = size.value;
          tester.view.devicePixelRatio = 1;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
            SystemChannels.platform,
            (call) async => null,
          );
          addTearDown(
            () => tester.binding.defaultBinaryMessenger
                .setMockMethodCallHandler(SystemChannels.platform, null),
          );
          debugDisableShadows = false;
          try {
            await tester.pumpWidget(
              MaterialApp(
                debugShowCheckedModeBanner: false,
                theme: theme.value(),
                home: Scaffold(
                  body: Align(
                    alignment: Alignment.topLeft,
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: SizedBox(
                        width: size.value.width - 48,
                        child: scene.value(),
                      ),
                    ),
                  ),
                ),
              ),
            );
            await tester.pumpAndSettle();
            if (scene.key == 'preference_choice') {
              await tester.tap(find.byKey(const Key('preference-choice')));
              await tester.pumpAndSettle();
            }
            if (scene.key == 'copy_toast') {
              await tester.tap(find.widgetWithText(UiKitButton, 'Copy link'));
              await tester.pump();
              await tester.pump(const Duration(milliseconds: 400));
            }
            expect(tester.takeException(), isNull);
            await expectLater(
              find.byType(MaterialApp),
              matchesGoldenFile('kit_controls_$name.png'),
            );
            if (scene.key == 'copy_toast') {
              await tester.pump(const Duration(seconds: 10));
              await tester.pumpAndSettle();
            }
          } finally {
            debugDisableShadows = true;
          }
        });
      }
    }
  }
}
