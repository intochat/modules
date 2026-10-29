import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('wide rows keep fixed and flexible controls reachable', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Align(
            alignment: Alignment.topLeft,
            child: SizedBox(
              width: 360,
              height: 64,
              child: NeuronView(
                kind: 'layout',
                name: 'row',
                load: (kind, name) async => kind == 'layout'
                    ? {
                        'mode': 'row',
                        'gap': 8,
                        'extents': [240, 0, 272],
                        'children': [
                          for (final id in ['left', 'center', 'right'])
                            {'kind': 'button', 'name': id},
                        ],
                      }
                    : {'label': name, 'action': name},
                onAction: (_) async {},
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    await tester.drag(
      find.byType(SingleChildScrollView).first,
      const Offset(-500, 0),
    );
    await tester.pumpAndSettle();
    expect(find.text('right').hitTestable(), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('viewport renders an app root without starting an operation', (
    tester,
  ) async {
    var actions = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'viewport',
            name: 'canvas',
            load: (kind, name) async => kind == 'viewport'
                ? {
                    'revision': 1,
                    'root': {'kind': 'text', 'name': 'app/text'},
                  }
                : {'version': 1, 'markdown': 'App-owned content'},
            onAction: (_) async {
              actions++;
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('App-owned content'), findsOneWidget);
    expect(actions, 0);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('recursive viewport is bounded', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'viewport',
            name: 'canvas',
            load: (_, _) async => {
              'revision': 1,
              'root': {'kind': 'viewport', 'name': 'canvas'},
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('This component cannot be displayed.'), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
  });
}
