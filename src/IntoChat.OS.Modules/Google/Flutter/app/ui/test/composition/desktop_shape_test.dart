import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'package:intochat_os_ui/src/composition/value_nodes/view_menu.dart';

import 'desktop_shape.dart';

Finder _icon(String label) => find.byWidgetPredicate(
  (widget) => widget is ViewIconButton && widget.label == label,
);

void main() {
  const wide = Size(1440, 900);
  const narrow = Size(390, 844);

  testWidgets('desktop_window_has_header_tabs_and_bounded_content', (
    tester,
  ) async {
    await pumpShape(tester, desktopShape(), size: wide);
    expect(tester.takeException(), isNull);
    final strips = find.byType(UiKitTabs);
    expect(strips, findsNWidgets(2));
    for (var i = 0; i < 2; i++) {
      final tabs = tester.getRect(strips.at(i));
      // Windows start at the top: the header is the 40 px above the strip.
      expect(tabs.top, 40, reason: 'window $i header');
      expect(tabs.height, 38, reason: 'window $i tabs');
    }
    // Content fills from the strip's bottom to the 68 px dock.
    for (final text in ['Files content', 'Transcript']) {
      final content = tester.getRect(find.text(text));
      expect(content.top, greaterThanOrEqualTo(78), reason: text);
      expect(content.bottom, lessThanOrEqualTo(900 - 68), reason: text);
    }
    expect(find.text('Tab 3'), findsOneWidget);
  });

  testWidgets('desktop_chrome_icon_buttons_fit_32_slots', (tester) async {
    await pumpShape(tester, desktopShape(), size: wide);
    final strip = tester.getRect(find.byType(UiKitTabs).first);
    var chrome = 0;
    for (final label in ['New tab', 'Add to favourites', 'Pin', 'Close tab']) {
      for (final button in _icon(label).evaluate()) {
        final rect = tester.getRect(find.byWidget(button.widget));
        if (rect.bottom > strip.top) continue; // the dock's own icon button
        chrome++;
        expect(rect.width, 32, reason: label);
        expect(rect.height, lessThanOrEqualTo(40), reason: label);
      }
    }
    expect(chrome, 7); // Ino has no pin; Files has all four.
  });

  testWidgets('desktop_favourites_hidden_when_empty', (tester) async {
    await pumpShape(tester, desktopShape(), size: wide);
    expect(find.text('Notes'), findsNothing);
    await tester.pumpWidget(const SizedBox());
    await pumpShape(tester, desktopShape(favourites: 3), size: wide);
    // Labelled, packed left after Ino, and never clipped.
    expect(find.widgetWithText(UiKitButton, 'Notes'), findsNWidgets(2));
    expect(find.widgetWithText(UiKitButton, 'Report'), findsOneWidget);
    final ino = tester.getRect(find.widgetWithText(UiKitButton, 'Ino').last);
    final first = tester.getRect(
      find.widgetWithText(UiKitButton, 'Notes').first,
    );
    expect(first.left - ino.right, lessThanOrEqualTo(8));
    for (final label in ['Notes', 'Report']) {
      for (final text in find.text(label).evaluate()) {
        // An ellipsis or a wrap means the slot was too narrow for the name.
        final paragraph = tester.renderObject<RenderParagraph>(
          find.descendant(
            of: find.byWidget(text.widget),
            matching: find.byType(RichText),
          ),
        );
        expect(paragraph.didExceedMaxLines, isFalse, reason: label);
        final line = paragraph.getFullHeightForCaret(
          const TextPosition(offset: 0),
        );
        expect(paragraph.size.height, lessThan(line * 1.5), reason: label);
      }
    }
  });

  testWidgets(
    'desktop_narrow_390_shows_one_window_and_favourites_menu_without_overflow',
    (tester) async {
      await pumpShape(
        tester,
        desktopShape(narrow: true, favourites: 3),
        size: narrow,
      );
      expect(tester.takeException(), isNull);
      expect(find.byType(UiKitTabs), findsOneWidget);
      expect(find.text('Transcript'), findsNothing);
      expect(find.text('Files content'), findsOneWidget);
      expect(find.byType(ViewMenu), findsNWidgets(2)); // Favourites and More
      expect(find.text('Notes'), findsNothing);
      // Ino, the favourites menu, Files, New tab, Apps and More all sit inside 390 px.
      for (final finder in [
        find.widgetWithText(UiKitButton, 'Ino').last,
        find.bySemanticsLabel('Favourites'),
        find.widgetWithText(UiKitButton, 'Files').last,
        find.bySemanticsLabel('New tab').last,
        find.bySemanticsLabel('Apps'),
        find.bySemanticsLabel('More'),
      ]) {
        final rect = tester.getRect(finder);
        expect(rect.left, greaterThanOrEqualTo(0));
        expect(rect.right, lessThanOrEqualTo(390));
      }
      // The apps group fits beside More: nothing scrolls under it.
      expect(
        tester.getRect(find.bySemanticsLabel('Apps')).right,
        lessThanOrEqualTo(tester.getRect(find.bySemanticsLabel('More')).left),
      );
      expect(find.text('Classic layout'), findsNothing);
      await tester.tap(find.bySemanticsLabel('More'));
      await tester.pumpAndSettle();
      expect(find.text('Classic layout'), findsOneWidget);
      expect(find.text('Settings'), findsOneWidget);
    },
  );

  testWidgets('desktop_refusal_toast_shows_its_text', (tester) async {
    await pumpShape(
      tester,
      desktopShape(toast: 'A window holds at most 8 tabs.'),
      size: wide,
    );
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('A window holds at most 8 tabs.'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
