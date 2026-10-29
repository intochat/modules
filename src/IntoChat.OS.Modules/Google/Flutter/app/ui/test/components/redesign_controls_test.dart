import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:intochat_os_ui/src/components/button/ui_neuron_select.dart';

void main() {
  const c = UiColors.intochatLight;

  Future<void> host(WidgetTester tester, Widget child) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.intochat(Brightness.light),
        home: Scaffold(body: Center(child: child)),
      ),
    );
    await tester.pump();
  }

  BoxDecoration decorationIn(WidgetTester tester, Finder of) {
    final box = tester.widget<DecoratedBox>(
      find.descendant(of: of, matching: find.byType(DecoratedBox)).first,
    );
    return box.decoration as BoxDecoration;
  }

  testWidgets('primary button: 48 high, radius 10, accent, 14/700', (
    tester,
  ) async {
    await host(tester, UiKitButton(label: 'Save', onPressed: () {}));
    expect(tester.getSize(find.byType(UiKitButton)).height, 48);
    final d = decorationIn(tester, find.byType(UiKitButton));
    expect(d.borderRadius, BorderRadius.circular(10));
    expect(d.color, c.accent);
    final text = tester.widget<Text>(find.text('Save'));
    expect(text.style?.fontSize, 14);
    expect(text.style?.fontWeight, FontWeight.w700);
    expect(text.style?.color, c.onAccent);
  });

  testWidgets('compact secondary button: 36 high, radius 8, inputBorder', (
    tester,
  ) async {
    await host(
      tester,
      UiKitButton(
        label: 'More',
        onPressed: () {},
        style: UiKitButtonStyle.secondary,
        compact: true,
      ),
    );
    expect(tester.getSize(find.byType(UiKitButton)).height, 36);
    final d = decorationIn(tester, find.byType(UiKitButton));
    expect(d.borderRadius, BorderRadius.circular(8));
    expect((d.border as Border).top.color, c.inputBorder);
    expect(d.color, c.surface);
    expect(tester.widget<Text>(find.text('More')).style?.fontSize, 13);
  });

  testWidgets('danger button is outlined: error border, strong text', (
    tester,
  ) async {
    await host(
      tester,
      UiKitButton(
        label: 'Delete',
        onPressed: () {},
        style: UiKitButtonStyle.danger,
      ),
    );
    final d = decorationIn(tester, find.byType(UiKitButton));
    expect(d.color, Colors.transparent);
    expect((d.border as Border).top.color, c.error);
    expect(tester.widget<Text>(find.text('Delete')).style?.color, c.strong);
  });

  testWidgets('disabled button has opacity 0.45', (tester) async {
    await host(tester, const UiKitButton(label: 'Off'));
    final opacity = tester.widget<Opacity>(
      find.descendant(
        of: find.byType(UiKitButton),
        matching: find.byType(Opacity),
      ),
    );
    expect(opacity.opacity, 0.45);
  });

  testWidgets('button style order mirrors the C# enum', (tester) async {
    expect(UiKitButtonStyle.values.map((s) => s.name), [
      'primary',
      'secondary',
      'ghost',
      'danger',
      'link',
    ]);
  });

  testWidgets('field: 48 high, radius 12; focus draws accent + 3px ring', (
    tester,
  ) async {
    await host(
      tester,
      const SizedBox(
        width: 300,
        child: UiKitField(label: 'Email', error: 'Required'),
      ),
    );
    final box = find.byKey(const ValueKey('ui_kit_field_box'));
    expect(tester.getSize(box).height, 48);
    var d = tester.widget<DecoratedBox>(box).decoration as BoxDecoration;
    expect(d.borderRadius, BorderRadius.circular(12));
    expect((d.border as Border).top.color, c.error);
    expect(find.text('Required'), findsOneWidget);
    expect(find.byKey(const ValueKey('ui_kit_field_ring')), findsNothing);

    await host(
      tester,
      const SizedBox(width: 300, child: UiKitField(label: 'Name')),
    );
    await tester.tap(find.byType(TextField));
    await tester.pump();
    d = tester.widget<DecoratedBox>(box).decoration as BoxDecoration;
    expect((d.border as Border).top.color, c.accent);
    final ring =
        tester
                .widget<DecoratedBox>(
                  find.byKey(const ValueKey('ui_kit_field_ring')),
                )
                .decoration
            as BoxDecoration;
    expect((ring.border as Border).top.width, 3);
    expect((ring.border as Border).top.color, c.focus);
  });

  Future<void> view(WidgetTester tester, Map root) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.intochat(Brightness.light),
        home: Scaffold(
          body: NeuronView(
            kind: 'view',
            name: 'v',
            load: (kind, name) async => {
              'generation': 1,
              'version': 1,
              'root': root,
            },
            onAction: (_) async {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('view Button node renders UiKitButton, Card a 14 surface', (
    tester,
  ) async {
    await view(tester, {
      'kind': 4,
      'key': 'card',
      'text': 'Card',
      'children': [
        {'kind': 1, 'key': 'go', 'text': 'Go', 'action': 'go'},
      ],
    });
    expect(find.widgetWithText(UiKitButton, 'Go'), findsOneWidget);
    expect(find.byType(TextButton), findsNothing);
    final surface = tester.widget<UiKitSurface>(find.byType(UiKitSurface));
    expect(surface.radius ?? 14, 14);
    final d = decorationIn(tester, find.byType(UiKitSurface));
    expect(d.borderRadius, BorderRadius.circular(14));
    expect((d.border as Border).top.color, c.hairline);
  });

  testWidgets('toggle track is 40x22', (tester) async {
    await host(
      tester,
      SizedBox(
        width: 300,
        child: UiToggle(label: 'Dark', on: true, onChanged: (_) {}),
      ),
    );
    expect(
      tester.getSize(find.byKey(const ValueKey('ui_toggle_track'))),
      const Size(40, 22),
    );
  });

  testWidgets('select trigger is 48 high with radius 12', (tester) async {
    await host(
      tester,
      UiNeuronSelect(
        label: 'Pick',
        options: const [
          {'id': 'a', 'label': 'Alpha'},
        ],
        selected: 'a',
        onSelected: (_) {},
      ),
    );
    final box = find.byKey(const ValueKey('ui_kit_select_box'));
    expect(tester.getSize(box).height, 48);
    final d = tester.widget<DecoratedBox>(box).decoration as BoxDecoration;
    expect(d.borderRadius, BorderRadius.circular(12));
  });

  testWidgets('field is named for screen readers above and folded', (
    tester,
  ) async {
    final handle = tester.ensureSemantics();
    await host(
      tester,
      const SizedBox(width: 300, child: UiKitField(label: 'Email')),
    );
    expect(find.bySemanticsLabel(RegExp('Email')), findsOneWidget);
    expect(
      tester.getSemantics(find.byType(TextField)).label,
      contains('Email'),
    );
    await host(
      tester,
      const SizedBox(
        width: 300,
        child: UiKitField(label: '', placeholder: 'Message'),
      ),
    );
    expect(
      tester.getSemantics(find.byType(TextField)).label,
      contains('Message'),
    );
    handle.dispose();
  });

  testWidgets('view Button in a slot under 48 renders compact', (tester) async {
    await view(tester, {
      'kind': 2,
      'key': 'main',
      'extents': [40, 0],
      'children': [
        {
          'kind': 3,
          'key': 'header',
          'extents': [0, 160],
          'children': [
            {'kind': 0, 'key': 'status', 'text': 'Ready'},
            {'kind': 1, 'key': 'new', 'text': 'New', 'action': 'new'},
          ],
        },
        {'kind': 0, 'key': 'rest', 'text': 'Body'},
      ],
    });
    expect(tester.takeException(), isNull);
    final button = tester.widget<UiKitButton>(find.byType(UiKitButton));
    expect(button.compact, isTrue);
    expect(tester.getSize(find.byType(UiKitButton)).height, 36);
  });
}
