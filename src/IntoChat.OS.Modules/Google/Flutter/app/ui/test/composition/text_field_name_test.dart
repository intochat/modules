import 'dart:async';
import 'dart:ui' show Tristate;

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';

import 'package:flutter_test/flutter_test.dart';

void main() {
  for (final extent in [64.0, 200.0]) {
    testWidgets('a text field keeps its accessible name after typing '
        '(extent $extent)', (tester) async {
      final handle = tester.ensureSemantics();
      final changes = StreamController<void>.broadcast();
      addTearDown(changes.close);
      var value = '';
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SizedBox(
              height: extent,
              child: NeuronView(
                kind: 'textfield',
                name: 'filter',
                load: (kind, name) async => {
                  'label': 'Filter names',
                  'kind': 'text',
                  'value': value,
                },
                watch: (kind, name) => changes.stream,
                onAction: (event) async {
                  if (event['kind'] == 'textfield') {
                    value = event['value'] as String;
                    changes.add(null);
                  }
                },
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      final field = find.byKey(const ValueKey('textfield:filter'));
      expect(tester.getSemantics(field).label, 'Filter names');
      await tester.enterText(field, 'nonexistent-e2e-file');
      await tester.pumpAndSettle();
      final node = tester.getSemantics(field);
      expect(node.value, 'nonexistent-e2e-file');
      expect(node.label, 'Filter names');
      expect(node.flagsCollection.isEnabled, Tristate.isTrue);
      handle.dispose();
    });
  }
}
