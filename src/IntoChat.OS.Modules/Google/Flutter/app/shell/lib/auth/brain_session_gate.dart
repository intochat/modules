import 'dart:async';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';

import 'credential_store_io.dart'
    if (dart.library.html) 'credential_store_web.dart'
    as store;
import 'login_screen.dart';
import 'local_account.dart';
import 'local_account_io.dart'
    if (dart.library.html) 'local_account_web.dart'
    as local;

/// Builds the signed-in shell once a client is available.
typedef ShellBuilder = Widget Function(
  IntoChatOSUiClient? client,
  String? statusMessage,
  Future<void> Function() switchAccount,
);

/// Creates a client for the given credentials, or throws if the kernel
/// location cannot be resolved.
typedef ClientFactory = IntoChatOSUiClient Function(
  BasicCredentials? credentials,
);

IntoChatOSUiClient _fromEnvironment(BasicCredentials? credentials) =>
    IntoChatOSUiClient.fromEnvironment(credentials: credentials);

// Verify the session before opening a workspace; connection failures remain retryable.
final class BrainSessionGate extends StatefulWidget {
  const BrainSessionGate({
    super.key,
    required this.builder,
    this.createClient = _fromEnvironment,
    this.readLocalAccount = local.readLocalAccount,
  });

  final ShellBuilder builder;
  final ClientFactory createClient;
  final Future<LocalAccount?> Function(Uri) readLocalAccount;

  @override
  State<BrainSessionGate> createState() => _BrainSessionGateState();
}

enum _Phase {
  probing,
  login,
  ready,
  logoutFailed,
  localFailed,
  connectionFailed,
}

final class _BrainSessionGateState extends State<BrainSessionGate> {
  _Phase _phase = _Phase.probing;
  IntoChatOSUiClient? _client;
  String? _statusMessage;
  String? _loginError;
  String _lastUsername = '';

  @override
  void initState() {
    super.initState();
    final stored = store.readStoredCredentials();
    _lastUsername = stored?.username ?? '';
    unawaited(_attempt(stored, fromStartup: true));
  }

  /// Returns an error message when the credentials were rejected, else null.
  Future<String?> _attempt(
    BasicCredentials? credentials, {
    bool fromStartup = false,
  }) async {
    final IntoChatOSUiClient client;
    try {
      client = widget.createClient(credentials);
    } on Object {
      if (mounted) setState(() => _phase = _Phase.connectionFailed);
      return 'Unable to connect to your account.';
    }
    if (fromStartup && credentials == null) {
      try {
        final account = await widget.readLocalAccount(client.baseUri);
        if (account != null) {
          await client.signInLocalAccount(
            account.principalId,
            account.password,
          );
          _enterShell(client, null);
          return null;
        }
      } on Object {
        client.close();
        if (mounted) setState(() => _phase = _Phase.localFailed);
        return 'Could not open your local account.';
      }
    }

    bool accepted;
    try {
      accepted = await client.checkAuth();
    } on Object {
      client.close();
      if (mounted) setState(() => _phase = _Phase.connectionFailed);
      return 'Unable to connect to your account.';
    }
    if (!accepted) {
      final inLobby = await _enterLobby(client);
      if (!inLobby) client.close();
      if (credentials != null) {
        store.clearStoredCredentials();
      }
      if (mounted && !inLobby) {
        setState(() {
          _phase = _Phase.login;
          _loginError = fromStartup && credentials == null
              ? null
              : 'Sign in failed. Check the username and password.';
        });
      }
      return 'Sign in failed. Check the username and password.';
    }

    try {
      await client.readSession();
      if (client.principalId == null ||
          client.accountId == null ||
          client.defaultBrainId == null) {
        final inLobby = await _enterLobby(client);
        if (!inLobby) client.close();
        if (mounted && !inLobby) setState(() => _phase = _Phase.login);
        return null;
      }
    } on Object {
      client.close();
      if (mounted) {
        setState(() {
          _phase = _Phase.login;
          _loginError = 'Could not load your account. Please sign in again.';
        });
      }
      return 'Could not load your account. Please sign in again.';
    }
    if (credentials != null) {
      store.writeStoredCredentials(credentials);
    }
    _enterShell(client, null);
    return null;
  }

