import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

void main() {
  testWidgets(
    'the shell reads the session brain without inventing a local project',
    (tester) async {
      final f = await ApplicationFixture.start(
        tester,
        server: ApplicationServer(brainId: 'owned'),
      );
      expect(f.space.brain, 'owned');
      expect(f.space.view.value.focused, 'owned/apps/ino');
      expect(f.server.requestsTo('/brains/owned'), isNotEmpty);
      await f.dispose();
    },
  );
}
