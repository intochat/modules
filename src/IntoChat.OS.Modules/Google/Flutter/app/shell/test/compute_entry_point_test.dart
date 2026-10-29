import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

void main() {
  testWidgets('the toolbar menu opens compute for the focused brain', (
    tester,
  ) async {
    final f = await ApplicationFixture.start(tester);
    await f.menu('Compute');
    expect(find.textContaining('Compute'), findsWidgets);
    expect(
      f.server.requests.any((r) => r.path == '/brains/${f.brainId}/compute'),
      isTrue,
    );
    await f.dispose();
  });
}
