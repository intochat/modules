import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

Map<String, dynamic> _button(
  String key,
  String text, {
  int style = 0,
  String? icon,
}) => {
  'kind': 1,
  'key': key,
  'text': text,
  'action': key,
  'style': style,
  'icon': icon,
};

Future<List<Map<String, dynamic>>> _pump(
  WidgetTester tester,
  Map<String, dynamic> root,
) => _pumpLive(tester, () => root, const Stream.empty());

Future<List<Map<String, dynamic>>> _pumpLive(
  WidgetTester tester,
  Map<String, dynamic> Function() root,
  Stream<void> changes, {
  int Function()? generation,
}) async {
  var version = 0;
  final sent = <Map<String, dynamic>>[];
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SizedBox(
          width: 800,
          height: 600,
          child: NeuronView(
            kind: 'view',
            name: 'nodes',
            load: (kind, name) async => {
              'generation': generation?.call() ?? 1,
              'version': ++version,
              'root': root(),
            },
            watch: (kind, name) => changes,
            onAction: (event) async => sent.add(event),
          ),
        ),
      ),
    ),
  );
  await tester.pump();
  await tester.pump();
  return sent;
}

Map<String, dynamic> _menu() => {
  'kind': 6,
  'key': 'menu',
  'text': 'More',
  'icon': 'more-horizontal',
  'children': [
    {'kind': 0, 'key': 'label', 'text': 'Chat'},
    _button('rename', 'Rename'),
    {'kind': 12, 'key': 'sep'},
    _button('archive', 'Archive', icon: 'archive'),
  ],
};

