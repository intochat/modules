import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('neuron enum values render an editable name and masked secret', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'form',
            name: 'copy-form',
            load: (_, _) async => {
              'title': 'Your copy',
              'revision': 4,
              'fields': [
                {
                  'name': 'name',
                  'label': 'App name',
                  'kind': 0,
                  'value': 'my-copy',
                },
                {
                  'name': 'secret',
                  'label': 'Secret',
                  'kind': 10,
                  'secretSet': true,
                },
              ],
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.text('my-copy'), findsOneWidget);
    expect(
      find.byWidgetPredicate((w) => w is TextField && w.obscureText),
      findsOneWidget,
    );
  });
}
