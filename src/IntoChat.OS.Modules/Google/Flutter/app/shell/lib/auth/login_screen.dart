import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

final class LoginScreen extends StatefulWidget {
  const LoginScreen({
    super.key,
    required this.onSubmit,
    this.onRegister,
    this.initialUsername = '',
    this.errorMessage,
  });
  final Future<String?> Function(String username, String password) onSubmit;
  final Future<String?> Function(String username, String password)? onRegister;
  final String initialUsername;
  final String? errorMessage;
  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

/// Figma auth form: wordmark, eyebrow, heading and muted lead, then kit
/// fields 16 apart, an error message box and a full-width kit button.
final class _LoginScreenState extends State<LoginScreen> {
  late final _username = TextEditingController(text: widget.initialUsername);
  final _password = TextEditingController();
  final _confirmation = TextEditingController();
  final _passwordFocus = FocusNode();
  final _confirmationFocus = FocusNode();
  late String? _error = widget.errorMessage;
  bool _busy = false;
  bool _register = false;
  bool _reveal = false;
  // Validation shows after the first submit, then follows each edit.
  bool _validate = false;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    _confirmation.dispose();
    _passwordFocus.dispose();
    _confirmationFocus.dispose();
    super.dispose();
  }

  String? get _usernameError {
    if (!_validate) return null;
    final name = _username.text.trim();
    if (name.isEmpty) return 'Enter your username.';
    if (_register &&
        (name.length > 80 || !RegExp(r'^[a-z0-9][a-z0-9-]*$').hasMatch(name))) {
      return 'Use lowercase letters, numbers and hyphens.';
    }
    return null;
  }

  String? get _passwordError {
    if (!_validate) return null;
    final value = _password.text;
    if (value.isEmpty) return 'Enter your password.';
    if (_register && (value.length < 12 || value.length > 256)) {
      return 'Use 12–256 characters.';
    }
    return null;
  }

  String? get _confirmationError {
    if (!_validate || !_register) return null;
    final value = _confirmation.text;
    if (value.isEmpty) return 'Confirm your password.';
    if (value != _password.text) return 'Passwords do not match.';
    return null;
  }

  Future<void> _submit() async {
    if (_busy) return;
    setState(() => _validate = true);
    if (_usernameError != null ||
        _passwordError != null ||
        _confirmationError != null) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    String? failure;
    try {
      failure = await (_register ? widget.onRegister! : widget.onSubmit)(
        _username.text.trim(),
        _password.text,
      );
      if (failure == null) TextInput.finishAutofillContext();
    } on Object {
      failure = 'Unable to connect. Please try again.';
    }
    if (!mounted) return;
    setState(() {
      _busy = false;
      _error = failure;
    });
  }

  void _switchMode() {
    _password.clear();
    _confirmation.clear();
    setState(() {
      _register = !_register;
      _error = null;
      _reveal = false;
      _validate = false;
    });
  }

