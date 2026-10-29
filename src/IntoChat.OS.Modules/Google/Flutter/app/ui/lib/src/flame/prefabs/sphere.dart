import 'dart:convert';
import 'dart:async';
import 'dart:typed_data';
import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flame/components.dart';
import 'package:flame/effects.dart';
import 'package:flame_behavior_tree/flame_behavior_tree.dart';
import 'package:flutter/painting.dart';
import 'package:flutter_svg/flutter_svg.dart' as svg;

import '../../shell_icon.dart';
import '../behavior/default_idle_tree.dart';
import '../behavior/tree_runtime.dart';
import '../node_components.dart';

typedef SphereIconLoader = Future<Uint8List?> Function(
  String neuron,
  String icon,
);

/// What a sphere's behavior tree moves: draws the icon disc and its labels, and runs the tree's
/// moveTo, scale, fade and pulse tasks as Flame effects on itself.
final class SphereVisual extends PositionComponent
    with HasPaint
    implements SphereBody {
  SphereVisual() : super(anchor: Anchor.center);

  @override
  double now = 0;
  @override
  int frame = 0;

  /// Off under reduced motion: tasks still run their clock but draw nothing.
  bool animations = true;
  String icon = '';
  String title = '';
  String description = '';
  bool badge = false;
  double presentationOpacity = 1;
  ui.Image? iconImage;

  final _effects = <String, Effect>{};
  TextPainter? _titleText, _descriptionText;

  bool get hasEffects => _effects.values.any((effect) => !effect.isRemoved);

  void advance(double dt) {
    now += dt;
    frame++;
  }

  void setLabels(String title, String description) {
    if (title != this.title || _titleText == null) {
      this.title = title;
      _titleText = _label(title, 13, const Color(0xFFE6EDF3));
    }
    if (description != this.description || _descriptionText == null) {
      this.description = description;
      _descriptionText = _label(description, 11, const Color(0xFF9AA4B2));
    }
  }

  static TextPainter _label(String text, double size, Color color) =>
      TextPainter(
        text: TextSpan(
          text: text,
          style: TextStyle(color: color, fontSize: size),
        ),
        textDirection: TextDirection.ltr,
        maxLines: 1,
        ellipsis: '...',
      )..layout(maxWidth: 160);

  @override
  void begin(String kind, TreeNode params) {
    if (!animations) return;
    final controller = EffectController(
      duration: math.max(params.seconds, 0.001),
    );
    final (property, effect) = switch (kind) {
      'moveTo' => (
        'position',
        MoveEffect.to(
          Vector2(size.x / 2 + params.x, size.y / 2 + params.y),
          controller,
        ),
      ),
      'scale' => (
        'scale',
        ScaleEffect.to(Vector2.all(params.factor), controller),
      ),
      'fade' => ('opacity', OpacityEffect.to(params.alpha, controller)),
      'pulse' => (
        'scale',
        ScaleEffect.to(
          Vector2.all(1 + params.amplitude),
          EffectController(
            duration: math.max(params.seconds, 0.002) / 2,
            alternate: true,
          ),
        ),
      ),
      _ => (null, null),
    };
    if (property == null || effect == null) return;
    _effects.remove(property)?.removeFromParent();
    _effects[property] = effect;
    add(effect);
  }

  @override
  void render(ui.Canvas canvas) {
    // HasPaint stores effect opacity in paint.color; render must leave it intact.
    canvas.saveLayer(
      null,
      ui.Paint()
        ..color = Color.fromRGBO(255, 255, 255, opacity * presentationOpacity),
    );
    final center = (size / 2).toOffset();
    final radius = size.x / 2;
    canvas.drawCircle(
      center,
      radius,
      ui.Paint()..color = const Color(0xFF1B2A22),
    );
    canvas.drawCircle(
      center,
      radius,
      ui.Paint()
        ..style = ui.PaintingStyle.stroke
        ..strokeWidth = 2
        ..color = const Color(0xFF7FB069),
    );
    if (iconImage case final image?) {
      final extent = radius * 1.3;
      canvas.drawImageRect(
        image,
        Rect.fromLTWH(0, 0, image.width.toDouble(), image.height.toDouble()),
        Rect.fromCenter(center: center, width: extent, height: extent),
        ui.Paint(),
      );
    } else if (icon.isNotEmpty) {
      final data = shellIcon(icon);
      final glyph = TextPainter(
        text: TextSpan(
          text: data == null
              ? icon[0].toUpperCase()
              : String.fromCharCode(data.codePoint),
          style: TextStyle(
            fontSize: radius * 0.9,
            color: const Color(0xFFE9F4DF),
            fontFamily: data?.fontFamily,
            package: data?.fontPackage,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      glyph.paint(canvas, center - Offset(glyph.width / 2, glyph.height / 2));
    }
    var top = -6.0;
    for (final text in [_descriptionText, _titleText]) {
      if (text == null || (text.text?.toPlainText().isEmpty ?? true)) continue;
      top -= text.height;
      text.paint(canvas, Offset(center.dx - text.width / 2, top));
    }
    if (badge) {
      canvas.drawCircle(
        center.translate(radius * 0.72, -radius * 0.72),
        7,
        ui.Paint()..color = const Color(0xFFE5484D),
      );
    }
    canvas.restore();
  }
}

/// A neuron's sphere: the icon disc with its labels, driven by the behavior tree its node carries
/// (or [defaultIdleTree]). The game rewrites [blackboard] from the node's `blackboard` on every apply.
final class SphereComponent extends NodeComponent
    with HasBehaviorTree<BaseNode> {
  SphereComponent(super.node);

  final _board = SphereBlackboard();

  @override
  SphereBlackboard get blackboard => _board;
  final body = SphereVisual();
  String _treeKey = 'unset';
  SphereIconLoader? _loadIcon;
  int _iconGeneration = 0;
  String _iconKey = '';
  bool _removed = false;
  Future<void> _iconRead = Future.value();
  Future<void> get iconReady => _iconRead;
  SphereIconLoader? get loadIcon => _loadIcon;
  set loadIcon(SphereIconLoader? value) {
    if (value == _loadIcon) return;
    _loadIcon = value;
    _iconKey = '';
    _readIcon();
  }

  void _readIcon() {
    final icon = params['icon'] ?? '', neuron = params['neuron'] ?? nodeId;
    final key = '$neuron:$icon';
    if (key == _iconKey) return;
    _iconKey = key;
    final generation = ++_iconGeneration;
    body.iconImage?.dispose();
    body.iconImage = null;
    final load = _loadIcon;
    if (load == null || icon.isEmpty || shellIcon(icon) != null || _removed) {
      return;
    }
    _iconRead = () async {
      ui.Image? image;
      try {
        final bytes = await load(neuron, icon);
        if (bytes == null ||
            bytes.isEmpty ||
            generation != _iconGeneration ||
            _removed) {
          return;
        }
        if (utf8
            .decode(bytes.take(256).toList(), allowMalformed: true)
            .trimLeft()
            .startsWith('<')) {
          final picture = await svg.vg.loadPicture(
            svg.SvgBytesLoader(bytes),
            null,
          );
          try {
            final recorder = ui.PictureRecorder();
            ui.Canvas(recorder)
              ..scale(96 / picture.size.width, 96 / picture.size.height)
              ..drawPicture(picture.picture);
            // End recording before awaiting conversion so both pictures have explicit owners.
            final scaled = recorder.endRecording();
            try {
              image = await scaled.toImage(96, 96);
            } finally {
              scaled.dispose();
            }
          } finally {
            picture.picture.dispose();
          }
        } else {
          final codec = await ui.instantiateImageCodec(bytes);
          try {
            image = (await codec.getNextFrame()).image;
          } finally {
            codec.dispose();
          }
        }
        if (generation != _iconGeneration || _removed) {
          image.dispose();
          return;
        }
        body.iconImage = image;
        if (isMounted) game.wake();
      } catch (_) {
        image?.dispose();
      }
    }();
    unawaited(_iconRead);
  }

  @override
  void onRemove() {
    _removed = true;
    _iconGeneration++;
    body.iconImage?.dispose();
    body.iconImage = null;
    super.onRemove();
  }

  bool get usesDefaultTree => treeDef == null;
  bool get attentionBadge => blackboard['attention'] == 'true';

  bool get animations => body.animations;
  set animations(bool value) => body.animations = value;

  /// Whether the scene must keep drawing frames for this sphere: a tree of its own, a flag that makes the
  /// default tree move, or an effect still running.
  bool get needsFrames =>
      animations &&
      (treeDef != null ||
          blackboard['running'] == 'true' ||
          attentionBadge ||
          body.hasEffects);

  @override
  Future<void> onLoad() async {
    await super.onLoad();
    await add(body);
  }

  @override
  void changed() {
    size.setAll(96);
    super.changed();
    _readBoard();
    body
      ..size.setFrom(size)
      ..position.setValues(size.x / 2, size.y / 2)
      ..icon = params['icon'] ?? ''
      ..presentationOpacity = params['state'] == 'unready' ? .45 : 1
      ..badge = attentionBadge || params['state'] == 'error'
      ..setLabels(params['title'] ?? '', params['description'] ?? '');
    _readIcon();
    final key = treeDef == null ? '' : jsonEncode(treeDef!.toJson());
    if (key != _treeKey) {
      _treeKey = key;
      treeRoot = buildTree(treeDef ?? defaultIdleTree(), blackboard, body);
    }
    if (isMounted) game.wake();
  }

  void _readBoard() {
    blackboard.clear();
    final raw = params['blackboard'];
    if (raw == null) return;
    try {
      final decoded = jsonDecode(raw);
      if (decoded is Map) {
        for (final entry in decoded.entries) {
          blackboard['${entry.key}'] = '${entry.value}';
        }
      }
    } on FormatException {
      // The server validates the blackboard as JSON; an old shell reading a newer shape shows no facts.
    }
  }

  @override
  void update(double dt) {
    body.advance(dt);
    super.update(dt);
    if (needsFrames) game.wake();
  }
}
