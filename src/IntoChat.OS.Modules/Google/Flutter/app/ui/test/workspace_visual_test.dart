import 'dart:io';
import 'dart:ui' as ui;

import 'package:intochat_os_ui/src/chat/ui_chat.dart';
import 'package:intochat_os_ui/src/components/workspace/ui_workspace_components.dart';
import 'package:intochat_os_ui/src/theme/ui_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_chat_core/flutter_chat_core.dart';
import 'package:flutter_test/flutter_test.dart';

// These are component fixtures, not captures of live workspace state.
void main() {
  for (final palette in ['paper', 'midnight', 'lumen']) {
    for (final narrow in [false, true]) {
      testWidgets(
        'workspace surfaces and assistant render in $palette ${narrow ? 'narrow' : 'desktop'}',
        (tester) async {
          tester.view.physicalSize = narrow
              ? const Size(390, 1100)
              : const Size(1000, 640);
          tester.view.devicePixelRatio = 1;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          final fontPath = Platform.environment['WORKSPACE_VISUAL_FONT'];
          if (fontPath != null) {
            await tester.runAsync(() async {
              final loader = FontLoader(UiType.bodyFamily)
                ..addFont(
                  File(fontPath).readAsBytes().then(ByteData.sublistView),
                );
              await loader.load();
              final fallback = FontLoader('Roboto')
                ..addFont(
                  File(fontPath).readAsBytes().then(ByteData.sublistView),
                );
              await fallback.load();
            });
          }
          final iconPath = Platform.environment['WORKSPACE_VISUAL_ICONS'];
          if (iconPath != null) {
            await tester.runAsync(() async {
              final loader = FontLoader('MaterialIcons')
                ..addFont(
                  File(iconPath).readAsBytes().then(ByteData.sublistView),
                );
              await loader.load();
            });
          }
          final controller = InMemoryChatController(
            messages: [
              const TextMessage(
                id: 'request',
                authorId: 'user',
                text: 'Help me organize these files.',
              ),
              const TextMessage(
                id: 'response',
                authorId: 'assistant',
                text: 'Choose a folder in Files. I can help you decide how to organize its contents.',
              ),
            ],
          );
          addTearDown(controller.dispose);
          final captureKey = GlobalKey();
          final theme = palette == 'paper'
              ? UiTheme.workspace(Brightness.light)
              : UiTheme.workspaceDark(palette);
          await tester.pumpWidget(
            MaterialApp(
              theme: theme,
              home: RepaintBoundary(
                key: captureKey,
                child: Scaffold(
                  body: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Flex(
                      direction: narrow ? Axis.vertical : Axis.horizontal,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Text(
                                'Shared workspace components',
                                style: theme.textTheme.headlineSmall,
                              ),
                              const SizedBox(height: 8),
                              Text(
                                'Fixture preview · $palette',
                                style: theme.textTheme.bodySmall,
                              ),
                              const SizedBox(height: 24),
                              UiSurface(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      'Home',
                                      style: theme.textTheme.titleLarge,
                                    ),
                                    const SizedBox(height: 8),
                                    Text(
                                      'Apps, running work, and brain inspection',
                                      style: theme.textTheme.bodyMedium,
                                    ),
                                    const SizedBox(height: 16),
                                    FilledButton.icon(
                                      onPressed: () {},
                                      icon: const Icon(Icons.folder_outlined),
                                      label: const Text('Open Files'),
                                    ),
                                  ],
                                ),
                              ),
                              const SizedBox(height: 16),
                              UiSurface(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      'Files',
                                      style: theme.textTheme.titleLarge,
                                    ),
                                    UiBreadcrumbs(
                                      entries: const [
                                        UiBreadcrumbEntry(
                                          id: 'files',
                                          label: 'Files',
                                        ),
                                        UiBreadcrumbEntry(
                                          id: 'storage',
                                          label: 'Storage',
                                        ),
                                        UiBreadcrumbEntry(
                                          id: 'folder',
                                          label: 'Documents',
                                        ),
                                      ],
                                      onSelected: (_) {},
                                    ),
                                    Text(
                                      'Folder contents appear here.',
                                      style: theme.textTheme.bodyMedium,
                                    ),
                                    const ListTile(
                                      contentPadding: EdgeInsets.zero,
                                      leading: Icon(Icons.description_outlined),
                                      title: Text('Project notes.md'),
                                      subtitle: Text('Document · fixture'),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(width: 24, height: 24),
                        SizedBox(
                          width: 340,
                          height: narrow ? 420 : null,
                          child: UiSurface(
                            padding: EdgeInsets.zero,
                            child: Column(
                              children: [
                                Padding(
                                  padding: const EdgeInsets.all(16),
                                  child: Align(
                                    alignment: Alignment.centerLeft,
                                    child: Text(
                                      'Assistant',
                                      style: theme.textTheme.titleMedium,
                                    ),
                                  ),
                                ),
                                const Divider(height: 1),
                                Expanded(
                                  child: UiChat(
                                    chatController: controller,
                                    currentUserId: 'user',
                                    resolveUser: (id) async => User(id: id),
                                    workspaceTheme: true,
                                    onMessageSend: (_) {},
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          );
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
          expect(find.text('Assistant'), findsOneWidget);
          final output = Platform.environment['WORKSPACE_VISUAL_OUTPUT'];
          if (output != null) {
            await tester.runAsync(() async {
              final boundary =
                  captureKey.currentContext!.findRenderObject()!
                      as RenderRepaintBoundary;
              final image = await boundary.toImage();
              final data = await image.toByteData(
                format: ui.ImageByteFormat.png,
              );
              await Directory(output).create(recursive: true);
              await File(
                '$output/workspace-components-$palette-${narrow ? 'narrow' : 'desktop'}.png',
              ).writeAsBytes(data!.buffer.asUint8List());
              image.dispose();
            });
          }
          await tester.pumpWidget(const SizedBox.shrink());
        },
      );
    }
  }
}
