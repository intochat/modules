import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter_svg/flutter_svg.dart';

void main() {
  testWidgets('app-authored icon remains labeled, selected and clickable', (
    tester,
  ) async {
    Map<String, dynamic>? sent;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SizedBox(
            width: 52,
            height: 52,
            child: NeuronView(
              kind: 'button',
              name: 'app',
              load: (_, _) async => {
                'version': 1,
                'label': 'PhotoCraft',
                'action': 'open',
                'enabled': true,
                'visual': {
                  'compact': true,
                  'selected': true,
                  'running': true,
                  'svg': '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="8" fill="currentColor"/></svg>',
                },
              },
              onAction: (value) async {
                sent = value;
              },
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(SvgPicture), findsOneWidget);
    expect(find.byTooltip('PhotoCraft'), findsOneWidget);
    expect(find.text('PhotoCraft'), findsNothing);
    final selected = tester
        .widgetList<Semantics>(find.byType(Semantics))
        .any((s) => s.properties.selected == true);
    expect(selected, isTrue);
    await tester.tap(find.byTooltip('PhotoCraft'));
    await tester.pumpAndSettle();
    expect(sent?['action'], 'open');
    await tester.pumpWidget(const SizedBox());
  });
}
