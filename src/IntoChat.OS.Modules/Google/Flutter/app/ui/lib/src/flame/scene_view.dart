import 'dart:async';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flame/game.dart';
import 'package:flutter/material.dart';

import 'scene_game.dart';
import 'prefabs/sphere.dart';
import '../theme/ui_tokens.dart';

/// Hosts one scene neuron in a [GameWidget].
///
/// With a [client] it reads the scene itself and re-reads on every [refresh] event (the
/// shell's watch stream for the scene key carries `SceneChanged`/`SceneEntered`); gestures go
/// back through [SceneClient.report]. Without one it draws [state] as given.
class SceneView extends StatefulWidget {
  const SceneView({
    super.key,
    required this.sceneKey,
    this.client,
    this.state,
    this.refresh,
    this.animateChanges = true,
    this.onInput,
    this.loadIcon,
  });

  final String sceneKey;
  final SceneClient? client;
  final SceneState? state;
  final Stream<void>? refresh;

  final bool animateChanges;
  final SceneInputHandler? onInput;
  final SphereIconLoader? loadIcon;

  @override
  State<SceneView> createState() => _SceneViewState();
}

class _SceneViewState extends State<SceneView> {
  late final SceneGame _game = SceneGame()
    ..onInput = _report
    ..loadIcon = widget.loadIcon;
  StreamSubscription<void>? _refresh;
  Object? _error;
  int? _revision;

  @override
  void initState() {
    super.initState();
    final state = widget.state;
    if (state != null) _show(state);
    _listen();
    if (state == null) unawaited(_read());
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final animations = !MediaQuery.disableAnimationsOf(context);
    _game.appearanceDuration = UiTokens.of(context).standard;
    if (_game.animations != animations) {
      _game
        ..animations = animations
        ..host();
    }
  }

  @override
  void didUpdateWidget(SceneView old) {
    super.didUpdateWidget(old);
    if (old.loadIcon != widget.loadIcon) {
      _game
        ..loadIcon = widget.loadIcon
        ..host();
    }
    final state = widget.state;
    if (state != null && !identical(state, old.state)) _show(state);
    if (old.refresh != widget.refresh) _listen();
    if (old.sceneKey != widget.sceneKey || old.client != widget.client) {
      _revision = null;
      if (state == null) unawaited(_read());
    }
  }

  void _listen() {
    unawaited(_refresh?.cancel());
    _refresh = widget.refresh?.listen((_) => unawaited(_read()));
  }

  void _show(SceneState state) {
    _revision = state.revision;
    final animations = _game.animations;
    if (!widget.animateChanges) _game.animations = false;
    _game.apply(state);
    _game.animations = animations;
    _game.host();
  }

  Future<void> _read() async {
    final client = widget.client;
    if (client == null) return;
    try {
      final state = await client.read(widget.sceneKey);
      if (!mounted) return;
      final hadError = _error != null;
      _error = null;
      if (state.revision != _revision) _show(state);
      if (hadError) setState(() {});
    } catch (error) {
      if (mounted) setState(() => _error = error);
    }
  }

  void _report(
    String nodeId,
    String kind,
    double x,
    double y,
    String? payload,
  ) {
    if (widget.onInput != null) {
      widget.onInput!(nodeId, kind, x, y, payload);
      return;
    }
    final client = widget.client;
    if (client == null) return;
    unawaited(
      client
          .report(widget.sceneKey, nodeId, kind, x, y, payload: payload)
          .catchError((Object _) {}),
    );
  }

  @override
  void dispose() {
    unawaited(_refresh?.cancel());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Stack(
    fit: StackFit.expand,
    children: [
      // Scenes take no keys; an autofocused GameWidget would swallow the host's shortcuts.
      GameWidget<SceneGame>(game: _game, autofocus: false),
      if (_error != null)
        Align(
          alignment: Alignment.bottomCenter,
          child: Padding(
            padding: const EdgeInsets.all(8),
            child: Text(
              'This scene could not be read.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
        ),
    ],
  );
}
