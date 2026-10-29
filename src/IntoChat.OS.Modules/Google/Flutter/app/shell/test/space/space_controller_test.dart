import 'package:intochat_os_flutter_shell/space/space_controller.dart';
import 'package:flutter_test/flutter_test.dart';

import 'space_fixture.dart';

void main() {
  test('a conflict re-reads and retries once with the current revision and operation id', () async {
    final ui = SpaceClient();
    final scenes = SpaceScenes(ui);
    final controller = SpaceController(
      client: ui.client,
      scenes: scenes,
      brain: 'brain',
      watch: () => ui.changes.stream,
    );
    await controller.start();
    ui.conflict = true;
    await controller.show('brain/apps/files');
    expect(ui.calls.map((c) => c.revision), [0, 7]);
    expect(ui.calls[0].id, ui.calls[1].id);
    expect(ui.reads, 2);
    await controller.home();
    expect(ui.calls.last.id, isNot(ui.calls.first.id));
    controller.dispose();
    await ui.changes.close();
    ui.close();
  });
  test(
    'identical scene revision does not rebuild and reconnect keeps focus',
    () async {
      final ui = SpaceClient();
      final scenes = SpaceScenes(ui);
      final controller = SpaceController(
        client: ui.client,
        scenes: scenes,
        brain: 'brain',
        watch: () => ui.changes.stream,
      );
      await controller.start();
      var updates = 0;
      controller.view.addListener(() => updates++);
      await controller.refresh();
      expect(updates, 0);
      expect(controller.view.value.focused, 'brain/apps/ino');
      scenes.revision++;
      await controller.refresh();
      expect(updates, 1);
      expect(controller.view.value.animate, isFalse);
      controller.dispose();
      await ui.changes.close();
      ui.close();
    },
  );
  testWidgets('reconnect cuts to a newer scene without replaying arrival', (
    tester,
  ) async {
    final ui = SpaceClient();
    final scenes = SpaceScenes(ui);
    final controller = SpaceController(
      client: ui.client,
      scenes: scenes,
      brain: 'brain',
      watch: () => ui.changes.stream,
    );
    await controller.start();
    ui.changes.addError(StateError('disconnected'));
    await tester.pump();
    await ui.client.showNeuron('brain', 'brain/apps/files', 'remote', 0);
    scenes.revision++;
    await tester.pump(const Duration(seconds: 2));
    await tester.pump();
    expect(controller.view.value.focused, 'brain/apps/files');
    expect(controller.view.value.animate, isFalse);
    controller.dispose();
    await ui.changes.close();
    ui.client.close();
  });
}