void main() {
  testWidgets('tapping a menu item dispatches its action once', (tester) async {
    final sent = await _pump(tester, _menu());
    await tester.tap(find.bySemanticsLabel('More'));
    await tester.pumpAndSettle();
    expect(find.text('CHAT'), findsOneWidget);
    await tester.tap(find.text('Archive'));
    await tester.pumpAndSettle();
    expect(sent, hasLength(1));
    expect(sent.single, containsPair('action', 'archive'));
    expect(sent.single, containsPair('kind', 'view'));
    expect(sent.single, containsPair('field', 'archive'));
    expect(find.text('Archive'), findsNothing);
  });

  testWidgets('Escape on an open menu dispatches nothing', (tester) async {
    final sent = await _pump(tester, _menu());
    await tester.tap(find.bySemanticsLabel('More'));
    await tester.pumpAndSettle();
    expect(find.text('Rename'), findsOneWidget);
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pumpAndSettle();
    expect(find.text('Rename'), findsNothing);
    expect(sent, isEmpty);
  });

  testWidgets('a button neuron in a menu hands over its own activation', (
    tester,
  ) async {
    final sent = <Map<String, dynamic>>[];
    final activated = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'view',
            name: 'nodes',
            load: (kind, name) async => kind == 'button'
                ? {'label': 'Settings', 'activation': '{"kind":"settings"}'}
                : {
                    'generation': 1,
                    'version': 1,
                    'root': {
                      'kind': 6,
                      'key': 'more',
                      'text': 'More',
                      'icon': 'more-horizontal',
                      'children': [
                        {'kind': 0, 'key': 'usage', 'text': r'$1.00'},
                        {
                          'kind': 5,
                          'key': 'more-settings',
                          'reference': {
                            'kind': 'button',
                            'name': 'brain/apps/desktop/settings',
                          },
                        },
                      ],
                    },
                  },
            watch: (kind, name) => const Stream.empty(),
            onAction: (event) async => sent.add(event),
            onActivate: activated.add,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.bySemanticsLabel('More'));
    await tester.pumpAndSettle();
    expect(find.text('Settings'), findsOneWidget);
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pumpAndSettle();
    expect(find.text('Settings'), findsNothing);
    expect(sent, isEmpty);
    expect(activated, isEmpty);
    await tester.tap(find.bySemanticsLabel('More'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Settings'));
    await tester.pumpAndSettle();
    expect(activated, [
      {'kind': 'settings'},
    ]);
    expect(sent, isEmpty);
    expect(find.text('Settings'), findsNothing);
  });

  testWidgets('modal Escape dispatches the cancel action', (tester) async {
    final sent = await _pump(tester, {
      'kind': 7,
      'key': 'confirm',
      'text': 'Delete chat?',
      'detail': 'This cannot be undone.',
      'children': [
        _button('delete', 'Delete', style: 3),
        _button('cancel', 'Cancel', style: 1),
      ],
    });
    await tester.pumpAndSettle();
    expect(find.text('Delete chat?'), findsOneWidget);
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pumpAndSettle();
    expect(sent.single, containsPair('action', 'cancel'));
  });

  testWidgets('a toast is gone after four seconds', (tester) async {
    final sent = await _pump(tester, {
      'kind': 8,
      'key': 'toast',
      'text': 'Chat archived',
      'children': [_button('undo', 'Undo')],
    });
    expect(find.text('Chat archived'), findsOneWidget);
    final undo = tester.getCenter(find.text('Undo'));
    await tester.pump(const Duration(seconds: 4));
    await tester.pump();
    expect(find.text('Chat archived'), findsNothing);
    expect(find.text('Undo'), findsNothing);
    await tester.tapAt(undo);
    await tester.pump();
    expect(sent, isEmpty);
  });

  testWidgets('a 500-row list builds lazily', (tester) async {
    await _pump(tester, {
      'kind': 9,
      'key': 'list',
      'children': [
        for (var i = 0; i < 500; i++)
          {'kind': 0, 'key': 'r$i', 'text': 'Row $i'},
      ],
    });
    expect(find.textContaining('Row ').evaluate().length, lessThan(50));
    expect(find.text('Row 0'), findsOneWidget);
  });

  testWidgets('unknown kind falls back and siblings still render', (
    tester,
  ) async {
    await _pump(tester, {
      'kind': 2,
      'key': 'root',
      'children': [
        {'kind': 99, 'key': 'odd'},
        {'kind': 0, 'key': 'ok', 'text': 'Still here'},
      ],
    });
    expect(find.text('This component cannot be displayed.'), findsOneWidget);
    expect(find.textContaining('Still here'), findsOneWidget);
  });

  testWidgets('an icon button exposes its text as semantics label', (
    tester,
  ) async {
    final handle = tester.ensureSemantics();
    final sent = await _pump(
      tester,
      _button('close', 'Close', style: 2, icon: 'x'),
    );
    expect(find.bySemanticsLabel('Close'), findsOneWidget);
    expect(find.text('Close'), findsNothing);
    await tester.tap(find.bySemanticsLabel('Close'));
    await tester.pump();
    expect(sent.single, containsPair('action', 'close'));
    handle.dispose();
  });

  testWidgets('badge, avatar and divider render their text', (tester) async {
    await _pump(tester, {
      'kind': 2,
      'key': 'root',
      'children': [
        {'kind': 10, 'key': 'b', 'text': 'Done', 'tone': 1},
        {'kind': 11, 'key': 'a', 'text': 'VH'},
        {'kind': 12, 'key': 'd', 'text': 'or'},
      ],
    });
    expect(find.text('Done'), findsOneWidget);
    expect(find.text('VH'), findsOneWidget);
    expect(find.text('or'), findsOneWidget);
  });

  testWidgets('closing a menu returns focus to its trigger', (tester) async {
    await _pump(tester, _menu());
    await tester.tap(find.bySemanticsLabel('More'));
    await tester.pumpAndSettle();
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pumpAndSettle();
    final trigger = Focus.of(
      tester.element(find.byIcon(uiIcon('more-horizontal')!)),
    );
    expect(trigger.hasPrimaryFocus, isTrue);
  });

  testWidgets('tapping outside an open menu dispatches nothing', (
    tester,
  ) async {
    final sent = await _pump(tester, _menu());
    await tester.tap(find.bySemanticsLabel('More'));
    await tester.pumpAndSettle();
    await tester.tapAt(const Offset(700, 500));
    await tester.pumpAndSettle();
    expect(find.text('Rename'), findsNothing);
    expect(sent, isEmpty);
  });

  Map<String, dynamic> modal(List<Map<String, dynamic>> buttons) => {
    'kind': 7,
    'key': 'confirm',
    'text': 'Delete chat?',
    'children': buttons,
  };

  testWidgets('modal action buttons are as wide as the modal content', (
    tester,
  ) async {
    await _pump(
      tester,
      modal([
        _button('delete', 'Delete', style: 3),
        _button('cancel', 'Cancel', style: 2),
      ]),
    );
    await tester.pumpAndSettle();
    final content = tester
        .getSize(find.byType(SingleChildScrollView).last)
        .width;
    for (final label in ['Delete', 'Cancel']) {
      final button = find.ancestor(
        of: find.text(label),
        matching: find.byType(UiKitButton),
      );
      expect(tester.getSize(button).width, content);
    }
  });

  testWidgets('modal backdrop dispatches only the cancel action, once', (
    tester,
  ) async {
    final sent = await _pump(
      tester,
      modal([
        _button('delete', 'Delete', style: 3),
        _button('cancel', 'Cancel', style: 2),
      ]),
    );
    await tester.pumpAndSettle();
    await tester.tapAt(const Offset(5, 5));
    await tester.pump();
    await tester.tapAt(const Offset(5, 5));
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pump();
    expect(sent, hasLength(1));
    expect(sent.single, containsPair('action', 'cancel'));
  });

  testWidgets('a modal in a short viewport scrolls and keeps cancel', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(360, 280);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final sent = await _pump(tester, {
      ...modal([
        _button('delete', 'Delete', style: 3),
        _button('cancel', 'Cancel', style: 1),
      ]),
      'detail': List.filled(20, 'This cannot be undone.').join(' '),
    });
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    await tester.tap(find.text('Cancel'));
    await tester.pump();
    expect(sent.single, containsPair('action', 'cancel'));
  });

  testWidgets('modal without a cancel button dispatches nothing', (
    tester,
  ) async {
    final sent = await _pump(
      tester,
      modal([_button('ok', 'OK'), _button('delete', 'Delete', style: 3)]),
    );
    await tester.pumpAndSettle();
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.tapAt(const Offset(5, 5));
    await tester.pump();
    expect(sent, isEmpty);
    expect(find.text('Delete chat?'), findsOneWidget);
  });

  testWidgets('a closed modal restores the previous focus', (tester) async {
    final changes = StreamController<void>.broadcast();
    var open = false;
    await _pumpLive(
      tester,
      () => {
        'kind': 2,
        'key': 'root',
        'children': [
          _button('open', 'Open'),
          if (open) modal([_button('cancel', 'Cancel', style: 1)]),
        ],
      },
      changes.stream,
    );
    final opener = Focus.of(tester.element(find.text('Open')));
    opener.requestFocus();
    await tester.pump();
    open = true;
    changes.add(null);
    await tester.pumpAndSettle();
    expect(find.text('Delete chat?'), findsOneWidget);
    expect(opener.hasPrimaryFocus, isFalse);
    open = false;
    changes.add(null);
    await tester.pumpAndSettle();
    expect(find.text('Delete chat?'), findsNothing);
    expect(opener.hasPrimaryFocus, isTrue);
    await changes.close();
  });

  testWidgets('a republished toast with the same key shows again', (
    tester,
  ) async {
    final changes = StreamController<void>.broadcast();
    var text = 'Chat archived';
    await _pumpLive(
      tester,
      () => {'kind': 8, 'key': 'toast', 'text': text},
      changes.stream,
    );
    await tester.pump(const Duration(seconds: 4));
    await tester.pump();
    expect(find.text('Chat archived'), findsNothing);
    text = 'Chat restored';
    changes.add(null);
    await tester.pump();
    await tester.pump();
    expect(find.text('Chat restored'), findsOneWidget);
    await tester.pump(const Duration(seconds: 4));
    await tester.pump();
    expect(find.text('Chat restored'), findsNothing);
    await changes.close();
  });

  testWidgets('the same toast text in a new generation shows again', (
    tester,
  ) async {
    final changes = StreamController<void>.broadcast();
    var generation = 1;
    await _pumpLive(
      tester,
      () => {'kind': 8, 'key': 'toast', 'text': 'Chat archived'},
      changes.stream,
      generation: () => generation,
    );
    await tester.pump(const Duration(seconds: 4));
    await tester.pump();
    expect(find.text('Chat archived'), findsNothing);
    generation++;
    changes.add(null);
    await tester.pump();
    await tester.pump();
    expect(find.text('Chat archived'), findsOneWidget);
    await tester.pump(const Duration(seconds: 4));
    await tester.pump();
    expect(find.text('Chat archived'), findsNothing);
    await changes.close();
  });

  testWidgets('a text-label menu returns focus to its button', (tester) async {
    await _pump(tester, {..._menu(), 'icon': null});
    await tester.tap(find.text('More'));
    await tester.pumpAndSettle();
    expect(find.text('Rename'), findsOneWidget);
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pumpAndSettle();
    expect(find.text('Rename'), findsNothing);
    final detector = tester.widget<FocusableActionDetector>(
      find.descendant(
        of: find.byType(UiKitButton),
        matching: find.byType(FocusableActionDetector),
      ),
    );
    expect(detector.focusNode, isNotNull);
    expect(detector.focusNode!.hasPrimaryFocus, isTrue);
  });

  testWidgets('tabs_node_renders_kit_tabs_and_dispatches_inactive_tab', (
    tester,
  ) async {
    final sent = await _pump(tester, {
      'kind': 13,
      'key': 'tabs',
      'children': [_button('a', 'Alpha'), _button('b', 'Beta', style: 2)],
    });
    final tabs = tester.widget<UiKitTabs>(find.byType(UiKitTabs));
    expect(find.byType(UiKitTabs), findsOneWidget);
    expect(tabs.labels, ['Alpha', 'Beta']);
    expect(tabs.index, 0);
    await tester.tap(find.text('Beta'));
    await tester.pumpAndSettle();
    expect(sent, hasLength(1));
    expect(sent.single, containsPair('action', 'b'));
    await tester.tap(find.text('Alpha'));
    await tester.pumpAndSettle();
    expect(sent, hasLength(1));
  });

  testWidgets('unknown_kind_still_shows_cannot_be_displayed', (tester) async {
    await _pump(tester, {'kind': 14, 'key': 'next'});
    expect(find.text('This component cannot be displayed.'), findsOneWidget);
  });
}
