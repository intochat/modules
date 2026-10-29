import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'connect_window.dart';
import 'windows_storage_setup.dart';
export 'connect_window.dart' show connectionStatus;

class ConnectionsSettings extends StatefulWidget {
  const ConnectionsSettings({
    super.key,
    this.request,
    this.connectorRequest,
    this.windowsRequest,
    this.onOpen,
  });
  final ConnectionsRequest? request;
  final ConnectionsRequest? connectorRequest;
  final ConnectionsRequest? windowsRequest;
  final Future<void> Function(Uri)? onOpen;
  @override
  State<ConnectionsSettings> createState() => _ConnectionsSettingsState();
}

class _ConnectionsSettingsState extends State<ConnectionsSettings> {
  List<Map<String, dynamic>> _connections = [];
  List<Map<String, dynamic>> _connectors = [];
  bool _busy = false;
  bool _add = false;
  String? _error;
  String? _notice;
  int _generation = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  @override
  void didUpdateWidget(ConnectionsSettings oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.request != widget.request ||
        oldWidget.connectorRequest != widget.connectorRequest) {
      _connections = [];
      _connectors = [];
      _notice = null;
      unawaited(_load());
    }
  }

  List<Map<String, dynamic>> _rows(Object? value) => [
    for (final row in value as List? ?? [])
      Map<String, dynamic>.from(row as Map),
  ];
  Future<void> _load() async {
    final generation = ++_generation;
    final request = widget.request;
    final connectorRequest = widget.connectorRequest;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final connections = request == null
          ? <Map<String, dynamic>>[]
          : _rows(await request(''));
      final services = connectorRequest == null
          ? <Map<String, dynamic>>[]
          : _rows(await connectorRequest(''));
      if (mounted && generation == _generation) {
        setState(() {
          _connections = connections;
          _connectors = services;
        });
      }
    } catch (_) {
      if (mounted && generation == _generation) {
        setState(
          () => _error = 'Connections are unavailable. Refresh to try again.',
        );
      }
    } finally {
      if (mounted && generation == _generation) setState(() => _busy = false);
    }
  }

  Future<void> _act(String action, String id) async {
    final generation = _generation;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.request!('${Uri.encodeComponent(id)}/$action');
      if (mounted && generation == _generation) await _load();
    } catch (_) {
      if (mounted && generation == _generation) {
        setState(() {
          _busy = false;
          _error = 'The connection action failed. Please try again.';
        });
      }
    }
  }

  Future<void> _start(
    String connector,
    String authMethod,
    String connectionId,
  ) async {
    final generation = _generation;
    final open = widget.onOpen!;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final result = await widget.request!(
        '${Uri.encodeComponent(connectionId)}/login',
        body: {'connectorId': connector, 'authMethodId': authMethod},
      );
      final url = Uri.parse((result as Map)['url'] as String);
      if (!url.hasAuthority ||
          (url.scheme != 'https' && url.scheme != 'http')) {
        throw const FormatException();
      }
      if (!mounted || generation != _generation) return;
      await open(url);
      if (mounted && generation == _generation) {
        setState(
          () => _notice = 'Authorization opened in your browser. Complete the provider steps, then refresh to see the authorized service.',
        );
      }
    } catch (_) {
      if (mounted && generation == _generation) {
        setState(
          () => _error = 'Provider authorization could not start. Check the service configuration and try again.',
        );
      }
    } finally {
      if (mounted && generation == _generation) setState(() => _busy = false);
    }
  }

  Future<void> _advanced([String? connectorId]) async {
    await showDialog<void>(
      context: context,
      builder: (context) => Dialog(
        child: SizedBox(
          width: 560,
          height: 680,
          child: ConnectWindow(
            request: widget.request!,
            connectorRequest: widget.connectorRequest,
            windowsRequest: widget.windowsRequest,
            initialConnectorId: connectorId,
            onOpen: widget.onOpen,
          ),
        ),
      ),
    );
    if (mounted) await _load();
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Row(
        children: [
          Expanded(
            child: Text('Connections', style: UiType.of(context).heading),
          ),
          IconButton(
            tooltip: 'Refresh connections',
            onPressed: _busy ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      const Text('Connections belong to the selected brain.'),
      const SizedBox(height: 16),
      if (_busy) const LinearProgressIndicator(),
      if (_error != null)
        Text(
          _error!,
          style: TextStyle(color: Theme.of(context).colorScheme.error),
        ),
      if (_notice != null) Text(_notice!),
      if (widget.request == null)
        const Text('Connect to a workspace to manage services.')
      else if (_connections.isEmpty && !_busy && _error == null)
        const Padding(
          padding: EdgeInsets.symmetric(vertical: 16),
          child: Text('No connections configured yet.'),
        ),
      for (final row in _connections)
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${_connectors.where((c) => c['id'] == row['connectorId']).firstOrNull?['displayName'] ?? row['connectorId']} · ${row['connectionId']}',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                Text(connectionStatus(row['status'])),
                if (row['account'] is String) Text(row['account'] as String),
                Wrap(
                  spacing: 8,
                  children: [
                    if (_connectors.any(
                      (service) =>
                          service['id'] == row['connectorId'] &&
                          List<String>.from(
                            service['authMethods'] as List? ?? [],
                          ).any((m) => m.contains('oauth')),
                    ))
                      TextButton.icon(
                        onPressed: _busy || widget.onOpen == null
                            ? null
                            : () => _start(
                                '${row['connectorId']}',
                                '${row['authMethodId']}',
                                '${row['connectionId']}',
                              ),
                        icon: const Icon(Icons.login),
                        label: const Text('Reconnect'),
                      ),
                    TextButton.icon(
                      onPressed: _busy
                          ? null
                          : () => _act('probe', '${row['connectionId']}'),
                      icon: const Icon(Icons.network_check),
                      label: const Text('Check credential'),
                    ),
                    TextButton.icon(
                      onPressed: _busy
                          ? null
                          : () => _act('disconnect', '${row['connectionId']}'),
                      icon: const Icon(Icons.link_off),
                      label: const Text('Disconnect'),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      const SizedBox(height: 16),
      if (widget.request != null &&
          widget.windowsRequest != null &&
          _connectors.any((connector) => connector['id'] == 'windows')) ...[
        WindowsStorageSetup(
          key: ValueKey(
            _connections
                .where((row) => row['connectorId'] == 'windows')
                .map((row) => '${row['connectionId']}:${row['status']}')
                .join(','),
          ),
          request: widget.request!,
          windowsRequest: widget.windowsRequest!,
          onChanged: _load,
        ),
        const SizedBox(height: 16),
      ],
      OutlinedButton.icon(
        onPressed: widget.request == null
            ? null
            : () => setState(() => _add = !_add),
        icon: Icon(_add ? Icons.expand_less : Icons.add),
        label: const Text('Add connection'),
      ),
      if (_add) ...[
        for (final service in _connectors)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text('${service['displayName']}'),
            subtitle: Text(
              List<String>.from(service['authMethods'] as List? ?? [])
                  .join(', '),
            ),
            trailing: TextButton(
              onPressed: _busy ? null : () => _advanced('${service['id']}'),
              child: const Text('Connect'),
            ),
          ),
        TextButton.icon(
          onPressed: _busy ? null : _advanced,
          icon: const Icon(Icons.key),
          label: const Text('Choose connector and authentication'),
        ),
      ],
    ],
  );
}
