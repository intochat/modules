import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:intochat_os_flutter_shell/space/space_toolbar.dart';
import 'package:intochat_os_flutter_shell/workspace/home/app_icon.dart';
import 'package:intochat_os_flutter_shell/space/neuron_space.dart';
import 'package:intochat_os_flutter_shell/space/space_controller.dart';
import 'package:intochat_os_flutter_shell/space/space_slots.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flame/game.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'space_fixture.dart';

void main() {
  Future<(SpaceClient, SpaceScenes, SpaceController)> mount(
    WidgetTester tester, {
    bool reduced = false,
    bool side = false,
    String? error,
    void Function(SpaceClient, SpaceScenes)? configure,
    SphereIconLoader? loadIcon,
  }) async {
    final ui = SpaceClient();
    final scenes = SpaceScenes(ui)
      ..side = side
      ..error = error;
    configure?.call(ui, scenes);
    final controller = SpaceController(
      client: ui.client,
      scenes: scenes,
      brain: 'brain',
      watch: () => ui.changes.stream,
    );
    await controller.start();
    await tester.pumpWidget(
      MaterialApp(
        home: MediaQuery(
          data: MediaQueryData(disableAnimations: reduced),
          child: Scaffold(
            body: NeuronSpace(
              controller: controller,
              loadIcon: loadIcon,
              load: (kind, name) async => kind == 'text'
                  ? {
                      'markdown': name.endsWith('/side')
                          ? 'Side content'
                          : 'Main content',
                    }
                  : await loadSlot(kind, name),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    addTearDown(() async {
      await tester.pumpWidget(const SizedBox());
      controller.dispose();
      await ui.changes.close();
      ui.close();
    });
    return (ui, scenes, controller);
  }

  testWidgets(
    'a fresh brain shows Ino focused with its main slot and the Home button',
    (tester) async {
      await mount(tester);
      expect(find.byType(SideSlot), findsNothing);
      expect(find.byType(MainSlot), findsOneWidget);
      expect(find.byTooltip('Home'), findsOneWidget);
      expect(find.text('Main content'), findsOneWidget);
    },
  );
  testWidgets('tapping a sphere posts Show and the slots swap after the zoom', (
    tester,
  ) async {
    final (ui, scenes, controller) = await mount(tester);
    final game = tester
        .widget<GameWidget<SceneGame>>(find.byType(GameWidget<SceneGame>))
        .game!;
    scenes.revision++;
    game.simulateTap('brain/apps/files');
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pumpAndSettle();
    expect(ui.calls.single.key, 'brain/apps/files');
    expect(scenes.reports, ['brain/apps/files']);
    expect(controller.view.value.focused, 'brain/apps/files');
  });
  testWidgets('Home pops to the brain', (tester) async {
    final (ui, scenes, _) = await mount(tester);
    scenes.revision++;
    await tester.tap(find.byTooltip('Home'));
    await tester.pumpAndSettle();
    expect(ui.workspace.path, ['brain']);
    expect(find.byType(MainSlot), findsNothing);
  });
  testWidgets('Home reverses the camera flight over 420 milliseconds', (
    tester,
  ) async {
    final (_, scenes, controller) = await mount(tester);
    final game = tester
        .widget<GameWidget<SceneGame>>(find.byType(GameWidget<SceneGame>))
        .game!;
    expect(game.state!.definition.camera.x, 176);
    scenes.revision++;
    await controller.home();
    await tester.pump();
    expect(game.state!.definition.camera.x, 176);
    await tester.pump(const Duration(milliseconds: 210));
    expect(
      game.state!.definition.camera.x,
      closeTo(176 * (1 - Curves.easeInOutCubic.transform(.5)), 0.1),
    );
    await tester.pump(const Duration(milliseconds: 210));
    expect(game.state!.definition.camera.x, 0);
  });
  testWidgets('reduced motion cuts the flight', (tester) async {
    final (_, scenes, controller) = await mount(tester, reduced: true);
    scenes.revision++;
    await controller.show('brain/apps/files');
    await tester.pump();
    expect(find.byType(MainSlot), findsOneWidget);
    final game = tester
        .widget<GameWidget<SceneGame>>(find.byType(GameWidget<SceneGame>))
        .game!;
    expect(game.state!.definition.camera.x, 320 + 800 * .22);
    expect(
      game.componentFor('brain/apps/files')!.scale.x,
      closeTo(2.2, 0.00001),
    );
    for (final transition in tester.widgetList<FadeTransition>(
      find.byType(FadeTransition),
    )) {
      expect(transition.opacity.value, 1);
    }
  });
  testWidgets('initial read failure offers retry and loads the scene', (
    tester,
  ) async {
    final (_, scenes, controller) = await mount(
      tester,
      configure: (_, scenes) => scenes.failRead = true,
    );
    expect(controller.view.value.scene.revision, -1);
    expect(find.text('Could not update your space.'), findsOneWidget);
    scenes.failRead = false;
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();
    expect(find.text('Could not update your space.'), findsNothing);
    expect(find.text('Main content'), findsOneWidget);
  });
  testWidgets('failed navigation keeps the scene and retries the intent', (
    tester,
  ) async {
    final (ui, scenes, controller) = await mount(tester);
    ui.failCommands = true;
    await expectLater(controller.show('brain/apps/files'), throwsA(anything));
    await tester.pumpAndSettle();
    expect(controller.view.value.focused, 'brain/apps/ino');
    expect(find.text('Main content'), findsOneWidget);
    expect(find.text('Could not update your space.'), findsOneWidget);
    ui.failCommands = false;
    scenes.revision++;
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();
    expect(controller.view.value.focused, 'brain/apps/files');
    expect(find.text('Could not update your space.'), findsNothing);
    expect(ui.calls[0].id, ui.calls[1].id);
  });
  for (final concurrent in [false, true]) {
    testWidgets(
      'failed Show survives successful unchanged-scene refresh concurrent=$concurrent',
      (tester) async {
        final (ui, scenes, controller) = await mount(tester);
        final release = Completer<void>();
        final entered = Completer<void>();
        Future<void>? reading;
        if (concurrent) {
          scenes.beforeRead = () async {
            entered.complete();
            await release.future;
          };
          reading = controller.refresh();
          await tester.pump();
          expect(entered.isCompleted, isTrue);
        }
        ui.failCommands = true;
        await expectLater(
          controller.show('brain/apps/files'),
          throwsA(anything),
        );
        final commandError = controller.error.value;
        expect(commandError, isNotNull);
        if (concurrent) {
          scenes.beforeRead = null;
          release.complete();
          await reading;
        } else {
          await controller.refresh();
        }
        await tester.pumpAndSettle();
        expect(controller.error.value, same(commandError));
        expect(controller.view.value.focused, 'brain/apps/ino');
        expect(find.text('Could not update your space.'), findsOneWidget);
        ui.failCommands = false;
        scenes.revision++;
        await tester.tap(find.text('Retry'));
        await tester.pumpAndSettle();
        expect(controller.view.value.focused, 'brain/apps/files');
        expect(ui.calls[0].id, ui.calls[1].id);
        expect(find.text('Could not update your space.'), findsNothing);
      },
    );
  }
  testWidgets('retry after a lost Back response does not pop twice', (
    tester,
  ) async {
    final (ui, scenes, controller) = await mount(tester);
    scenes.revision++;
    await controller.show('brain/apps/files');
    await tester.pumpAndSettle();
    ui.loseNextResponse = true;
    await expectLater(controller.back(), throwsA(anything));
    await tester.pumpAndSettle();
    expect(ui.workspace.path, ['brain', 'brain/apps/ino']);
    expect(controller.view.value.focused, 'brain/apps/files');
    scenes.revision++;
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();
    expect(ui.calls[1].id, ui.calls[2].id);
    expect(ui.workspace.path, ['brain', 'brain/apps/ino']);
    expect(controller.view.value.focused, 'brain/apps/ino');
    expect(find.text('Could not update your space.'), findsNothing);
  });
  testWidgets('same-focus scene updates preserve the active flight clock', (
    tester,
  ) async {
    final (_, scenes, controller) = await mount(tester);
    final game = tester
        .widget<GameWidget<SceneGame>>(find.byType(GameWidget<SceneGame>))
        .game!;
    scenes.revision++;
    await controller.show('brain/apps/files');
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 210));
    final halfway = game.state!.definition.camera.x;
    expect(
      halfway,
      closeTo(176 + 320 * Curves.easeInOutCubic.transform(.5), .1),
    );
    scenes.revision++;
    await controller.refresh(animate: true);
    await tester.pump();
    expect(game.state!.definition.camera.x, closeTo(halfway, .1));
    await tester.pump(const Duration(milliseconds: 105));
    expect(
      game.state!.definition.camera.x,
      closeTo(176 + 320 * Curves.easeInOutCubic.transform(.75), .1),
    );
    await tester.pump(const Duration(milliseconds: 105));
    expect(game.state!.definition.camera.x, 496);
  });
  testWidgets('toolbar manifest icons use the focused authenticated loader', (
    tester,
  ) async {
    final loads = <(String, String)>[];
    await mount(
      tester,
      configure: (_, scenes) {
        scenes.icon = 'mark.svg';
        scenes.toolbar = [
          {'operation': 'open', 'icon': 'mark.svg', 'label': 'Open'},
        ];
      },
      loadIcon: (neuron, icon) async {
        loads.add((neuron, icon));
        return Uint8List.fromList(
          utf8.encode(
            '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"><rect width="16" height="16" fill="red"/></svg>',
          ),
        );
      },
    );
    final icon = find.descendant(
      of: find.byType(SpaceToolbar),
      matching: find.byType(AppIcon),
    );
    expect(icon, findsOneWidget);
    expect(tester.widget<AppIcon>(icon).path, '/brains/brain/apps/ino/icon');
    expect(loads, contains(('brain/apps/ino', 'mark.svg')));
    expect(
      find.descendant(
        of: icon,
        matching: find.byKey(const ValueKey('app-icon-fallback')),
      ),
      findsNothing,
    );
  });
  testWidgets('each slot kind renders in its region', (tester) async {
    await mount(tester, side: true);
    expect(
      find.descendant(
        of: find.byType(SideSlot),
        matching: find.text('Side content'),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: find.byType(MainSlot),
        matching: find.text('Main content'),
      ),
      findsOneWidget,
    );
    expect(tester.getSize(find.byType(SideSlot)).width, 320);
  });
  testWidgets('an errored presentation shows attention and the error in main', (
    tester,
  ) async {
    await mount(tester, error: 'Could not prepare app');
    expect(find.bySemanticsLabel('Needs attention'), findsOneWidget);
    expect(
      find.descendant(
        of: find.byType(MainSlot),
        matching: find.text('Could not prepare app'),
      ),
      findsOneWidget,
    );
  });
}
