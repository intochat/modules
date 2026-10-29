import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  final events = <Map<String, dynamic>>[];

  Future<void> pump(
    WidgetTester tester,
    String kind,
    Map<String, Map<String, dynamic>> definitions, {
    bool settle = true,
  }) async {
    events.clear();
    await tester.pumpWidget(
      MaterialApp(
        theme: ThemeData(extensions: const [UiColors.intochatLight]),
        home: Scaffold(
          body: NeuronView(
            key: UniqueKey(),
            kind: kind,
            name: 'n',
            load: (k, name) async => {
              'version': 1,
              'definition': definitions[name] ?? definitions[k] ?? {},
            },
            onAction: (event) async => events.add(event),
          ),
        ),
      ),
    );
    if (settle) {
      await tester.pumpAndSettle();
    } else {
      await tester.pump();
      await tester.pump();
    }
    expect(tester.takeException(), isNull);
    expect(find.text(RendererRegistry.fallbackLabel), findsNothing);
  }

  const c = UiColors.intochatLight;

  testWidgets('toggle shows label and state; tap dispatches flip', (
    tester,
  ) async {
    await pump(tester, 'toggle', {
      'n': {'label': 'Dark mode', 'on': true},
    });
    expect(find.text('Dark mode'), findsOneWidget);
    expect(tester.widget<UiToggle>(find.byType(UiToggle)).on, isTrue);
    await tester.tap(find.byType(UiToggle));
    await tester.pumpAndSettle();
    expect(events, [
      {'kind': 'toggle', 'name': 'n', 'action': 'flip'},
    ]);
  });

  testWidgets('toggle with missing label renders sanely', (tester) async {
    await pump(tester, 'toggle', {'n': {}});
    expect(tester.widget<UiToggle>(find.byType(UiToggle)).on, isFalse);
  });

  testWidgets('progress clamps value and supports indeterminate', (
    tester,
  ) async {
    await pump(tester, 'progress', {
      'n': {'determinate': true, 'value': 1.7, 'label': 'Upload'},
    });
    expect(find.text('Upload'), findsOneWidget);
    expect(
      tester
          .widget<LinearProgressIndicator>(find.byType(LinearProgressIndicator))
          .value,
      1.0,
    );
    await pump(tester, 'progress', {
      'n': {'determinate': false, 'value': .3, 'label': 'Working'},
    }, settle: false);
    expect(
      tester
          .widget<LinearProgressIndicator>(find.byType(LinearProgressIndicator))
          .value,
      isNull,
    );
  });

  testWidgets('wrong-typed fields render sanely', (tester) async {
    for (final (kind, definition) in [
      ('toggle', {'label': 3, 'on': 'yes'}),
      ('progress', {'determinate': true, 'value': '0.5', 'label': 7}),
      ('infobar', {'severity': 1, 'title': [], 'body': {}, 'visible': true}),
      ('image', {'url': 42, 'prompt': false}),
      ('expander', {'header': 9, 'expanded': 'true', 'children': 'x'}),
    ]) {
      await pump(tester, kind, {'n': definition});
    }
  });

  Color barColor(WidgetTester tester) =>
      (tester
                  .widget<DecoratedBox>(find.byKey(const Key('ui_info_bar')))
                  .decoration
              as BoxDecoration)
          .color!;

  testWidgets('infobar error uses errorBg and dismiss dispatches', (
    tester,
  ) async {
    await pump(tester, 'infobar', {
      'n': {
        'severity': 'error',
        'title': 'Failed',
        'body': 'Retry later',
        'visible': true,
      },
    });
    expect(find.text('Failed'), findsOneWidget);
    expect(find.text('Retry later'), findsOneWidget);
    expect(barColor(tester), c.errorBg);
    await tester.tap(find.byTooltip('Dismiss'));
    await tester.pumpAndSettle();
    expect(events, [
      {'kind': 'infobar', 'name': 'n', 'action': 'dismiss'},
    ]);
  });

  testWidgets('infobar unknown severity uses info style', (tester) async {
    await pump(tester, 'infobar', {
      'n': {'severity': 'weird', 'title': 'Hi', 'visible': true},
    });
    expect(barColor(tester), c.accentTint);
  });

  testWidgets('hidden infobar renders nothing', (tester) async {
    await pump(tester, 'infobar', {
      'n': {'severity': 'info', 'title': 'Gone', 'visible': false},
    });
    expect(find.text('Gone'), findsNothing);
  });

  testWidgets('image with empty url shows caption only', (tester) async {
    await pump(tester, 'image', {
      'n': {'url': '', 'mediaType': 'image/png', 'prompt': 'A cat'},
    });
    expect(find.text('A cat'), findsOneWidget);
    expect(find.byType(Image), findsNothing);
  });

  testWidgets('image refuses non-http urls', (tester) async {
    await pump(tester, 'image', {
      'n': {'url': 'file:///etc/passwd', 'prompt': 'Local'},
    });
    expect(find.byType(Image), findsNothing);
  });

  testWidgets('expander without children renders header', (tester) async {
    await pump(tester, 'expander', {
      'n': {'header': 'More', 'expanded': true},
    });
    expect(find.text('More'), findsOneWidget);
  });

  testWidgets('expanded expander renders child neurons; tap toggles', (
    tester,
  ) async {
    await pump(tester, 'expander', {
      'n': {
        'header': 'More',
        'expanded': true,
        'children': [
          {'kind': 'toggle', 'name': 'child'},
        ],
      },
      'child': {'label': 'Nested toggle', 'on': false},
    });
    expect(find.text('Nested toggle'), findsOneWidget);
    await tester.tap(find.text('More'));
    await tester.pumpAndSettle();
    expect(events, [
      {'kind': 'expander', 'name': 'n', 'action': 'toggle'},
    ]);
  });
}
