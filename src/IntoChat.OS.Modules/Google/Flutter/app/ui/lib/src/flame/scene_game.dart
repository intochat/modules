import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flame/components.dart';
import 'package:flame/effects.dart';
import 'package:flame/game.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/painting.dart';

import 'node_components.dart';
import 'placeholder_node.dart';
import 'prefabs/sphere.dart';
import 'primitive_nodes.dart';

typedef SceneInputHandler = void Function(
  String nodeId,
  String kind,
  double x,
  double y,
  String? payload,
);

/// Draws a scene neuron: nodes become components keyed by id, a sphere runs the behavior tree its node
/// carries, and declared gestures come back through [onInput].
final class SceneGame extends FlameGame {
  SceneGame();

  final _byId = <String, NodeComponent>{};
  SceneInputHandler? onInput;
  SphereIconLoader? loadIcon;

  /// False under reduced motion: prefabs snap to their targets and stay still.
  bool animations = true;
  Duration appearanceDuration = const Duration(milliseconds: 200);
  final _appearances =
      <
        String,
        ({
          NodeComponent node,
          Vector2 target,
          double opacity,
          List<Effect> effects,
        })
      >{};
  SceneState? _state;
  Color _background = const Color(0xFF0B0F14);

  SceneState? get state => _state;

  NodeComponent? componentFor(String nodeId) => _byId[nodeId];

  @override
  Color backgroundColor() => _background;

  /// Diffs [state] against what is drawn: adds new nodes, removes those that left, resets the
  /// rest to their definition.
  void apply(SceneState state) {
    final initial = _state == null;
    _state = state;
    wake();
    final seen = <String>{};
    void walk(SceneNode definition, Component parent) {
      seen.add(definition.id);
      final node = definition;
      var component = _byId[node.id];
      if (component == null || component.typeName != node.type) {
        component?.removeFromParent();
        component = NodeComponents.build(node) ?? PlaceholderNode(node);
        _byId[node.id] = component;
        parent.add(component);
        if (!initial &&
            animations &&
            node.tweens.isEmpty &&
            appearanceDuration > Duration.zero) {
          final target = component.position.clone();
          final opacity = component.opacity;
          final seconds =
              appearanceDuration.inMicroseconds /
              Duration.microsecondsPerSecond;
          final effects = <Effect>[
            MoveEffect.to(target, EffectController(duration: seconds)),
            OpacityEffect.to(opacity, EffectController(duration: seconds)),
          ];
          component.position.y += 12;
          component.opacity = 0;
          component.addAll(effects);
          _appearances[node.id] = (
            node: component,
            target: target,
            opacity: opacity,
            effects: effects,
          );
        }
      } else {
        var appearance = _appearances[node.id];
        if (appearance != null &&
            (node.transform.x != appearance.target.x ||
                node.transform.y != appearance.target.y ||
                node.transform.opacity != appearance.opacity ||
                node.tweens.isNotEmpty)) {
          for (final effect in appearance.effects) {
            effect.removeFromParent();
          }
          _appearances.remove(node.id);
          appearance = null;
        }
        final position = appearance == null ? null : component.position.clone();
        final opacity = component.opacity;
        component.configure(node);
        if (position != null) {
          // Content can change while its entrance continues toward the same target.
          component.position.setFrom(position);
          component.opacity = opacity;
        }
        // A parent rebuilt for a new type, or a node moved in the definition, takes it along.
        if (component.parent != parent) component.parent = parent;
      }
      for (final child in node.children) {
        walk(child, component);
      }
    }

    for (final node in state.definition.nodes) {
      walk(node, world);
    }
    for (final id in _byId.keys.where((k) => !seen.contains(k)).toList()) {
      _byId.remove(id)!.removeFromParent();
      _appearances.remove(id);
    }
    final camera = state.definition.camera;
    this.camera.viewfinder
      ..zoom = camera.zoom
      ..position = Vector2(camera.x, camera.y);
    _background = sceneColor(state.definition.background) ?? _background;
    host();
  }

  /// Hands the host's motion preference and authenticated icon loader to its spheres.
  void host() {
    _appearances.removeWhere(
      (_, appearance) => appearance.effects.every((effect) => effect.isRemoved),
    );
    if (!animations) {
      for (final appearance in _appearances.values) {
        for (final effect in appearance.effects) {
          effect.removeFromParent();
        }
        appearance.node.position.setFrom(appearance.target);
        appearance.node.opacity = appearance.opacity;
      }
      _appearances.clear();
    }
    for (final component in _byId.values) {
      switch (component) {
        case SphereComponent sphere:
          sphere.animations = animations;
          sphere.loadIcon = loadIcon;
        default:
      }
    }
    wake();
  }

  int _idleFrames = 0;

  /// Restarts the loop after it stopped on a still scene.
  void wake() {
    _idleFrames = 0;
    if (paused) resumeEngine();
  }

  // A scene with no running effect or particles stops its loop, so a still scene costs no frames
  // (and widget tests can settle); apply, input and asset loads wake it.
  @override
  void update(double dt) {
    super.update(dt);
    final animating = world.descendants().any(
      (c) =>
          c is Effect ||
          c is ParticlesNode ||
          (c is SphereComponent && c.needsFrames),
    );
    _idleFrames = animating ? 0 : _idleFrames + 1;
    if (_idleFrames > 2) pauseEngine();
  }

  void report(
    NodeComponent component,
    String kind,
    Vector2 at, [
    String? payload,
  ]) => onInput?.call(component.nodeId, kind, at.x, at.y, payload);

  @visibleForTesting
  void simulateTap(String nodeId) {
    final component = _byId[nodeId];
    if (component != null && component.inputs.contains('tap')) {
      report(component, 'tap', component.position);
    }
  }
}
