import 'dart:convert';
import 'dart:math' as math;

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flame/components.dart';
import 'package:flame/effects.dart';
import 'package:flame/events.dart';
import 'package:flutter/animation.dart';
import 'package:flutter/foundation.dart';

import 'prefabs/activity_ring.dart';
import 'prefabs/sphere.dart';
import 'primitive_nodes.dart';

import 'scene_game.dart';

/// Parses a scene style: `#rrggbb` or `#aarrggbb`.
Color? sceneColor(String? style) {
  if (style == null || !style.startsWith('#')) return null;
  final hex = style.substring(1);
  final value = int.tryParse(hex, radix: 16);
  if (value == null) return null;
  return switch (hex.length) {
    6 => Color(0xFF000000 | value),
    8 => Color(value),
    _ => null,
  };
}

/// One scene node drawn in Flame, set from its definition.
abstract class NodeComponent extends PositionComponent
    with HasPaint, HasGameReference<SceneGame>, TapCallbacks, DragCallbacks {
  NodeComponent(SceneNode node)
    : nodeId = node.id,
      typeName = node.type,
      super(anchor: Anchor.center) {
    configure(node);
  }

  final String nodeId;
  final String typeName;
  String? style;
  Map<String, String> params = const {};
  List<String> inputs = const [];
  List<SceneTween> _tweens = const [];
  BehaviorTreeDef? treeDef;
  String _tweenKey = '';

  Color get color => sceneColor(style) ?? const Color(0xFFFFFFFF);

  double param(String name, double fallback) =>
      double.tryParse(params[name] ?? '') ?? fallback;

  /// Number of times this node's tweens were (re)started.
  @visibleForTesting
  int restarts = 0;
  String _signature = '';

  /// Sets the component to [node]. An unchanged node is a no-op, so running tweens and trees keep going
  /// across refreshes.
  void configure(SceneNode node) {
    final signature = _signatureOf(node);
    if (signature == _signature) return;
    _signature = signature;
    style = node.style;
    params = Map.of(node.parameters);
    inputs = node.inputs;
    treeDef = node.tree;
    _transform(node.transform);
    _tweens = node.tweens;
    changed();
  }

  static String _signatureOf(SceneNode node) {
    final t = node.transform;
    final params = node.parameters.entries.toList()
      ..sort((a, b) => a.key.compareTo(b.key));
    return [
      node.style,
      params.map((e) => '${e.key}=${e.value}').join('&'),
      node.inputs.join(','),
      '${t.x},${t.y},${t.z},${t.scale},${t.rotation},${t.opacity}',
      _tweenKeyOf(node.tweens),
      node.tree == null ? '' : jsonEncode(node.tree!.toJson()),
    ].join('|');
  }

  static String _tweenKeyOf(List<SceneTween> tweens) => tweens
      .map(
        (t) =>
            '${t.property}|${t.from}|${t.to}|${t.milliseconds}|${t.curve}|${t.repeat}',
      )
      .join(';');

  void _transform(SceneTransform t) {
    position.setValues(t.x, t.y);
    scale.setAll(t.scale);
    angle = t.rotation;
    priority = t.z.round();
    _opacity = t.opacity;
  }

  double _opacity = 1;

  /// Recomputes size and paint after the definition or a patch changed; subclasses add to it.
  void changed() {
    paint.color = color;
    opacity = _opacity.clamp(0, 1);
    final key = _tweenKeyOf(_tweens);
    if (key != _tweenKey) {
      _tweenKey = key;
      _runTweens();
    }
  }

  void _runTweens() {
    restarts++;
    for (final effect in children.whereType<Effect>().toList()) {
      effect.removeFromParent();
    }
    for (final tween in _tweens) {
      final controller = EffectController(
        duration: math.max(tween.milliseconds, 1) / 1000,
        curve: _curve(tween.curve),
        infinite: tween.repeat,
        alternate: tween.repeat,
      );
      final effect = switch (tween.property) {
        'x' => (() {
          position.x = tween.from;
          return MoveEffect.to(Vector2(tween.to, position.y), controller);
        })(),
        'y' => (() {
          position.y = tween.from;
          return MoveEffect.to(Vector2(position.x, tween.to), controller);
        })(),
        'scale' => (() {
          scale.setAll(tween.from);
          return ScaleEffect.to(Vector2.all(tween.to), controller);
        })(),
        'opacity' => (() {
          opacity = tween.from.clamp(0, 1);
          return OpacityEffect.to(tween.to.clamp(0, 1), controller);
        })(),
        'rotation' => (() {
          angle = tween.from;
          return RotateEffect.to(tween.to, controller);
        })(),
        _ => null,
      };
      if (effect != null) add(effect);
    }
  }

  static Curve _curve(String name) => switch (name) {
    'linear' => Curves.linear,
    'easeIn' => Curves.easeIn,
    'easeOut' => Curves.easeOut,
    'bounce' || 'bounceOut' => Curves.bounceOut,
    'elastic' || 'elasticOut' => Curves.elasticOut,
    _ => Curves.easeInOut,
  };

  @override
  bool containsLocalPoint(Vector2 point) =>
      inputs.isNotEmpty && super.containsLocalPoint(point);

  @override
  void onTapUp(TapUpEvent event) {
    game.wake();
    if (inputs.contains('tap')) {
      game.report(this, 'tap', event.localPosition);
    } else {
      event.continuePropagation = true;
    }
  }

  /// Whether this node takes drags (and stops them propagating).
  bool get draggable => inputs.contains('drag');

  /// Whether a drag moves the node and is reported; a prefab may take drags as presentation only.
  bool get reportsDrag => inputs.contains('drag');

  @override
  void onDragStart(DragStartEvent event) {
    super.onDragStart(event);
    game.wake();
    if (!draggable) event.continuePropagation = true;
  }

  @override
  void onDragEnd(DragEndEvent event) {
    super.onDragEnd(event);
    game.wake();
    if (reportsDrag) {
      game.report(
        this,
        'drag',
        position,
        '${event.velocity.x},${event.velocity.y}',
      );
    }
  }

  @override
  void onDragUpdate(DragUpdateEvent event) {
    game.wake();
    if (reportsDrag) position.add(event.localDelta);
  }
}

abstract final class NodeComponents {
  /// The component for a known primitive or prefab type; null for anything else.
  static NodeComponent? build(SceneNode node) => switch (node.type) {
    'circle' => CircleNode(node),
    'rect' => RectNode(node),
    'text' => TextNode(node),
    'sprite' => SpriteNode(node),
    'path' => PathNode(node),
    'particles' => ParticlesNode(node),
    'group' => GroupNode(node),
    'activity_ring' => ActivityRing(node),
    'sphere' => SphereComponent(node),
    _ => null,
  };
}