  void _edited(String _) {
    if (_validate) setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final colors = UiColors.of(context);
    final type = UiType.of(context);
    final tokens = UiTokens.of(context);
    return Scaffold(
      backgroundColor: colors.page,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 400),
              child: AutofillGroup(
                onDisposeAction: AutofillContextAction.cancel,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text('IntoChat', style: type.title),
                    const SizedBox(height: 52),
                    Text(
                      (_register ? 'Get started' : 'Welcome back')
                          .toUpperCase(),
                      style: type.menuLabel,
                    ),
                    const SizedBox(height: 7),
                    Text(
                      _register
                          ? 'Create your IntoChat account'
                          : 'Log in to IntoChat',
                      style: type.heading,
                    ),
                    const SizedBox(height: 9),
                    Text(
                      _register
                          ? 'Your own space to bring your ideas to life.'
                          : 'Use your account to continue.',
                      style: type.bodyMuted,
                    ),
                    const SizedBox(height: 28),
                    UiKitField(
                      key: const Key('login-username'),
                      label: 'Username',
                      controller: _username,
                      enabled: !_busy,
                      autofocus: true,
                      autofillHints: const [AutofillHints.username],
                      autocorrect: false,
                      textInputAction: TextInputAction.next,
                      error: _usernameError,
                      helper: _register
                          ? 'Lowercase letters, numbers and hyphens. Up to 80 characters.'
                          : null,
                      onChanged: _edited,
                      onSubmitted: (_) => _passwordFocus.requestFocus(),
                    ),
                    const SizedBox(height: 16),
                    UiKitField(
                      key: const Key('login-password'),
                      label: 'Password',
                      controller: _password,
                      focusNode: _passwordFocus,
                      enabled: !_busy,
                      secret: !_reveal,
                      autocorrect: false,
                      autofillHints: [
                        _register
                            ? AutofillHints.newPassword
                            : AutofillHints.password,
                      ],
                      textInputAction: _register
                          ? TextInputAction.next
                          : TextInputAction.done,
                      error: _passwordError,
                      helper: _register ? 'Use 12–256 characters.' : null,
                      onChanged: _edited,
                      onSubmitted: (_) => _register
                          ? _confirmationFocus.requestFocus()
                          : _submit(),
                      suffix: SizedBox.square(
                        dimension: 34,
                        child: IconButton(
                          tooltip: _reveal ? 'Hide password' : 'Show password',
                          padding: EdgeInsets.zero,
                          color: colors.muted,
                          style: IconButton.styleFrom(
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(
                                tokens.radiusSmall,
                              ),
                            ),
                          ),
                          onPressed: _busy
                              ? null
                              : () => setState(() => _reveal = !_reveal),
                          icon: Icon(
                            uiIcon(_reveal ? 'eye-off' : 'eye'),
                            size: uiIconSize,
                          ),
                        ),
                      ),
                    ),
                    if (_register) ...[
                      const SizedBox(height: 16),
                      UiKitField(
                        key: const Key('login-confirm-password'),
                        label: 'Confirm password',
                        controller: _confirmation,
                        focusNode: _confirmationFocus,
                        enabled: !_busy,
                        secret: !_reveal,
                        autocorrect: false,
                        autofillHints: const [AutofillHints.newPassword],
                        textInputAction: TextInputAction.done,
                        error: _confirmationError,
                        onChanged: _edited,
                        onSubmitted: (_) => _submit(),
                      ),
                    ],
                    if (_error case final error?) ...[
                      const SizedBox(height: 16),
                      Semantics(
                        liveRegion: true,
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 12,
                            vertical: 10,
                          ),
                          decoration: BoxDecoration(
                            color: colors.errorBg,
                            borderRadius: BorderRadius.circular(
                              tokens.radiusSmall,
                            ),
                          ),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Icon(
                                uiIcon('alert'),
                                size: uiIconSize,
                                color: colors.error,
                              ),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  error,
                                  key: const Key('login-error'),
                                  style: type.toast.copyWith(
                                    color: colors.strong,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: 16),
                    UiKitButton(
                      key: const Key('login-submit'),
                      label: _busy
                          ? (_register ? 'Creating account…' : 'Signing in…')
                          : (_register ? 'Create account' : 'Sign in'),
                      onPressed: _busy ? null : _submit,
                      fullWidth: true,
                    ),
                    if (widget.onRegister != null) ...[
                      const SizedBox(height: 24),
                      Wrap(
                        alignment: WrapAlignment.center,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        spacing: 4,
                        children: [
                          Text(
                            _register
                                ? 'Already have an account?'
                                : 'No account?',
                            style: type.bodyMuted,
                          ),
                          UiKitButton(
                            label: _register ? 'Sign in' : 'Create account',
                            style: UiKitButtonStyle.link,
                            onPressed: _busy ? null : _switchMode,
                          ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
