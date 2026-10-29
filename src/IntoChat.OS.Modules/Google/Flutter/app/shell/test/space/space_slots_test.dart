import 'package:intochat_os_flutter_shell/space/space_controller.dart';
import 'package:intochat_os_flutter_shell/space/space_slots.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('side and main use the typed neuron renderer', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Row(
            children: [
              SideSlot(
                reference: const UiChildRef('text', 'side'),
                load: (k, n) async => {'markdown': 'Side'},
              ),
              Expanded(
                child: MainSlot(
                  reference: const UiChildRef('text', 'main'),
                  load: (k, n) async => {'markdown': 'Main'},
                ),
              ),
            ],
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Side'), findsOneWidget);
    expect(find.text('Main'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });
}
