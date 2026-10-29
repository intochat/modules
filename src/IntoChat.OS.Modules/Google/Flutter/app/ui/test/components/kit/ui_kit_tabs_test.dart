import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  const c = UiColors.intochatLight;

  Future<void> host(WidgetTester tester, Widget child, {double w = 600}) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.intochat(Brightness.light),
        home: Scaffold(
          body: Align(
            alignment: Alignment.topLeft,
            child: SizedBox(width: w, child: child),
          ),
        ),
      ),
    );
    await tester.pump();
  }

  Border tabBorder(WidgetTester tester, int i) =>
      (tester
                      .widget<DecoratedBox>(
                        find.byKey(ValueKey('ui_kit_tab_$i')),
                      )
                      .decoration
                  as BoxDecoration)
              .border!
          as Border;

  testWidgets('active tab: 2 px accent bottom border, 700, strong', (
    tester,
  ) async {
    await host(
      tester,
      UiKitTabs(labels: const ['One', 'Two'], index: 0, onChanged: (_) {}),
    );
    final active = tabBorder(tester, 0);
    expect(active.bottom.width, 2);
    expect(active.bottom.color, c.accent);
    expect(tabBorder(tester, 1).bottom.color, Colors.transparent);
    final a = tester.widget<Text>(find.text('One')).style!;
    expect(a.fontWeight, FontWeight.w700);
    expect(a.color, c.strong);
    expect(a.fontSize, 12);
    final i = tester.widget<Text>(find.text('Two')).style!;
    expect(i.color, c.muted);
    expect(i.fontWeight, isNot(FontWeight.w700));
  });

  testWidgets('tap changes; disabled tab does not', (tester) async {
    int? got;
    await host(
      tester,
      UiKitTabs(
        labels: const ['One', 'Two', 'Later'],
        index: 0,
        disabled: const {2},
        onChanged: (v) => got = v,
      ),
    );
    await tester.tap(find.text('Later'));
    expect(got, isNull);
    await tester.tap(find.text('Two'));
    expect(got, 1);
  });

  testWidgets('scrolls horizontally when narrow', (tester) async {
    await host(
      tester,
      UiKitTabs(
        labels: const [
          'General',
          'AI models',
          'API keys',
          'Connections',
          'Variables and secrets',
          'Notifications',
        ],
        index: 0,
        onChanged: (_) {},
      ),
      w: 300,
    );
    expect(tester.takeException(), isNull);
    expect(find.byType(SingleChildScrollView), findsOneWidget);
  });

  testWidgets('active tab scrolls into view at 390', (tester) async {
    const labels = [
      'General',
      'AI models',
      'API keys',
      'Connections',
      'Variables and secrets',
      'Notifications',
      'Archived chats',
      'Danger zone',
    ];
    Widget tabs(int index) =>
        UiKitTabs(labels: labels, index: index, onChanged: (_) {});
    bool visible(int i) {
      final tab = tester.getRect(find.byKey(ValueKey('ui_kit_tab_$i')));
      return tab.left >= 0 && tab.right <= 390;
    }

    await host(tester, tabs(7), w: 390);
    await tester.pumpAndSettle();
    expect(visible(7), isTrue);
    await host(tester, tabs(0), w: 390);
    await tester.pumpAndSettle();
    expect(visible(0), isTrue);
  });
}
