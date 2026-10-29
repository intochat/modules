import 'package:intochat_os_ui/src/theme/ui_theme.dart';
import 'package:intochat_os_ui/src/theme/ui_tokens.dart';
import 'package:intochat_os_ui/src/components/workspace/ui_workspace_components.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:forui/forui.dart';

Widget host(Widget child, {bool reduced = false, ThemeData? theme}) =>
    MaterialApp(
      theme: theme ?? UiTheme.workspaceDark('lumen'),
      home: MediaQuery(
        data: MediaQueryData(disableAnimations: reduced),
        child: Scaffold(body: child),
      ),
    );

void main() {
  testWidgets(
    'rapid navigation keeps only latest content and reduced motion settles it',
    (tester) async {
      Widget content(String id, {bool reduced = false}) => host(
        UiMotion(contentKey: id, child: Text(id)),
        reduced: reduced,
      );
      Animation<double> opacity() => tester
          .widget<FadeTransition>(
            find
                .descendant(
                  of: find.byType(UiMotion),
                  matching: find.byType(FadeTransition),
                )
                .first,
          )
          .opacity;
      await tester.pumpWidget(content('Home'));
      expect(opacity().value, 1);
      await tester.pumpWidget(content('Files'));
      await tester.pump(const Duration(milliseconds: 50));
      expect(opacity().value, inExclusiveRange(0, 1));
      await tester.pumpWidget(content('Assistant'));
      expect(find.text('Files'), findsNothing);
      expect(find.text('Home'), findsNothing);
      expect(find.text('Assistant'), findsOneWidget);
      await tester.pump(const Duration(milliseconds: 40));
      expect(opacity().value, inExclusiveRange(0, 1));
      await tester.pumpWidget(content('Assistant', reduced: true));
      expect(opacity().value, 1);
      expect(opacity().status, AnimationStatus.completed);
      await tester.pumpWidget(content('Assistant'));
      expect(opacity().value, 1);
      expect(opacity().status, AnimationStatus.completed);
    },
  );
  testWidgets('single root breadcrumb is visible and cannot navigate', (
    tester,
  ) async {
    final selected = <String>[];
    await tester.pumpWidget(
      host(
        UiBreadcrumbs(
          entries: const [UiBreadcrumbEntry(id: 'root', label: 'Storages')],
          onSelected: selected.add,
        ),
      ),
    );
    expect(tester.takeException(), isNull);
    expect(find.text('Storages'), findsOneWidget);
    expect(find.byType(PopupMenuButton<String>), findsNothing);
    expect(find.byIcon(Icons.chevron_right), findsNothing);
    expect(
      tester.widget<TextButton>(find.byType(TextButton)).onPressed,
      isNull,
    );
    await tester.tap(find.text('Storages'));
    expect(selected, isEmpty);
  });
  testWidgets('empty breadcrumbs render no navigation controls', (
    tester,
  ) async {
    await tester.pumpWidget(host(const UiBreadcrumbs(entries: [])));
    expect(tester.takeException(), isNull);
    expect(find.byType(TextButton), findsNothing);
    expect(find.byType(PopupMenuButton<String>), findsNothing);
    expect(find.byIcon(Icons.chevron_right), findsNothing);
    expect(tester.getSize(find.byType(UiBreadcrumbs)), Size.zero);
  });
  testWidgets('control bridge inherits selected palette', (tester) async {
    final theme = UiTheme.workspaceDark('lumen');
    late Color primary;
    await tester.pumpWidget(
      host(
        UiThemeScope(
          child: Builder(
            builder: (context) {
              primary = FTheme.of(context).colors.primary;
              return const Text('Control');
            },
          ),
        ),
        theme: theme,
      ),
    );
    expect(primary, theme.colorScheme.primary);
  });
  testWidgets('stable identity preserves editor state without replay', (
    tester,
  ) async {
    Widget content(String id) =>
        host(UiMotion(contentKey: id, child: const TextField()));
    await tester.pumpWidget(content('document'));
    final fade = tester.widget<FadeTransition>(
      find
          .descendant(
            of: find.byType(UiMotion),
            matching: find.byType(FadeTransition),
          )
          .first,
    );
    expect(fade.opacity.value, 1);
    expect(fade.opacity.status, AnimationStatus.completed);
    await tester.enterText(find.byType(TextField), 'draft');
    await tester.pumpWidget(content('document'));
    expect(find.text('draft'), findsOneWidget);
    await tester.pumpWidget(content('other'));
    await tester.pump(const Duration(milliseconds: 30));
    expect(tester.hasRunningAnimations, isTrue);
    await tester.pumpAndSettle();
  });
  for (final reduced in [true, false]) {
    testWidgets(
      'motion suppression for ${reduced ? 'accessibility' : 'reconnect'}',
      (tester) async {
        Widget content(String id) => host(
          UiMotion(contentKey: id, animate: reduced, child: Text(id)),
          reduced: reduced,
        );
        await tester.pumpWidget(content('before'));
        await tester.pumpWidget(content('after'));
        expect(find.text('before'), findsNothing);
        expect(find.text('after'), findsOneWidget);
        final fade = tester.widget<FadeTransition>(
          find
              .descendant(
                of: find.byType(UiMotion),
                matching: find.byType(FadeTransition),
              )
              .first,
        );
        expect(fade.opacity.value, 1);
        expect(fade.opacity.status, AnimationStatus.completed);
      },
    );
  }
  testWidgets(
    'collapsed ancestors navigate by identity and current location is inert',
    (tester) async {
      final selected = <String>[];
      await tester.pumpWidget(
        host(
          SizedBox(
            width: 260,
            child: UiBreadcrumbs(
              entries: const [
                UiBreadcrumbEntry(id: 'root', label: 'Files'),
                UiBreadcrumbEntry(id: 'a', label: 'Archive'),
                UiBreadcrumbEntry(id: 'b', label: 'Photos'),
                UiBreadcrumbEntry(id: 'c', label: 'Current'),
              ],
              maxVisible: 2,
              onSelected: selected.add,
            ),
          ),
        ),
      );
      await tester.tap(find.byTooltip('Show parent locations'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Archive'));
      await tester.pumpAndSettle();
      expect(selected, ['a']);
      await tester.tap(find.text('Current'));
      expect(selected, ['a']);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets('surface inherits custom theme spacing', (tester) async {
    final theme = UiTheme.workspace(Brightness.light)
        .copyWith(extensions: [const UiTokens(spaceLarge: 30)]);
    await tester.pumpWidget(
      host(const UiSurface(child: Text('Content')), theme: theme),
    );
    final padding = tester.widget<Padding>(
      find
          .descendant(
            of: find.byType(UiSurface),
            matching: find.byType(Padding),
          )
          .first,
    );
    expect(padding.padding, const EdgeInsets.all(30));
  });
}
