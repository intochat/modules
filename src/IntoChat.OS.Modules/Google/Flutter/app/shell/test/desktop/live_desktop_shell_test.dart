import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_flutter_shell/desktop/live_desktop_shell.dart';
import 'package:intochat_os_flutter_shell/desktop/os_bar.dart';
import 'package:intochat_os_flutter_shell/desktop/smart_desktop.dart';
import 'package:intochat_os_flutter_shell/desktop/window_frame.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  Map<String, dynamic> buildSampleDesktopTree() {
    return {
      'generation': 1,
      'root': {
        'kind': 2,
        'key': 'desktop',
        'extents': [0, 32],
        'children': [
          {
            'kind': 1,
            'key': 'windows',
            'children': [
              {
                'kind': 2,
                'key': 'frame-w-1',
                'extents': [40, 28, 0],
                'children': [
                  {
                    'kind': 1,
                    'key': 'bar-w-1',
                    'children': [
                      {
                        'kind': 3,
                        'key': 'focus-w-1',
                        'text': 'Ino',
                        'action': 'focus:w-1',
                        'enabled': true,
                      },
                      {
                        'kind': 3,
                        'key': 'close-w-1',
                        'text': 'Close',
                        'action': 'close:w-1',
                        'enabled': true,
                      },
                    ],
                  },
                  {
                    'kind': 1,
                    'key': 'tabs-w-1',
                    'children': [
                      {
                        'kind': 3,
                        'key': 'tab-w-1-t-1',
                        'text': 'Ino',
                        'action': 'tab:w-1:t-1',
                        'selected': true,
                        'enabled': true,
                      },
                    ],
                  },
                  {
                    'kind': 5,
                    'key': 'viewport-w-1',
                    'reference': {
                      'kind': 'view',
                      'name': 'brain-1/apps/ino/view',
                    },
                  },
                ],
              },
              {
                'kind': 2,
                'key': 'frame-w-2',
                'extents': [40, 28, 0],
                'children': [
                  {
                    'kind': 1,
                    'key': 'bar-w-2',
                    'children': [
                      {
                        'kind': 3,
                        'key': 'focus-w-2',
                        'text': 'Files',
                        'action': 'focus:w-2',
                        'enabled': true,
                      },
                      {
                        'kind': 3,
                        'key': 'close-w-2',
                        'text': 'Close',
                        'action': 'close:w-2',
                        'enabled': true,
                      },
                    ],
                  },
                  {
                    'kind': 1,
                    'key': 'tabs-w-2',
                    'children': [
                      {
                        'kind': 3,
                        'key': 'tab-w-2-t-1',
                        'text': 'Files',
                        'action': 'tab:w-2:t-1',
                        'selected': true,
                        'enabled': true,
                      },
                    ],
                  },
                  {
                    'kind': 5,
                    'key': 'viewport-w-2',
                    'reference': {
                      'kind': 'view',
                      'name': 'brain-1/apps/files/view',
                    },
                  },
                ],
              },
            ],
          },
          {
            'kind': 1,
            'key': 'bar',
            'children': [
              {
                'kind': 1,
                'key': 'dock',
                'children': [
                  {
                    'kind': 3,
                    'key': 'dock-ino',
                    'text': 'Ino',
                    'action': 'open:ino',
                    'enabled': true,
                  },
                  {
                    'kind': 3,
                    'key': 'dock-files',
                    'text': 'Files',
                    'action': 'open:files',
                    'enabled': true,
                  },
                  {
                    'kind': 3,
                    'key': 'dock-gallery',
                    'text': 'UI Gallery',
                    'action': 'open:gallery',
                    'enabled': true,
                  },
                ],
              },
            ],
          },
        ],
      },
    };
  }

  testWidgets(
    'LiveDesktopShell renders SmartDesktop, floating Acrylic WindowFrames, and OsBar',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1440, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final actions = <Map<String, dynamic>>[];
      final activations = <Map<String, dynamic>>[];

      await tester.pumpWidget(
        MaterialApp(
          theme: UiTheme.dark('fluent-acrylic'),
          builder: (context, child) => IcTheme(
            colors: IcColors.forPreset(IcStylePreset.fluentAcrylic, dark: true),
            child: child!,
          ),
          home: Scaffold(
            body: LiveDesktopShell(
              brain: 'brain-1',
              name: 'brain-1/apps/desktop/view',
              load: (kind, name) async {
                if (name == 'brain-1/apps/desktop/view') {
                  return buildSampleDesktopTree();
                }
                return {
                  'generation': 1,
                  'root': {
                    'kind': 2,
                    'key': 'app-body',
                    'children': [
                      {'kind': 6, 'key': 'txt', 'text': 'Viewport: $name'},
                    ],
                  },
                };
              },
              watch: (kind, name) => const Stream.empty(),
              onAction: (event) async => actions.add(event),
              onActivate: (action) => activations.add(action),
              stylePreset: IcStylePreset.fluentAcrylic,
              barLayout: BarLayout.floatingGlass,
              paletteOpen: false,
              onTogglePalette: () {},
              onSelectPreset: (_) {},
              onCycleBarLayout: () {},
              onOpenCompute: () {},
            ),
          ),
        ),
      );

      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));

      expect(find.byType(SmartDesktop), findsOneWidget);
      expect(find.byType(WindowFrame), findsNWidgets(2));
      expect(find.byType(OsBar), findsOneWidget);
      expect(find.text('Viewport: brain-1/apps/ino/view'), findsOneWidget);
      expect(find.text('Viewport: brain-1/apps/files/view'), findsOneWidget);

      // Clicking 3D Brain button in OsBar minimizes all windows to show 3D neural desktop
      await tester.tap(find.text('3D Brain'));
      await tester.pump();
      expect(find.byType(WindowFrame), findsNothing);

      // Clicking 3D Brain again restores windows
      await tester.tap(find.text('3D Brain'));
      await tester.pump();
      expect(find.byType(WindowFrame), findsNWidgets(2));
    },
  );
}
