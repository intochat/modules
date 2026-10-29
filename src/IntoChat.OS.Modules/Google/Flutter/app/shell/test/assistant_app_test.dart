import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

void main() {
  testWidgets('Ino opens its typed main slot and toolbar requests target Ino', (
    tester,
  ) async {
    final f = await ApplicationFixture.start(tester);
    expect(f.renderedSurface, '${f.brainId}/apps/ino/surface');
    expect(find.widgetWithText(TextField, 'Message'), findsWidgets);
    await tester.enterText(find.byKey(const ValueKey('dock-send')), 'hello');
    await tester.testTextInput.receiveAction(TextInputAction.send);
    await tester.pumpAndSettle();
    final request = f.server
        .requestsTo('/brains/${f.brainId}/apps/ino/request')
        .single;
    expect(request.body, {'operation': 'send', 'input': 'hello'});
    await f.dispose();
  });
}
