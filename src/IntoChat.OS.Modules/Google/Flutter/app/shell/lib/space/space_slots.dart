import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';

import 'space_controller.dart';

class SideSlot extends StatelessWidget {
  const SideSlot({
    super.key,
    required this.reference,
    required this.load,
    this.watch,
    this.onAction,
    this.onActivate,
    this.scenes,
    this.entries,
    this.localPreferences,
  });
  final UiChildRef reference;
  final NeuronLoader load;
  final Stream<void> Function(String, String)? watch;
  final Future<void> Function(Map<String, dynamic>)? onAction;
  final ValueChanged<Map<String, dynamic>>? onActivate;
  final SceneClient? scenes;
  final EntriesClient Function(String brainId)? entries;
  final LocalPreferences? localPreferences;
  @override
  Widget build(BuildContext context) => SizedBox(
    width: 320,
    child: UiSurface(
      child: NeuronView(
        kind: reference.kind,
        name: reference.name,
        load: load,
        watch: watch ?? (_, _) => const Stream.empty(),
        onAction: onAction,
        onActivate: onActivate,
        scenes: scenes,
        entries: entries,
        localPreferences: localPreferences,
      ),
    ),
  );
}

class MainSlot extends StatelessWidget {
  const MainSlot({
    super.key,
    this.reference,
    required this.load,
    this.error,
    this.watch,
    this.onAction,
    this.onActivate,
    this.scenes,
    this.entries,
    this.localPreferences,
  });
  final UiChildRef? reference;
  final NeuronLoader load;
  final String? error;
  final Stream<void> Function(String, String)? watch;
  final Future<void> Function(Map<String, dynamic>)? onAction;
  final ValueChanged<Map<String, dynamic>>? onActivate;
  final SceneClient? scenes;
  final EntriesClient Function(String brainId)? entries;
  final LocalPreferences? localPreferences;
  @override
  Widget build(BuildContext context) => UiSurface(
    child: error != null
        ? Center(child: Text(error!))
        : reference == null
        ? const SizedBox.shrink()
        : NeuronView(
            kind: reference!.kind,
            name: reference!.name,
            load: load,
            watch: watch ?? (_, _) => const Stream.empty(),
            onAction: onAction,
            onActivate: onActivate,
            scenes: scenes,
            entries: entries,
            localPreferences: localPreferences,
          ),
  );
}
