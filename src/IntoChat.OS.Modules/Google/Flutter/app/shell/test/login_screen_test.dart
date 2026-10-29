import 'dart:async';

import 'package:intochat_os_flutter_shell/auth/login_screen.dart';
import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets(
    'registration validates confirmation before sending credentials',
    (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: LoginScreen(
            onSubmit: (_, _) async => null,
            onRegister: (_, _) async {
              calls++;
              return null;
            },
          ),
        ),
      );
      await tester.tap(find.text('Create account'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('login-username')), 'alice');
      await tester.enterText(
        find.byKey(const Key('login-password')),
        'long-password-123',
      );
      await tester.ensureVisible(find.byKey(const Key('login-submit')));
      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pumpAndSettle();
      expect(calls, 0);
      expect(find.text('Confirm your password.'), findsOneWidget);
      await tester.enterText(
        find.byKey(const Key('login-confirm-password')),
        'different-password',
      );
      await tester.ensureVisible(find.byKey(const Key('login-submit')));
      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pumpAndSettle();
      expect(find.text('Passwords do not match.'), findsOneWidget);
      expect(calls, 0);
      await tester.enterText(
        find.byKey(const Key('login-confirm-password')),
        'long-password-123',
      );
      await tester.ensureVisible(find.byKey(const Key('login-submit')));
      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pumpAndSettle();
      expect(calls, 1);
    },
  );

  testWidgets(
    'failed request restores controls and permits retry without leaking exception',
    (tester) async {
      var calls = 0;
      final response = Completer<String?>();
      await tester.pumpWidget(
        MaterialApp(
          home: LoginScreen(
            onSubmit: (_, _) {
              calls++;
              return response.future;
            },
          ),
        ),
      );
      await tester.enterText(find.byKey(const Key('login-username')), 'alice');
      await tester.enterText(
        find.byKey(const Key('login-password')),
        'password',
      );
      await tester.ensureVisible(find.byKey(const Key('login-submit')));
      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pump();
      expect(
        tester
            .widget<UiKitButton>(find.byKey(const Key('login-submit')))
            .onPressed,
        isNull,
      );
      expect(calls, 1);
      response.completeError(Exception('secret backend details'));
      await tester.pumpAndSettle();
      expect(find.text('Unable to connect. Please try again.'), findsOneWidget);
      expect(
        tester
            .widget<UiKitButton>(find.byKey(const Key('login-submit')))
            .onPressed,
        isNotNull,
      );
      expect(find.textContaining('secret backend'), findsNothing);
      final errorText = tester.widget<Text>(
        find.byKey(const Key('login-error')),
      );
      expect(
        errorText.style?.color,
        UiColors.of(tester.element(find.byKey(const Key('login-error'))))
            .strong,
      );
    },
  );

  testWidgets('password reveal retains input and sign in autofill hints', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: LoginScreen(onSubmit: (_, _) async => 'Sign in failed.'),
      ),
    );
    await tester.enterText(find.byKey(const Key('login-password')), 'password');
    expect(
      tester
          .widget<TextField>(
            find.descendant(
              of: find.byKey(const Key('login-password')),
              matching: find.byType(TextField),
            ),
          )
          .obscureText,
      isTrue,
    );
    await tester.tap(find.byTooltip('Show password'));
    await tester.pump();
    final field = tester.widget<TextField>(
      find.descendant(
        of: find.byKey(const Key('login-password')),
        matching: find.byType(TextField),
      ),
    );
    expect(field.obscureText, isFalse);
    expect(field.controller!.text, 'password');
    expect(field.autofillHints, contains(AutofillHints.password));
  });
}
