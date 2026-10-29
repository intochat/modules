import 'dart:convert';
import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flame/components.dart';
import 'package:flame/events.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/painting.dart';

import '../node_components.dart';

final class RingEntry {
  const RingEntry(this.id, this.label, this.kind, this.progress);
  final String id, label, kind;
  final double progress;

  /// `items` as JSON; malformed input and entries without an id are skipped, at most 24 kept.
  static List<RingEntry> parse(String? json) {
    if (json == null || json.isEmpty) return const [];
    Object? decoded;
    try {
      decoded = jsonDecode(json);
    } on FormatException {
      return const [];
    }
    if (decoded is! List) return const [];
    final entries = <RingEntry>[];
    final seen = <String>{};
    for (final item in decoded) {
      if (item is! Map) continue;
      final id = item['id'];
      if (id is! String || !seen.add(id)) continue;
      final label = item['label'];
      final kind = item['kind'];
      final progress = item['progress'];
      entries.add(
        RingEntry(
          id,
          label is String ? label : '',
          kind is String ? kind : '',
          progress is num ? progress.toDouble().clamp(0, 1).toDouble() : 0,
        ),
      );
      if (entries.length == 24) break;
    }
    return entries;
  }
}

/// Live activity as pills on a circle; each pill is tappable and reports its item id.
final class ActivityRing extends NodeComponent {
  ActivityRing(super.node);

  final _items = <String, RingItem>{};
  List<RingItem> get items => _items.values.where((i) => !i.leaving).toList();

  double get radius => param('radius', 120).clamp(8, 4000).toDouble();

  @override
  void changed() {
    // Pills centre on the circle, so the box grows by one pill to hold them whole.
    size.setValues(
      radius * 2 + RingItem.pillSize.x,
      radius * 2 + RingItem.pillSize.y,
    );
    super.changed();
    final entries = RingEntry.parse(params['items']);
    final ids = entries.map((e) => e.id).toSet();
    for (final item in _items.values) {
      if (!ids.contains(item.entry.id)) item.leaving = true;
    }
    for (final (index, entry) in entries.indexed) {
      final angle = 2 * math.pi * index / entries.length - math.pi / 2;
      final home =
          size / 2 + Vector2(math.cos(angle), math.sin(angle)) * radius;
      final item = _items[entry.id];
      if (item == null || item.leaving) {
        item?.removeFromParent();
        final created = RingItem(this, entry, home)..position = size / 2;
        _items[entry.id] = created;
        add(created);
      } else {
        item
          ..entry = entry
          ..home = home;
      }
    }
    if (isMounted) game.wake();
  }

  void forget(RingItem item) {
    if (_items[item.entry.id] == item) _items.remove(item.entry.id);
  }

  void tapped(RingItem item) {
    game.wake();
    if (!inputs.contains('tap')) return;
    game.report(
      this,
      'tap',
      position + item.position - size / 2,
      item.entry.id,
    );
  }

  @visibleForTesting
  void simulateTap(String id) {
    final item = _items[id];
    if (item != null && !item.leaving) tapped(item);
  }

  // Only pills report; a tap between them passes through.
  @override
  void onTapUp(TapUpEvent event) => event.continuePropagation = true;

  @override
  void render(ui.Canvas canvas) {}
}

/// One pill: eases in from the ring's centre, fades out when its item leaves.
final class RingItem extends PositionComponent with TapCallbacks {
  RingItem(this.ring, this.entry, this.home)
    : super(size: pillSize.clone(), anchor: Anchor.center);

  static final pillSize = Vector2(96, 28);

  final ActivityRing ring;
  RingEntry entry;
  Vector2 home;
  bool leaving = false;
  double _alpha = 0;

  @override
  void update(double dt) {
    super.update(dt);
    final k = 1 - math.exp(-dt * 8);
    position += (home - position) * k;
    _alpha += ((leaving ? 0 : 1) - _alpha) * k;
    if (leaving && _alpha < 0.02) {
      ring.forget(this);
      removeFromParent();
      return;
    }
    if (leaving || _alpha < 0.999 || position.distanceTo(home) > 0.1) {
      ring.game.wake();
    }
  }

  @override
  void onTapUp(TapUpEvent event) => ring.tapped(this);

  static const _glyphs = {
    'route': '→',
    'behavior': '⚡',
    'app': '◆',
    'signal': '•',
  };

  @override
  void render(ui.Canvas canvas) {
    final alpha = _alpha.clamp(0.0, 1.0);
    canvas.drawRRect(
      ui.RRect.fromRectAndRadius(size.toRect(), const ui.Radius.circular(14)),
      ui.Paint()..color = const ui.Color(0xFFE4EEE5).withValues(alpha: alpha),
    );
    canvas.drawArc(
      ui.Rect.fromCircle(center: ui.Offset(14, size.y / 2), radius: 9),
      -math.pi / 2,
      2 * math.pi * entry.progress,
      false,
      ui.Paint()
        ..style = ui.PaintingStyle.stroke
        ..strokeWidth = 2
        ..color = const ui.Color(0xFF397B63).withValues(alpha: alpha),
    );
    final text = TextPainter(
      text: TextSpan(
        text: '${_glyphs[entry.kind] ?? '·'} ${entry.label}',
        style: TextStyle(
          fontSize: 11,
          color: const ui.Color(0xFF263934).withValues(alpha: alpha),
        ),
      ),
      textDirection: TextDirection.ltr,
      maxLines: 1,
      ellipsis: '…',
    )..layout(maxWidth: size.x - 30);
    text.paint(canvas, ui.Offset(26, (size.y - text.height) / 2));
  }
}
