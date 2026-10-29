import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

void main() {
  testWidgets('the field and slots watch their own neuron change signals', (
    tester,
  ) async {
    final f = await ApplicationFixture.start(tester);
    expect(
      f.link.watches.any(
        (w) => w.$2.any((s) => s.types.contains('SceneChanged')),
      ),
      isTrue,
    );
    expect(
      f.link.watches.any(
        (w) => w.$2.any((s) => s.types.contains('SurfaceChanged')),
      ),
      isTrue,
    );
    await f.dispose();
  });
}
