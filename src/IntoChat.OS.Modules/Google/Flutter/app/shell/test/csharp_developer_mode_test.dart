import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

void main() {
  for (final developer in [false, true]) {
    testWidgets(
      'native C# files are exposed only with server developer capability $developer',
      (tester) async {
        final f = await ApplicationFixture.start(
          tester,
          server: ApplicationServer()..developerMode = developer,
        );
        await tester.tap(find.byTooltip('Profile'));
        await tester.pumpAndSettle();
        expect(
          find.text('C# files'),
          developer ? findsOneWidget : findsNothing,
        );
        await f.dispose();
      },
    );
  }
}
