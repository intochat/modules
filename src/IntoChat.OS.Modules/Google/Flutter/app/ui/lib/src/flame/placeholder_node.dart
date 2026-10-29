import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flame/components.dart';
import 'package:flutter/painting.dart';

import 'node_components.dart';

/// Any node type this shell does not know: a dashed outline and the type name.
final class PlaceholderNode extends NodeComponent {
  PlaceholderNode(super.node);
  TextPainter? _label;

  @override
  void changed() {
    size.setValues(param('width', 96), param('height', 48));
    super.changed();
    _label = TextPainter(
      text: TextSpan(
        text: typeName,
        style: const TextStyle(color: Color(0xFF9AA4B2), fontSize: 11),
      ),
      textDirection: TextDirection.ltr,
    )..layout(maxWidth: size.x);
  }

  @override
  void render(ui.Canvas canvas) {
    final outline = ui.Paint()
      ..color = const Color(0xFF9AA4B2)
      ..style = ui.PaintingStyle.stroke
      ..strokeWidth = 1;
    const dash = 6.0;
    void dashed(Offset from, Offset to) {
      final length = (to - from).distance;
      final step = (to - from) / length;
      for (var d = 0.0; d < length; d += dash * 2) {
        canvas.drawLine(
          from + step * d,
          from + step * math.min(d + dash, length),
          outline,
        );
      }
    }

    final r = size.toRect();
    dashed(r.topLeft, r.topRight);
    dashed(r.topRight, r.bottomRight);
    dashed(r.bottomRight, r.bottomLeft);
    dashed(r.bottomLeft, r.topLeft);
    final label = _label;
    if (label != null) {
      label.paint(
        canvas,
        Offset((size.x - label.width) / 2, (size.y - label.height) / 2),
      );
    }
  }
}
