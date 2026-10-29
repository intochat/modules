import 'package:intochat_os_flutter/intochat_os_flutter.dart';

import 'neuron_view.dart';

import 'package:flutter/material.dart';

/// Renders the surface an application declares. The host decides how the application is
/// started; the application owns the component tree.
class ApplicationSurface extends StatefulWidget {
  const ApplicationSurface({
    super.key,
    required this.client,
    required this.workspace,
    required this.application,
    required this.start,
    this.onActivate,
  });

  final IntoChatOSUiClient client;
  final String workspace, application;
  final Future<Map<String, dynamic>> Function() start;
  final ValueChanged<Map<String, dynamic>>? onActivate;

  @override
  State<ApplicationSurface> createState() => _ApplicationSurfaceState();
}

class _ApplicationSurfaceState extends State<ApplicationSurface> {
  late Future<Map<String, dynamic>> _surface = _start();

  Future<Map<String, dynamic>> _start() => widget.start();

  @override
  void didUpdateWidget(ApplicationSurface oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.workspace != widget.workspace ||
        oldWidget.application != widget.application ||
        oldWidget.client != widget.client) {
      _surface = _start();
    }
  }

  @override
  Widget build(BuildContext context) => FutureBuilder<Map<String, dynamic>>(
    future: _surface,
    builder: (context, snapshot) {
      if (snapshot.hasError) {
        return Center(
          child: TextButton(
            onPressed: () => setState(() {
              _surface = _start();
            }),
            child: const Text('Retry application'),
          ),
        );
      }
      if (snapshot.connectionState != ConnectionState.done ||
          !snapshot.hasData) {
        return const Center(child: CircularProgressIndicator());
      }
      final surface = snapshot.data!;
      return NeuronView(
        key: ValueKey('${widget.workspace}/${widget.application}'),
        kind: surface['kind'] as String,
        name: surface['name'] as String,
        load: (kind, name) => widget.client.appRequest(
          widget.workspace,
          'node?${Uri(queryParameters: {'kind': kind, 'name': name}).query}',
        ),
        onAction: (event) async {
          await widget.client.appRequest(
            widget.workspace,
            'event',
            body: event,
          );
        },
        onActivate: widget.onActivate,
      );
    },
  );
}