  // An anonymous visitor of a deployment with a lobby opens the lobby session; any other host keeps the
  // login screen. Entry is never a redirect to a server-supplied URL.
  Future<bool> _enterLobby(IntoChatOSUiClient client) async {
    if (!await client.readLobbySession() || !mounted) return false;
    client.onLobbyEnded = () async {
      // No lobby state survives: close the lobby client, then start again from /identity/session.
      client.close();
      if (!mounted) return;
      setState(() {
        _phase = _Phase.probing;
        _client = null;
      });
      await _attempt(store.readStoredCredentials());
    };
    _enterShell(client, null);
    return true;
  }

  void _enterShell(IntoChatOSUiClient? client, String? statusMessage) {
    if (!mounted) {
      client?.close();
      return;
    }
    setState(() {
      _phase = _Phase.ready;
      _client = client;
      _statusMessage = statusMessage;
    });
  }

  Future<String?> _signIn(
    String username,
    String password, {
    bool register = false,
  }) async {
    _lastUsername = username;
    final client = widget.createClient(null);
    try {
      await client.signIn(username, password, register: register);
      store.clearStoredCredentials();
      _enterShell(client, null);
      return null;
    } on Object {
      client.close();
      if (!register) {
        // The bootstrap administrator can still use the configured Basic credential.
        final basic = widget.createClient(
          BasicCredentials(username: username, password: password),
        );
        try {
          if (await basic.checkAuth()) {
            // An open local server also answers true: only accept Basic if anonymous access is denied.
            final anonymous = widget.createClient(null);
            final gated = !(await anonymous.checkAuth());
            anonymous.close();
            if (gated) {
              store.writeStoredCredentials(
                BasicCredentials(username: username, password: password),
              );
              _enterShell(basic, null);
              return null;
            }
          }
        } on Object {
          /* Show the same authentication error. */
        }
        basic.close();
      }
      return register
          ? 'Account creation failed. Use a unique lowercase username and a password of at least 12 characters.'
          : 'Sign in failed. Check the username and password.';
    }
  }

  Future<void> _switchAccount() async {
    final previous = _client;
    setState(() {
      _phase = _Phase.probing;
      _client = null;
    });
    // Remove the shell and cancel its subscriptions before replacing the cookie.
    await WidgetsBinding.instance.endOfFrame;
    try {
      await previous?.signOut();
      store.clearStoredCredentials();
      final inLobby = previous != null && await _enterLobby(previous);
      if (!inLobby) previous?.close();
      if (mounted && !inLobby) {
        setState(() {
          _phase = _Phase.login;
          _loginError = null;
        });
      }
    } on Object {
      if (mounted) {
        setState(() {
          _phase = _Phase.logoutFailed;
          _client = previous;
        });
      }
    }
  }

  @override
  void dispose() {
    _client?.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_phase == _Phase.ready) {
      return widget.builder(_client, _statusMessage, _switchAccount);
    }

    return MaterialApp(
      title: 'IntoChat',
      debugShowCheckedModeBanner: false,
      // v2 is the default for everyone and the gate runs before user
      // settings load, so it always uses the IntoChat theme.
      theme: UiTheme.light('intochat'),
      darkTheme: UiTheme.dark('intochat'),
      themeMode: UiTheme.modeFor('intochat'),
      home: _phase == _Phase.localFailed || _phase == _Phase.connectionFailed
          ? Scaffold(
              body: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      _phase == _Phase.localFailed
                          ? 'Could not open your local account.'
                          : 'Unable to connect to your account.',
                    ),
                    TextButton(
                      onPressed: () {
                        setState(() => _phase = _Phase.probing);
                        unawaited(
                          _attempt(
                            store.readStoredCredentials(),
                            fromStartup: true,
                          ),
                        );
                      },
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
            )
          : _phase == _Phase.logoutFailed
          ? Scaffold(
              body: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text('Could not end the session.'),
                    TextButton(
                      onPressed: _switchAccount,
                      child: const Text('Retry sign out'),
                    ),
                  ],
                ),
              ),
            )
          : _phase == _Phase.login
          ? LoginScreen(
              onSubmit: _signIn,
              onRegister: (username, password) =>
                  _signIn(username, password, register: true),
              initialUsername: _lastUsername,
              errorMessage: _loginError,
            )
          : ColoredBox(
              color: UiColors.of(context).page,
              child: const Center(child: CircularProgressIndicator()),
            ),
    );
  }
}
