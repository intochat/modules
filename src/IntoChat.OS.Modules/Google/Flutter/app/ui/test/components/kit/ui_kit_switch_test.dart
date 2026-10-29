import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  const c = UiColors.intochatLight;

  Future<void> host(WidgetTester tester, Widget child) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.intochat(Brightness.light),
        home: Scaffold(body: Center(child: child)),
      ),
    );
    await tester.pumpAndSettle();
  }

  final trackKey = const ValueKey('ui_kit_switch_track');
  final thumbKey = const ValueKey('ui_kit_switch_thumb');

  Color trackColor(WidgetTester tester) =>
      (tester.widget<DecoratedBox>(find.byKey(trackKey)).decoration
              as BoxDecoration)
          .color!;

  double thumbDx(WidgetTester tester) =>
      tester.getTopLeft(find.byKey(thumbKey)).dx -
      tester.getTopLeft(find.byKey(trackKey)).dx;

  testWidgets('is 40x22 with the thumb at dx 0 off and dx 18 on', (
    tester,
  ) async {
    await host(tester, UiKitSwitch(value: false, onChanged: (_) {}));
    expect(tester.getSize(find.byKey(trackKey)), const Size(40, 22));
    expect(tester.getSize(find.byKey(thumbKey)), const Size(16, 16));
    // Padding 3 is the thumb's rest offset; "dx 0" is relative to it.
    final off = thumbDx(tester);
    expect(off, 3);
    expect(trackColor(tester), c.inputBorder);
    await host(tester, UiKitSwitch(value: true, onChanged: (_) {}));
    expect(thumbDx(tester) - off, 18);
    expect(trackColor(tester), c.accent);
  });

  testWidgets('tap calls onChanged with the inverse; null ignores taps', (
    tester,
  ) async {
    bool? got;
    await host(tester, UiKitSwitch(value: false, onChanged: (v) => got = v));
    await tester.tap(find.byType(UiKitSwitch));
    expect(got, isTrue);
    got = null;
    await host(tester, const UiKitSwitch(value: true, onChanged: null));
    await tester.tap(find.byType(UiKitSwitch));
    expect(got, isNull);
  });
}
