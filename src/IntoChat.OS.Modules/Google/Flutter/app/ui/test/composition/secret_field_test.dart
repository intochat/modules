import 'dart:async';

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const marker = '\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022';

void main() {
  testWidgets('a secret field never echoes its stored marker into the text', (
    tester,
  ) async {
    var value = '';
    final changes = StreamController<void>.broadcast();
    addTearDown(changes.close);
    final dispatched = <String>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'surface',
            name: 'surface',
            load: (kind, name) async => kind == 'surface'
                ? {
                    'children': [
                      {'kind': 'textfield', 'name': 'pw'},
                    ],
                  }
                : {'label': 'Password', 'kind': 'password', 'value': value},
            watch: (kind, name) => changes.stream,
            onAction: (event) async {
              if (event['kind'] == 'textfield') {
                dispatched.add(event['value'] as String);
              }
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    final field = find.byKey(const ValueKey('textfield:pw'));
    await tester.enterText(field, 'a');
    await tester.pumpAndSettle();
    value = marker;
    changes.add(null);
    await tester.pumpAndSettle();
    final text = tester
        .widget<TextField>(find.byType(TextField))
        .controller!
        .text;
    expect(text, 'a');
    await tester.enterText(field, '${text}b');
    await tester.pumpAndSettle();
    expect(dispatched, ['a', 'ab']);
    expect(dispatched.any((v) => v.contains('\u2022')), isFalse);
    await tester.enterText(field, '');
    changes.add(null);
    await tester.pumpAndSettle();
    expect(find.text('A value is set'), findsOneWidget);
  });
}
