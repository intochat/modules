import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'desktop_shape.dart';

Finder _button(String label) => find.byWidgetPredicate(
  (widget) => widget is UiKitButton && widget.label == label,
);

Size buttonSize(WidgetTester tester, String label) =>
    tester.getSize(_button(label).first);

void main() {
  testWidgets('a button outside an extents slot sizes to its label', (
    tester,
  ) async {
    await pumpShape(tester, desktopShape());
    expect(tester.takeException(), isNull);
    for (final label in ['Files', 'Ino', 'Apps', 'Settings']) {
      for (final button in _button(label).evaluate()) {
        final text = find.descendant(
          of: find.byWidget(button.widget),
          matching: find.text(label),
        );
        // Label + 2 x 18 px padding (11 px when compact), plus the border and an optional icon (<= 28),
        // or the dock's explicit slot width when placed inside dockWidth().
        final maxExpected = [
          tester.getSize(text).width + 2 * 18 + 2 + 28,
          dockWidth(label, icon: true).toDouble(),
        ].reduce((a, b) => a > b ? a : b);
        expect(
          button.size!.width,
          lessThanOrEqualTo(maxExpected),
          reason: label,
        );
      }
    }
  });

  testWidgets('a button in an explicit extents slot fills it', (tester) async {
    await pumpShape(tester, desktopShape());
    expect(buttonSize(tester, 'Vlad').width, 72);
    expect(buttonSize(tester, 'New conversation').width, 160);
  });

  testWidgets('a compact button fits a 30 px slot', (tester) async {
    await pumpShape(tester, {
      'kind': 2,
      'key': 'bar',
      'extents': [30, 0],
      'children': [
        shapeButton('go', 'A very long action label that wraps'),
        {'kind': 0, 'key': 'rest', 'text': 'Rest'},
      ],
    }, size: const Size(160, 400));
    expect(tester.takeException(), isNull);
    expect(
      buttonSize(tester, 'A very long action label that wraps').height,
      30,
    );
    final text = tester.widget<Text>(
      find.text('A very long action label that wraps'),
    );
    expect(text.maxLines, 1);
  });
}
