import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'windows_storage_setup.dart';

String connectionStatus(Object? status) => switch (status) {
  'Connected' => 'Connected',
  'Expired' => 'Expired',
  'Failing' => 'Failing',
  'Disconnected' => 'Disconnected',
  _ => 'Unavailable',
};

typedef ConnectionsRequest = Future<Object?> Function(
  String path, {
  Map<String, Object?>? body,
});

/// The connection id a `{kind: connections}` activation names: its `connection` when well formed (the Apps app's
/// Add new names one per app and slot), else the connector's own id as the gallery's Connect always used.
String? connectWindowConnectionId(Map<String, dynamic> action) =>
    switch (action['connection']) {
      final String id when _connectionIdPattern.hasMatch(id) => id,
      _ => action['connector'] as String?,
    };

final _connectionIdPattern = RegExp(r'^[a-z0-9][a-z0-9_-]{0,127}$');

class ConnectWindow extends StatefulWidget {
  const ConnectWindow({
    super.key,
    required this.request,
    this.connectorRequest,
    this.windowsRequest,
    this.initialConnectorId,
    this.initialConnectionId,
    this.onOpen,
  });

  final ConnectionsRequest request;
  final ConnectionsRequest? connectorRequest;
  final ConnectionsRequest? windowsRequest;
  final String? initialConnectorId;
  // The desktop gallery's Connect names the connection after its connector.
  final String? initialConnectionId;
  final Future<void> Function(Uri url)? onOpen;

  @override
  State<ConnectWindow> createState() => _ConnectWindowState();
}

class _ConnectWindowState extends State<ConnectWindow> {
  final _connectionId = TextEditingController();
  final _value = TextEditingController();
  List<Map<String, dynamic>> connections = [];
  List<Map<String, dynamic>> connectors = [];
  String? source;
  String? authMethod;

  List<String> get authMethods => [
    for (final row in connectors.where((c) => c['id'] == source))
      ...List<String>.from(row['authMethods'] as List? ?? []),
  ];
  bool get usesOAuth => authMethod?.contains('oauth') ?? false;
  String? error;
  String? notice;
  bool busy = false;
  bool loaded = false;
  int generation = 0;

  @override
  void initState() {
    super.initState();
    _connectionId.text = widget.initialConnectionId ?? '';
    unawaited(load());
  }

  @override
  void dispose() {
    _connectionId.dispose();
    _value.dispose();
    super.dispose();
  }

  Future<void> load() async {
    final ticket = ++generation;
    setState(() => busy = true);
    try {
      final result = await widget.request('');
      final offered = await widget.connectorRequest?.call('');
      if (!mounted || ticket != generation) return;
      setState(() {
        connections = [
          for (final item in (result as List? ?? const []))
            Map<String, dynamic>.from(item as Map),
        ];
        connectors = [
          for (final item in offered as List? ?? [])
            Map<String, dynamic>.from(item as Map),
        ];
        if (!connectors.any((c) => c['id'] == source)) {
          source = connectors.any((c) => c['id'] == widget.initialConnectorId)
              ? widget.initialConnectorId
              : connectors.isEmpty
              ? null
              : '${connectors.first['id']}';
        }
        if (!authMethods.contains(authMethod)) {
          authMethod = authMethods.firstOrNull;
        }
        loaded = true;
        error = null;
      });
    } catch (failure) {
      if (mounted && ticket == generation) {
        setState(() => error = 'Connections are unavailable. $failure');
      }
    } finally {
      if (mounted && ticket == generation) setState(() => busy = false);
    }
  }

  Future<void> connect() async {
    if (source == null ||
        authMethod == null ||
        _connectionId.text.trim().isEmpty ||
        _value.text.isEmpty) {
      setState(() => error = 'Provide a connection name and a value.');
      return;
    }
    final ticket = ++generation;
    setState(() {
      busy = true;
      notice = null;
      error = null;
    });
    final secret = _value.text;
    _value.clear();
    try {
      final result = await widget.request(
        '${Uri.encodeComponent(_connectionId.text.trim())}/connect',
        body: {
          'connectorId': source,
          'authMethodId': authMethod,
          'value': secret,
        },
      );
      if (!mounted || ticket != generation) return;
      final record = Map<String, dynamic>.from(result as Map);
      setState(() {
        _connectionId.clear();
        notice =
            'Connected ${record['connectorId']} (${connectionStatus(record['status'])}).';
      });
      await load();
    } catch (failure) {
      if (mounted && ticket == generation) {
        setState(() => error = 'The connection was refused. $failure');
      }
    } finally {
      if (mounted && ticket == generation) setState(() => busy = false);
    }
  }

