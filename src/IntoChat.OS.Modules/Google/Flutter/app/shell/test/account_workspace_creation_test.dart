import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

void main() {
  testWidgets('creating a workspace selects only the server returned brain', (
    tester,
  ) async {
    final f = await ApplicationFixture.start(tester);
    await f.menu('New workspace');
    expect(f.space.brain, 'created-brain');
    expect(f.space.workspace!.path.first, 'created-brain');
    expect(f.server.requestsTo('/identity/brains').single.body, isEmpty);
    await f.dispose();
  });
}
