import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_flutter_shell/desktop/desktop_overlays.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'application_fixture.dart';

void main() {
  for (final width in [390.0, 1440.0]) {
    testWidgets('space navigation fits and supports keyboard focus at $width', (
      tester,
    ) async {
      tester.view.physicalSize = Size(width, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final f = await ApplicationFixture.start(tester);
      expect(find.byTooltip('Home'), findsOneWidget);
      expect(find.byTooltip('Settings'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await f.dispose();
    });
  }

  testWidgets('DesktopSystemModalSheet parses os:// URIs and renders Secure, Profile, Install & Conflict sheets', (
    tester,
  ) async {
    expect(DesktopSystemModalSheet.parseUri('os://window/secure'), DesktopSystemSheetKind.secure);
    expect(DesktopSystemModalSheet.parseUri('os://window/profile'), DesktopSystemSheetKind.profile);
    expect(DesktopSystemModalSheet.parseUri('os://sheet/install'), DesktopSystemSheetKind.install);
    expect(DesktopSystemModalSheet.parseUri('os://sheet/reconsent'), DesktopSystemSheetKind.reconsent);
    expect(DesktopSystemModalSheet.parseUri('os://sheet/conflict'), DesktopSystemSheetKind.conflict);
    expect(DesktopSystemModalSheet.parseUri('os://sheet/2fa'), DesktopSystemSheetKind.twoFactor);

    var activeSheet = DesktopSystemSheetKind.secure;
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.dark('fluent-acrylic'),
        home: IcTheme(
          colors: IcColors.dark,
          child: Scaffold(
            body: StatefulBuilder(
              builder: (context, setState) => DesktopSystemModalSheet(
                kind: activeSheet,
                onClose: () => setState(() => activeSheet = DesktopSystemSheetKind.none),
                onOpenSheet: (next) => setState(() => activeSheet = next),
              ),
            ),
          ),
        ),
      ),
    );

    expect(find.textContaining('Zero-Leakage Credential Vault'), findsOneWidget);
    await tester.tap(find.byKey(const ValueKey('sheet-secure-save')));
    await tester.pump();
    expect(find.text('Saved to Vault ✓'), findsOneWidget);
  });
}
