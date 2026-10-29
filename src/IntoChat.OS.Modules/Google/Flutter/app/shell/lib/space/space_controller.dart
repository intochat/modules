import 'dart:async';
import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter/foundation.dart';
import 'package:uuid/uuid.dart';

@immutable
class UiChildRef {
  const UiChildRef(this.kind, this.name);
  final String kind, name;
  factory UiChildRef.fromJson(Map<String, dynamic> json) =>
      UiChildRef(json['kind'] as String, json['name'] as String);
}

@immutable
class SpaceView {
  const SpaceView({
    required this.scene,
    required this.focused,
    this.side,
    this.main,
    this.toolbar = const [],
    this.title = '',
    this.description = '',
    this.state = 'unready',
    this.error,
    this.animate = false,
    this.cut = false,
    this.icon,
  });
  final SceneState scene;
  final String focused, title, description, state;
  final UiChildRef? side, main;
  final List<AppToolbarEntry> toolbar;
  final String? error, icon;
  final bool animate, cut;
  factory SpaceView.fromScene(
    SceneState scene, {
    required String brain,
    bool animate = false,
    bool cut = false,
  }) {
    SceneNode? focused;
    for (final node in scene.definition.nodes) {
      if (node.type == 'sphere' && node.parameters['focused'] == 'true') {
        focused = node;
        break;
      }
    }
    final p = focused?.parameters ?? const <String, String>{};
    UiChildRef? ref(String key) {
      final value = p[key];
      return value == null
          ? null
          : UiChildRef.fromJson(
              Map<String, dynamic>.from(jsonDecode(value) as Map),
            );
    }

    return SpaceView(
      scene: scene,
      focused: p['neuron'] ?? brain,
      side: ref('side'),
      main: ref('main'),
      toolbar: List.unmodifiable([
        for (final entry in jsonDecode(p['toolbar'] ?? '[]') as List)
          AppToolbarEntry.fromJson(Map<String, dynamic>.from(entry as Map)),
      ]),
      title: p['title'] ?? (focused == null ? 'Home' : ''),
      description: p['description'] ?? '',
      state: p['state'] ?? 'unready',
      error: p['error'],
      animate: animate,
      cut: cut,
      icon: p['icon'],
    );
  }
}

class SpaceController {
  SpaceController({
    required this.client,
    required this.scenes,
    required this.brain,
    this.watch,
  }) : _view = ValueNotifier(
         SpaceView(
           scene: SceneState(
             name: '$brain/space',
             revision: -1,
             definition: const SceneDefinition(),
           ),
           focused: brain,
         ),
       );
  final IntoChatOSUiClient client;
  final SceneClient scenes;
  final String brain;
  final Stream<void> Function()? watch;
  final ValueNotifier<SpaceView> _view;
  ValueListenable<SpaceView> get view => _view;
  WorkspaceSnapshot? workspace;
  final error = ValueNotifier<Object?>(null);
  StreamSubscription<void>? _subscription;
  Timer? _reconnect;
  bool _disposed = false, _reading = false, _again = false, _recovering = false;
  Future<void> _commands = Future.value();
  Future<void> Function()? _retry;

  Future<void> retry() => _retry?.call() ?? refresh();

  Future<void> start() async {
    _listen();
    await refresh();
  }

  void _listen() {
    if (_disposed) return;
    _subscription =
        (watch?.call() ??
                client.watchNode(brain, 'scene', '$brain/space').map((_) {}))
            .listen(
              (_) => unawaited(refresh(animate: !_recovering)),
              onError: (Object problem) {
                if (!_disposed && _retry == null) error.value = problem;
                _reopen();
              },
              onDone: _reopen,
            );
  }

  void _reopen() {
    if (_disposed || _reconnect != null) return;
    _recovering = true;
    unawaited(_subscription?.cancel());
    _reconnect = Timer(const Duration(seconds: 2), () {
      _reconnect = null;
      if (_disposed) return;
      _listen();
      unawaited(refresh());
    });
  }

  Future<void> refresh({bool animate = false}) async {
    if (_disposed) return;
    if (_reading) {
      _again = true;
      return;
    }
    _reading = true;
    try {
      do {
        _again = false;
        final snapshot = await client.readWorkspace(brain);
        final scene = await scenes.read('$brain/space');
        if (_disposed) return;
        workspace = snapshot;
        _apply(scene, animate: animate && !_recovering, cut: _recovering);
        // A successful read cannot acknowledge a failed navigation intent.
        if (_retry == null) error.value = null;
        _recovering = false;
      } while (_again && !_disposed);
    } catch (problem) {
      if (!_disposed && _retry == null) error.value = problem;
    } finally {
      _reading = false;
    }
  }

  void _apply(SceneState scene, {required bool animate, bool cut = false}) {
    if (scene.revision <= _view.value.scene.revision) return;
    final next = SpaceView.fromScene(scene, brain: brain);
    _view.value = SpaceView.fromScene(
      scene,
      brain: brain,
      cut: cut,
      animate:
          animate &&
          next.focused != _view.value.focused &&
          _view.value.scene.revision >= 0,
    );
  }

  Future<void> _navigate(
    Future<WorkspaceSnapshot> Function(String, int) command, {
    String? operationId,
  }) {
    final id = operationId ?? const Uuid().v4();
    final operation = _commands.then((_) async {
      if (_disposed) return;
      workspace ??= await client.readWorkspace(brain);
      try {
        workspace = await command(id, workspace!.revision);
      } on TableRequestException catch (problem) {
        if (problem.statusCode != 409) rethrow;
        workspace = await client.readWorkspace(brain);
        workspace = await command(id, workspace!.revision);
      }
      final scene = await scenes.read('$brain/space');
      if (!_disposed) {
        _apply(scene, animate: true);
        _retry = null;
        error.value = null;
      }
    });
    _commands = operation.catchError((Object problem) {
      if (!_disposed) {
        _retry = () => _navigate(command, operationId: id);
        error.value = problem;
      }
    });
    return operation;
  }

  Future<void> show(String key) =>
      _navigate((id, revision) => client.showNeuron(brain, key, id, revision));
  Future<void> back() =>
      _navigate((id, revision) => client.backNeuron(brain, id, revision));
  Future<void> home() =>
      _navigate((id, revision) => client.homeNeuron(brain, id, revision));
  Future<void> close(String key) =>
      _navigate((id, revision) => client.closeNeuron(brain, key, id, revision));
  Future<void> input(
    String nodeId,
    String kind,
    double x,
    double y,
    String? payload,
  ) async {
    await scenes.report('$brain/space', nodeId, kind, x, y, payload: payload);
    if (kind != 'tap') return;
    for (final node in _view.value.scene.definition.nodes) {
      if (node.id == nodeId && node.type == 'sphere') {
        final key = node.parameters['neuron'] ?? node.id;
        if (key == brain) {
          await home();
        } else {
          await show(key);
        }
        return;
      }
    }
  }

  void dispose() {
    _disposed = true;
    _reconnect?.cancel();
    unawaited(_subscription?.cancel());
    _view.dispose();
    error.dispose();
  }
}
