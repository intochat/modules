import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  test('single-format gallery catalog covers all 5 clean categories and 13 LFP cards', () {
    final categories = galleryComponentSpecs.map((s) => s.category).toSet();
    expect(
      categories,
      equals({
        'Design & Identity',
        'Basic Input',
        'Layout & App Shell',
        'Data & 13 LFP Chat Cards',
        '3D Brain & Neurons',
      }),
    );

    final lfpSpecs = galleryComponentSpecs
        .where((s) => s.category == 'Data & 13 LFP Chat Cards')
        .map((s) => s.id)
        .toList();
    expect(lfpSpecs.length, 13);
    expect(
      lfpSpecs,
      containsAll([
        'table-card',
        'chart-card',
        'approval-card',
        'choice-card',
        'draft-card',
        'change-preview-card',
        'app-install-card',
        'record-card',
        'secret-card',
        'job-card',
        'remember-card',
        'file-cards',
        'error-line',
      ]),
    );
  });

  testWidgets('search, select component spec, mutate property knobs, and inspect dynamic code', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(home: Scaffold(body: UiGalleryScreen())),
    );
    await tester.pumpAndSettle();

    await tester.enterText(
      find.byKey(const Key('gallery_search')),
      'buttons',
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('gallery_sample_buttons')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('gallery_code_inspector')), findsOneWidget);
    expect(find.textContaining('ButtonStyle.Primary'), findsOneWidget);

    // Click Danger style knob -> C# snippet dynamically updates to ButtonStyle.Danger
    await tester.ensureVisible(find.byKey(const Key('knob_style_danger')));
    await tester.tap(find.byKey(const Key('knob_style_danger')));
    await tester.pumpAndSettle();
    expect(find.textContaining('ButtonStyle.Danger'), findsOneWidget);

    // Switch to Flutter Ic* code tab
    await tester.ensureVisible(find.byKey(const Key('gallery_tab_flutter')));
    await tester.tap(find.byKey(const Key('gallery_tab_flutter')));
    await tester.pumpAndSettle();
    expect(find.textContaining('danger: true'), findsOneWidget);

    // Fire Test [Signal]
    await tester.ensureVisible(find.byKey(const Key('gallery_fire_signal')));
    await tester.tap(find.byKey(const Key('gallery_fire_signal')));
    await tester.pumpAndSettle();
    expect(find.textContaining('[Signal] ButtonClickedSignal (1)'), findsOneWidget);

    // Return to gallery
    await tester.ensureVisible(find.byKey(const Key('gallery_back')));
    await tester.tap(find.byKey(const Key('gallery_back')));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.byKey(const Key('gallery_search')),
      'no-such-component',
    );
    await tester.pumpAndSettle();
    expect(find.text('No components found'), findsWidgets);
  });

  testWidgets('category dropdown filters specs by the 5 studio categories', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(home: Scaffold(body: UiGalleryScreen())),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('gallery_category')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('3D Brain & Neurons').last);
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('gallery_sample_brain-3d')), findsOneWidget);
    expect(find.byKey(const Key('gallery_sample_buttons')), findsNothing);
  });
}
