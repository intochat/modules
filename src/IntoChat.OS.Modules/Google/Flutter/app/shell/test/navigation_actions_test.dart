import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

void main() {
  testWidgets('account actions remain reachable in the toolbar menu', (
    tester,
  ) async {
    var switches = 0;
    final f = await ApplicationFixture.start(
      tester,
      onSwitchAccount: () async {
        switches++;
      },
    );
    await f.menu('Switch account');
    expect(switches, 1);
    await f.dispose();
  });
}
