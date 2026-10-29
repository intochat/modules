import 'package:intochat_os_flutter_shell/space/neuron_space.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter_test/flutter_test.dart';

import '../application_fixture.dart';

// Settings in the space layout saves keys, variables and device preferences as the root view does: every
// NeuronView the space mounts gets the person's entries client and the device's preference store.
void main() {
  testWidgets('space slots carry entries and local preferences', (
    tester,
  ) async {
    final fixture = await ApplicationFixture.start(tester);
    await fixture.openApp('settings');
    final space = tester.widget<NeuronSpace>(find.byType(NeuronSpace));
    expect(space.entries, isNotNull);
    expect(space.entries!('brain-1').brainId, 'brain-1');
    expect(space.localPreferences, isNotNull);
    final views = tester.widgetList<NeuronView>(find.byType(NeuronView));
    expect(views, isNotEmpty);
    for (final view in views) {
      expect(view.entries, isNotNull);
      expect(view.localPreferences, same(space.localPreferences));
    }
    await fixture.dispose();
  });
}
