import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  Future<BuildContext> host(WidgetTester tester) async {
    late BuildContext context;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Builder(
            builder: (c) {
              context = c;
              return const SizedBox.expand();
            },
          ),
        ),
      ),
    );
    return context;
  }

  testWidgets('kit toast matches the Figma toast and hides after 4 s', (
    tester,
  ) async {
    final context = await host(tester);
    showKitToast(context, 'Saved');
    await tester.pump();
    final box = tester.widget<Container>(
      find.byKey(const ValueKey('ui_kit_toast')),
    );
    final decoration = box.decoration! as BoxDecoration;
    final c = UiColors.of(context);
    expect(decoration.color, c.strong);
    expect(decoration.borderRadius, BorderRadius.circular(10));
    expect(decoration.boxShadow, isNotEmpty);
    expect(
      box.padding,
      const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
    );
    final text = tester.widget<Text>(find.text('Saved'));
    expect(text.style!.color, c.page);
    expect(text.style!.fontSize, UiType.of(context).toast.fontSize);
    final rect = tester.getRect(find.byKey(const ValueKey('ui_kit_toast')));
    expect(
      tester.view.physicalSize.height / tester.view.devicePixelRatio -
          rect.bottom,
      22,
    );
    await tester.pump(kitToastLifetime);
    expect(find.text('Saved'), findsNothing);
  });

  testWidgets('a new kit toast replaces the previous one; action runs', (
    tester,
  ) async {
    final context = await host(tester);
    var undone = 0;
    showKitToast(context, 'First');
    showKitToast(
      context,
      'Second',
      tone: UiTone.error,
      actionLabel: 'Undo',
      onAction: () => undone++,
    );
    await tester.pump();
    expect(find.text('First'), findsNothing);
    expect(find.text('Second'), findsOneWidget);
    await tester.tap(find.text('Undo'));
    await tester.pump();
    expect(undone, 1);
    expect(find.text('Second'), findsNothing);
    await tester.pump(kitToastLifetime);
  });

  test('UiTone indices equal the C# wire ints', () {
    expect(UiTone.values.map((t) => t.index), [0, 1, 2, 3]);
    expect(UiTone.values.map((t) => t.name), [
      'neutral',
      'done',
      'working',
      'error',
    ]);
    for (var i = 0; i < 4; i++) {
      expect(UiTone.fromWire(i), UiTone.values[i]);
    }
    expect(UiTone.fromWire(9), UiTone.neutral);
    expect(UiTone.fromWire(null), UiTone.neutral);
  });

  testWidgets('tearing down the host with a toast up clears it cleanly', (
    tester,
  ) async {
    final context = await host(tester);
    showKitToast(context, 'Leaving', tone: UiTone.working);
    await tester.pump();
    expect(find.text('Leaving'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
    // No pending timer is left, and a later toast in a new host works.
    final next = await host(tester);
    showKitToast(next, 'Again');
    await tester.pump();
    expect(find.text('Again'), findsOneWidget);
    await tester.pump(kitToastLifetime);
    expect(find.text('Again'), findsNothing);
  });
}
