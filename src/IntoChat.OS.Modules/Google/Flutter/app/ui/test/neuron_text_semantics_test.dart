import 'dart:async';

import 'package:intochat_os_ui/src/composition/neuron_view.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('text remains accessible after clearing and replacing it', (
    tester,
  ) async {
    final semantics = tester.ensureSemantics();
    final updates = StreamController<void>.broadcast();
    var text = '14';
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'text',
            name: 'brain/apps/calculator/display',
            load: (_, _) async => {'markdown': text, 'version': 1},
            watch: (_, _) => updates.stream,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.bySemanticsLabel('14'), findsOneWidget);
    text = '';
    updates.add(null);
    await tester.pumpAndSettle();
    expect(find.bySemanticsLabel('14'), findsNothing);
    text = 'Error';
    updates.add(null);
    await tester.pumpAndSettle();
    expect(find.bySemanticsLabel('Error'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
    await updates.close();
    semantics.dispose();
  });
}