  Future<void> probe(String id) =>
      act('${Uri.encodeComponent(id)}/probe', id, 'Probed $id.');
  Future<void> disconnect(String id) =>
      act('${Uri.encodeComponent(id)}/disconnect', id, 'Disconnected $id.');

  Future<void> act(String action, String id, String message) async {
    final ticket = ++generation;
    setState(() {
      busy = true;
      notice = null;
      error = null;
    });
    try {
      await widget.request(action);
      if (!mounted || ticket != generation) return;
      setState(() => notice = message);
      await load();
    } catch (failure) {
      if (mounted && ticket == generation) {
        setState(() => error = '$action failed. $failure');
      }
    } finally {
      if (mounted && ticket == generation) setState(() => busy = false);
    }
  }

  Future<void> startOAuth() async {
    final open = widget.onOpen;
    if (source == null ||
        authMethod == null ||
        open == null ||
        _connectionId.text.trim().isEmpty) {
      setState(() => error = 'Provide a connection name to authorize.');
      return;
    }
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final result = await widget.request(
        '${Uri.encodeComponent(_connectionId.text.trim())}/login',
        body: {'connectorId': source, 'authMethodId': authMethod},
      );
      final url = Uri.parse((result as Map)['url'] as String);
      if (!url.hasAuthority || !['https', 'http'].contains(url.scheme)) {
        throw const FormatException();
      }
      await open(url);
      if (mounted) {
        setState(
          () => notice = 'Authorization opened. Complete sign-in, then refresh connections.',
        );
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => error = 'Provider authorization could not start. Try again.',
        );
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  void _pickSource(String value) => setState(() {
    source = value;
    authMethod = authMethods.firstOrNull;
  });

  /// Figma `.connection-row`: icon, strong title, muted status.
  Widget _row(BuildContext context, Map connection) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final status = connectionStatus(connection['status']);
    final id = '${connection['connectionId']}';
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 7),
      child: Row(
        children: [
          Icon(
            uiIcon(status == 'Connected' ? 'plug' : 'alert'),
            size: uiIconSize,
            color: status == 'Connected' ? c.done : c.error,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${connection['connectorId']} · $id',
                  style: type.rowTitle,
                  overflow: TextOverflow.ellipsis,
                ),
                Text(status, style: type.caption.copyWith(color: c.muted)),
              ],
            ),
          ),
          IconButton(
            tooltip: 'Probe connection',
            color: c.muted,
            onPressed: busy ? null : () => probe(id),
            icon: Icon(uiIcon('rotate'), size: uiIconSize),
          ),
          IconButton(
            tooltip: 'Disconnect',
            color: c.muted,
            onPressed: busy ? null : () => disconnect(id),
            icon: Icon(uiIcon('x'), size: uiIconSize),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final tokens = UiTokens.of(context);
    // One scroll view: kit fields are taller than the old dense fields, and
    // the window may be short.
    return ListView(
      padding: EdgeInsets.zero,
      children: [
        // Figma ConnectionModal header: 52 accent-tint plug tile, title and
        // muted lead.
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 20, 12, 0),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 52,
                height: 52,
                decoration: BoxDecoration(
                  color: c.accentTint,
                  borderRadius: BorderRadius.circular(tokens.panelRadius),
                ),
                child: Icon(uiIcon('plug'), size: uiIconSize, color: c.accent),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Connections', style: type.cardTitle),
                    const SizedBox(height: 4),
                    Text(
                      'IntoChat only gets the access you approve.',
                      style: type.bodyMuted,
                    ),
                  ],
                ),
              ),
              IconButton(
                tooltip: 'Refresh connections',
                color: c.muted,
                onPressed: load,
                icon: Icon(uiIcon('rotate'), size: uiIconSize),
              ),
            ],
          ),
        ),
        if (error != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              decoration: BoxDecoration(
                color: c.errorBg,
                borderRadius: BorderRadius.circular(tokens.radiusSmall),
              ),
              child: Text(error!, style: type.toast.copyWith(color: c.strong)),
            ),
          ),
        if (notice != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
            child: Text(notice!, style: type.bodyMuted),
          ),
        const SizedBox(height: 12),
        if (busy)
          LinearProgressIndicator(minHeight: 2, color: c.accent)
        else
          Divider(height: 2, thickness: 1, color: c.hairline),
        Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              UiKitSelect<String>(
                key: ValueKey('connect_source_$source'),
                label: 'Source',
                value: source,
                items: [
                  for (final connector in connectors)
                    UiKitSelectItem(
                      '${connector['id']}',
                      '${connector['displayName']}',
                    ),
                ],
                onChanged: busy ? null : _pickSource,
              ),
              const SizedBox(height: 16),
              if (source == 'windows' && widget.windowsRequest != null)
                ConstrainedBox(
                  constraints: const BoxConstraints(maxHeight: 320),
                  child: SingleChildScrollView(
                    child: WindowsStorageSetup(
                      key: ValueKey(
                        connections
                            .where((row) => row['connectorId'] == 'windows')
                            .map(
                              (row) =>
                                  '${row['connectionId']}:${row['status']}',
                            )
                            .join(','),
                      ),
                      request: widget.request,
                      windowsRequest: widget.windowsRequest!,
                      onChanged: load,
                    ),
                  ),
                ),
              if (source != 'windows' || widget.windowsRequest == null) ...[
                UiKitField(
                  fieldKey: const Key('connect_name'),
                  label: 'Connection name',
                  controller: _connectionId,
                ),
                const SizedBox(height: 16),
                if (!usesOAuth) ...[
                  UiKitField(
                    fieldKey: const Key('connect_value'),
                    label: 'Connection string or token',
                    controller: _value,
                    secret: true,
                    autocorrect: false,
                  ),
                  const SizedBox(height: 16),
                ],
                if (connectors.isEmpty && loaded) ...[
                  Text(
                    'No connectors are available for this deployment.',
                    style: type.bodyMuted,
                  ),
                  const SizedBox(height: 16),
                ],
                if (authMethods.length > 1) ...[
                  UiKitSelect<String>(
                    key: ValueKey('auth_method_$source'),
                    label: 'Authentication method',
                    value: authMethod,
                    items: [
                      for (final method in authMethods)
                        UiKitSelectItem(method, method),
                    ],
                    onChanged: busy
                        ? null
                        : (value) => setState(() => authMethod = value),
                  ),
                  const SizedBox(height: 16),
                ],
                if (!usesOAuth)
                  UiKitButton(
                    key: const Key('connect_submit'),
                    label: 'Connect',
                    icon: 'link',
                    fullWidth: true,
                    onPressed: busy || source == null ? null : connect,
                  ),
                if (usesOAuth)
                  UiKitButton(
                    key: const Key('connect_oauth'),
                    label: 'Sign in with the provider',
                    icon: 'external-link',
                    fullWidth: true,
                    onPressed: busy ? null : startOAuth,
                  ),
              ],
            ],
          ),
        ),
        Divider(height: 1, thickness: 1, color: c.hairline),
        if (!loaded)
          error != null
              ? const SizedBox.shrink()
              : Padding(
                  padding: const EdgeInsets.all(24),
                  child: Center(
                    child: CircularProgressIndicator(color: c.accent),
                  ),
                )
        else if (connections.isEmpty)
          Padding(
            padding: const EdgeInsets.all(24),
            child: Center(
              child: Text('No connections yet.', style: type.empty),
            ),
          )
        else
          Padding(
            padding: const EdgeInsets.all(11),
            child: Column(
              children: [
                for (final connection in connections) _row(context, connection),
              ],
            ),
          ),
      ],
    );
  }
}
