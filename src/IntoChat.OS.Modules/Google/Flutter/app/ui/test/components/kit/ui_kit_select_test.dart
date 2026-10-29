import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  const c = UiColors.intochatLight;
  const boxKey = ValueKey('ui_kit_select_box');

  Future<void> host(WidgetTester tester, Widget child) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.intochat(Brightness.light),
        home: Scaffold(
          body: Align(
            alignment: Alignment.topLeft,
            child: SizedBox(width: 600, child: child),
          ),
        ),
      ),
    );
    await tester.pump();
  }

  UiKitSelect<String> select({
    ValueChanged<String>? onChanged,
    String? value,
  }) => UiKitSelect<String>(
    value: value,
    label: 'Model',
    hint: 'Pick one',
    onChanged: onChanged,
    items: const [
      UiKitSelectItem('a', 'Alpha'),
      UiKitSelectItem('b', 'Beta', enabled: false, detail: 'Needs a key'),
      UiKitSelectItem('c', 'Gamma'),
    ],
  );

  testWidgets('fills a 600-wide parent at 48 high with radius 12', (
    tester,
  ) async {
    await host(tester, select(onChanged: (_) {}, value: 'a'));
    expect(tester.getSize(find.byKey(boxKey)), const Size(600, 48));
    final d =
        tester.widget<DecoratedBox>(find.byKey(boxKey)).decoration
            as BoxDecoration;
    expect(d.borderRadius, BorderRadius.circular(12));
    expect((d.border as Border).top.color, c.inputBorder);
    expect(find.text('Alpha'), findsOneWidget);
  });

  testWidgets('shows the hint with no value', (tester) async {
    await host(tester, select(onChanged: (_) {}));
    expect(find.text('Pick one'), findsOneWidget);
  });

  testWidgets('disabled items cannot be chosen', (tester) async {
    String? got;
    await host(tester, select(onChanged: (v) => got = v, value: 'a'));
    await tester.tap(find.byKey(boxKey));
    await tester.pumpAndSettle();
    expect(find.text('Needs a key'), findsOneWidget);
    await tester.tap(find.text('Beta'));
    await tester.pumpAndSettle();
    expect(got, isNull);
    await tester.tap(find.text('Gamma'));
    await tester.pumpAndSettle();
    expect(got, 'c');
  });

  testWidgets('focus draws a 3 px focus ring', (tester) async {
    await host(tester, select(onChanged: (_) {}, value: 'a'));
    expect(find.byKey(const ValueKey('ui_kit_select_ring')), findsNothing);
    await tester.sendKeyEvent(LogicalKeyboardKey.tab);
    await tester.pump();
    final ring = tester.widget<DecoratedBox>(
      find.byKey(const ValueKey('ui_kit_select_ring')),
    );
    final border = (ring.decoration as BoxDecoration).border as Border;
    expect(border.top.width, 3);
    expect(border.top.color, c.focus);
  });
}
