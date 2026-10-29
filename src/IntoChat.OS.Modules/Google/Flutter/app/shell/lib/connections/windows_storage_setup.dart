import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'connect_window.dart';

class WindowsStorageSetup extends StatefulWidget {
  const WindowsStorageSetup({
    super.key,
    required this.request,
    required this.windowsRequest,
    this.onChanged,
  });

  final ConnectionsRequest request;
  final ConnectionsRequest windowsRequest;
  final Future<void> Function()? onChanged;

  @override
  State<WindowsStorageSetup> createState() => _WindowsStorageSetupState();
}

class _WindowsStorageSetupState extends State<WindowsStorageSetup> {
  final _path = TextEditingController();
  String? _machineId, _machineName, _error;
  List<String> _roots = [];
  bool _connected = false, _busy = true;
  int _generation = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  @override
  void didUpdateWidget(WindowsStorageSetup oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.request != oldWidget.request ||
        widget.windowsRequest != oldWidget.windowsRequest) {
      _machineId = null;
      _machineName = null;
      _roots = [];
      _connected = false;
      _path.clear();
      unawaited(_load());
    }
  }

  @override
  void dispose() {
    _path.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final generation = ++_generation;
    final request = widget.request;
    final windows = widget.windowsRequest;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final device = Map<String, dynamic>.from(await windows('device') as Map);
      final id = device['machineId'] as String;
      final name = device['machineName'] as String;
      final connections = await request('') as List;
      final connected = connections.any(
        (row) =>
            row['connectorId'] == 'windows' &&
            row['connectionId'] == id &&
            row['status'] == 'Connected',
      );
      final roots = connected
          ? List<String>.from(
              await windows('${Uri.encodeComponent(id)}/roots') as List,
            )
          : <String>[];
      if (!mounted || generation != _generation) return;
      setState(() {
        _machineId = id;
        _machineName = name;
        _connected = connected;
        _roots = roots;
      });
    } catch (_) {
      if (mounted && generation == _generation) {
        setState(
          () =>
              _error = 'Windows storage is unavailable. Refresh to try again.',
        );
      }
    } finally {
      if (mounted && generation == _generation) setState(() => _busy = false);
    }
  }

  Future<void> _change(String action, [String? path]) async {
    final generation = _generation;
    final id = _machineId!;
    final request = widget.request;
    final windows = widget.windowsRequest;
    final changed = widget.onChanged;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      if (action == 'connect') {
        final result = await request(
          '${Uri.encodeComponent(id)}/connect',
          body: {
            'connectorId': 'windows',
            'authMethodId': 'device',
            'value': id,
          },
        ) as Map;
        if (result['status'] != 'Connected') {
          throw StateError('Connection failed');
        }
      } else {
        await windows(
          '${Uri.encodeComponent(id)}/roots/$action',
          body: {'path': path},
        );
      }
      if (!mounted || generation != _generation) return;
      _path.clear();
      await _load();
      if (mounted && _generation == generation + 1) await changed?.call();
    } catch (_) {
      if (mounted && generation == _generation) {
        setState(() {
          _busy = false;
          _error = action == 'connect'
              ? 'The computer could not connect. Please try again.'
              : 'The folder change failed. Use an existing full folder path on $_machineName.';
        });
      }
    }
  }

  void _allow() {
    final path = _path.text.trim();
    if (path.isEmpty) {
      setState(() => _error = 'Enter a full folder path on $_machineName.');
      return;
    }
    unawaited(_change('allow', path));
  }

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final tokens = UiTokens.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(child: Text('Windows storage', style: type.cardTitle)),
            IconButton(
              tooltip: 'Refresh Windows storage',
              color: c.muted,
              onPressed: _busy ? null : _load,
              icon: Icon(uiIcon('rotate'), size: uiIconSize),
            ),
          ],
        ),
        if (_busy) LinearProgressIndicator(minHeight: 2, color: c.accent),
        if (_error != null) ...[
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              color: c.errorBg,
              borderRadius: BorderRadius.circular(tokens.radiusSmall),
            ),
            child: Text(_error!, style: type.toast.copyWith(color: c.strong)),
          ),
        ],
        if (_machineName != null) ...[
          const SizedBox(height: 8),
          Text('Computer: $_machineName', style: type.rowTitle),
          const SizedBox(height: 4),
          Text(
            'Folder paths refer to this workspace host, which may be a different computer from yours.',
            style: type.bodyMuted,
          ),
          const SizedBox(height: 16),
          if (!_connected)
            UiKitButton(
              label: 'Connect $_machineName',
              icon: 'local-machine',
              fullWidth: true,
              onPressed: _busy ? null : () => _change('connect'),
            )
          else ...[
            Text('Allowed folders on $_machineName', style: type.rowTitle),
            const SizedBox(height: 4),
            Text(
              'Only folders you allow are available to Files and console commands.',
              style: type.bodyMuted,
            ),
            const SizedBox(height: 8),
            if (_roots.isEmpty)
              Text('No folders allowed yet.', style: type.bodyMuted),
            for (final root in _roots)
              Row(
                children: [
                  Icon(uiIcon('file'), size: uiIconSize, color: c.muted),
                  const SizedBox(width: 10),
                  Expanded(child: Text(root, style: type.body)),
                  IconButton(
                    tooltip: 'Revoke $root',
                    color: c.muted,
                    onPressed: _busy ? null : () => _change('revoke', root),
                    icon: Icon(uiIcon('x'), size: uiIconSize),
                  ),
                ],
              ),
            const SizedBox(height: 16),
            UiKitField(
              fieldKey: const Key('windows_folder_path'),
              label: 'Full folder path on $_machineName',
              placeholder: r'C:\Folder',
              controller: _path,
              enabled: !_busy,
              onSubmitted: (_) => _busy ? null : _allow(),
            ),
            const SizedBox(height: 16),
            UiKitButton(
              label: 'Allow folder',
              icon: 'plus',
              fullWidth: true,
              onPressed: _busy ? null : _allow,
            ),
          ],
        ],
      ],
    );
  }
}
