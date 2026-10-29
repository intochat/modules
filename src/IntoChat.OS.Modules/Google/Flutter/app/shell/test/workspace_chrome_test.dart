import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

void main() {
  for (final width in [390.0, 1440.0]) {
    testWidgets('space navigation fits and supports keyboard focus at $width', (
      tester,
    ) async {
      tester.view.physicalSize = Size(width, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final f = await ApplicationFixture.start(tester);
      expect(find.byTooltip('Home'), findsOneWidget);
      expect(find.byTooltip('Settings'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await f.dispose();
    });
  }
}
