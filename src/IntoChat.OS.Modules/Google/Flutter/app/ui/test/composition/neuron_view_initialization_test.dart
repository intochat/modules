import 'dart:async';

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  for (final kind in ['text', 'surface']) {
    testWidgets(
      '$kind root reports uninitialized state until its first composition',
      (tester) async {
        final changes = StreamController<void>.broadcast();
        var composed = false;
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: NeuronView(
                kind: kind,
                name: 'root',
                watch: (_, _) => changes.stream,
                load: (type, name) async => type == 'surface'
                    ? {
                        'revision': composed ? 1 : 0,
                        'definition': {
                          'children': composed
                              ? [
                                  {'kind': 'text', 'name': 'content'},
                                ]
                              : [],
                        },
                      }
                    : {
                        'version': composed ? 1 : 0,
                        'markdown': composed ? 'Composed content' : '',
                      },
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('Preparing this view'), findsOneWidget);
        composed = true;
        changes.add(null);
        await tester.pumpAndSettle();
        expect(find.text('Preparing this view'), findsNothing);
        expect(find.textContaining('Composed content'), findsOneWidget);
        await tester.pumpWidget(const SizedBox());
        await changes.close();
      },
    );
  }

  testWidgets(
    'an intentionally empty composed root and empty children stay empty',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: NeuronView(
              kind: 'surface',
              name: 'root',
              load: (type, _) async => type == 'surface'
                  ? {
                      'revision': 1,
                      'definition': {
                        'children': [
                          {'kind': 'text', 'name': 'empty'},
                        ],
                      },
                    }
                  : {'version': 0, 'markdown': ''},
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Preparing this view'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    },
  );
}
