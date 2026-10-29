import 'dart:math' as math;

import 'package:flutter/scheduler.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import 'controls.dart';
import 'tokens.dart';

/// A 3D node in the DigitalBrain topology.
class BrainNode3D {
  const BrainNode3D({
    required this.id,
    required this.neuronUri,
    required this.label,
    required this.module,
    required this.position,
    this.status = BrainNodeStatus.healthy,
    this.appId,
    this.signalsPerMin = 12,
  });

  final String id;
  final String neuronUri;
  final String label;
  final String module;
  final (double, double, double) position;
  final BrainNodeStatus status;
  final String? appId;
  final int signalsPerMin;
}

enum BrainNodeStatus { healthy, working, warning, error }

/// A directed synapse connecting two 3D neurons.
class BrainSynapse3D {
  const BrainSynapse3D(this.fromId, this.toId, {this.signalType = 'Signal'});
  final String fromId;
  final String toId;
  final String signalType;
}

/// Default DigitalBrain 3D topology representing the active user brain.
const defaultBrainNodes = <BrainNode3D>[
  BrainNode3D(
    id: 'ino',
    neuronUri: 'neuron://nordlys/apps.ino/main',
    label: 'Ino Assistant',
    module: 'IntoChat.OS.Apps',
    position: (0.0, 0.0, 0.0),
    status: BrainNodeStatus.working,
    appId: 'chat',
    signalsPerMin: 48,
  ),
  BrainNode3D(
    id: 'memory',
    neuronUri: 'neuron://nordlys/os.memory/main',
    label: 'Semantic Memory',
    module: 'IntoChat.OS.Memory',
    position: (-0.52, -0.28, 0.34),
    status: BrainNodeStatus.healthy,
    appId: 'settings',
    signalsPerMin: 24,
  ),
  BrainNode3D(
    id: 'files',
    neuronUri: 'neuron://nordlys/storage.files/root',
    label: 'FileSystem Neuron',
    module: 'IntoChat.OS.Storage',
    position: (0.56, -0.22, 0.28),
    status: BrainNodeStatus.healthy,
    appId: 'files',
    signalsPerMin: 19,
  ),
  BrainNode3D(
    id: 'creator',
    neuronUri: 'neuron://nordlys/apps.creator/main',
    label: 'Creator Loop',
    module: 'IntoChat.OS.Apps',
    position: (0.0, -0.58, -0.25),
    status: BrainNodeStatus.healthy,
    appId: 'creator',
    signalsPerMin: 15,
  ),
  BrainNode3D(
    id: 'aspire',
    neuronUri: 'neuron://nordlys/microsoft.aspire/telemetry',
    label: 'Aspire Telemetry',
    module: 'Microsoft.Aspire',
    position: (0.46, 0.38, -0.32),
    status: BrainNodeStatus.healthy,
    appId: 'creator',
    signalsPerMin: 64,
  ),
  BrainNode3D(
    id: 'bdd',
    neuronUri: 'neuron://nordlys/digitalbrain.bdd/runner',
    label: 'BDD Scenario Gate',
    module: 'DigitalBrain.Bdd',
    position: (-0.48, 0.36, -0.30),
    status: BrainNodeStatus.healthy,
    appId: 'creator',
    signalsPerMin: 9,
  ),
  BrainNode3D(
    id: 'salesforce',
    neuronUri: 'neuron://nordlys/salesforce.crm/opportunities',
    label: 'Salesforce Sync',
    module: 'IntoChat.Modules.Salesforce',
    position: (0.68, 0.12, 0.42),
    status: BrainNodeStatus.working,
    appId: 'apps',
    signalsPerMin: 31,
  ),
  BrainNode3D(
    id: 'gmail',
    neuronUri: 'neuron://nordlys/google.gmail/inbox',
    label: 'Gmail Webhook',
    module: 'IntoChat.Modules.Google',
    position: (-0.66, 0.08, 0.44),
    status: BrainNodeStatus.healthy,
    appId: 'chat',
    signalsPerMin: 27,
  ),
  BrainNode3D(
    id: 'settings',
    neuronUri: 'neuron://nordlys/os.identity/settings',
    label: 'User Settings',
    module: 'IntoChat.OS.Identity',
    position: (0.18, 0.56, 0.25),
    status: BrainNodeStatus.healthy,
    appId: 'settings',
    signalsPerMin: 6,
  ),
  BrainNode3D(
    id: 'gallery',
    neuronUri: 'neuron://nordlys/flutter.ui/gallery',
    label: 'UI Kit Gallery',
    module: 'IntoChat.OS.Flutter',
    position: (-0.22, -0.52, 0.48),
    status: BrainNodeStatus.healthy,
    appId: 'gallery',
    signalsPerMin: 14,
  ),
];

const defaultBrainSynapses = <BrainSynapse3D>[
  BrainSynapse3D('ino', 'memory', signalType: 'MemoryRecalled'),
  BrainSynapse3D('ino', 'files', signalType: 'FileRead'),
  BrainSynapse3D('ino', 'creator', signalType: 'StudioSynthesize'),
  BrainSynapse3D('creator', 'aspire', signalType: 'AspireTraceQueried'),
  BrainSynapse3D('creator', 'bdd', signalType: 'BddRunCompleted'),
  BrainSynapse3D('gmail', 'ino', signalType: 'EmailReceived'),
  BrainSynapse3D('salesforce', 'ino', signalType: 'OpportunityUpdated'),
  BrainSynapse3D('settings', 'ino', signalType: 'UserSettingsChanged'),
  BrainSynapse3D('gallery', 'creator', signalType: 'ViewChanged'),
  BrainSynapse3D('files', 'memory', signalType: 'DocumentIndexed'),
];

/// Interactive 3D DigitalBrain visualizer with orbital drag, signal photon
/// pulses along synapses, and live neuron node inspection.
class BrainScene3D extends StatefulWidget {
  const BrainScene3D({
    super.key,
    this.nodes = defaultBrainNodes,
    this.synapses = defaultBrainSynapses,
    this.selectedId,
    this.onSelectNode,
    this.interactive = true,
    this.showHud = true,
    this.ambientOpacity = 1.0,
  });

  final List<BrainNode3D> nodes;
  final List<BrainSynapse3D> synapses;
  final String? selectedId;
  final ValueChanged<BrainNode3D>? onSelectNode;
  final bool interactive;
  final bool showHud;
  final double ambientOpacity;

  @override
  State<BrainScene3D> createState() => _BrainScene3DState();
}

class _BrainScene3DState extends State<BrainScene3D>
    with SingleTickerProviderStateMixin {
  late final Ticker _ticker;
  double _time = 0;
  double _yaw = 0.35;
  double _pitch = -0.18;
  String? _hoveredId;
  final Map<String, Offset> _projectedCenters = {};

  @override
  void initState() {
    super.initState();
    _ticker = createTicker((elapsed) {
      setState(() {
        _time = elapsed.inMilliseconds / 1000.0;
        if (!_dragging) {
          _yaw += 0.0022;
        }
      });
    });
    if (!SchedulerBinding.instance.runtimeType.toString().contains('Test')) {
      _ticker.start();
    }
  }

  bool _dragging = false;

  @override
  void dispose() {
    _ticker.dispose();
    super.dispose();
  }

  BrainNode3D? _hitTest(Offset localPos) {
    BrainNode3D? best;
    var bestDist = 28.0;
    for (final n in widget.nodes) {
      final p = _projectedCenters[n.id];
      if (p == null) continue;
      final d = (p - localPos).distance;
      if (d < bestDist) {
        bestDist = d;
        best = n;
      }
    }
    return best;
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final selected = widget.nodes
        .where((n) => n.id == (widget.selectedId ?? _hoveredId))
        .firstOrNull;

    final canvas = CustomPaint(
      painter: _Brain3DPainter(
        colors: c,
        nodes: widget.nodes,
        synapses: widget.synapses,
        time: _time,
        yaw: _yaw,
        pitch: _pitch,
        selectedId: widget.selectedId,
        hoveredId: _hoveredId,
        ambientOpacity: widget.ambientOpacity,
        onProjected: (map) {
          _projectedCenters
            ..clear()
            ..addAll(map);
        },
      ),
      child: const SizedBox.expand(),
    );

    if (!widget.interactive) return IgnorePointer(child: canvas);

    return MouseRegion(
      cursor: _hoveredId != null
          ? SystemMouseCursors.click
          : (_dragging ? SystemMouseCursors.grabbing : SystemMouseCursors.grab),
      onHover: (e) {
        final hit = _hitTest(e.localPosition)?.id;
        if (hit != _hoveredId) setState(() => _hoveredId = hit);
      },
      onExit: (_) => setState(() => _hoveredId = null),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onPanStart: (_) => setState(() => _dragging = true),
        onPanUpdate: (d) {
          setState(() {
            _yaw += d.delta.dx * 0.007;
            _pitch = (_pitch + d.delta.dy * 0.007).clamp(-1.1, 1.1);
          });
        },
        onPanEnd: (_) => setState(() => _dragging = false),
        onTapUp: (d) {
          final hit = _hitTest(d.localPosition);
          if (hit != null) widget.onSelectNode?.call(hit);
        },
        child: Stack(
          children: [
            Positioned.fill(child: canvas),
            if (widget.showHud && selected != null)
              Positioned(
                left: 16,
                bottom: 16,
                child: IcAcrylicBox(
                  radius: IcRadius.button,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 10,
                  ),
                  shadows: IcShadow.md(c),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(LucideIcons.cpu, size: 16, color: c.accent),
                      const SizedBox(width: 10),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Row(
                            children: [
                              Text(
                                selected.label,
                                style: IcType.text(
                                  13,
                                  weight: FontWeight.w600,
                                  color: c.textStrong,
                                ),
                              ),
                              const SizedBox(width: 8),
                              TagPill(
                                label: '${selected.signalsPerMin} sig/m',
                                tone: TagTone.accent,
                              ),
                            ],
                          ),
                          const SizedBox(height: 2),
                          Text(
                            selected.neuronUri,
                            style: IcType.text(
                              11.5,
                              color: c.textMuted,
                              tnum: true,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _Brain3DPainter extends CustomPainter {
  _Brain3DPainter({
    required this.colors,
    required this.nodes,
    required this.synapses,
    required this.time,
    required this.yaw,
    required this.pitch,
    required this.selectedId,
    required this.hoveredId,
    required this.ambientOpacity,
    required this.onProjected,
  });

  final IcColors colors;
  final List<BrainNode3D> nodes;
  final List<BrainSynapse3D> synapses;
  final double time;
  final double yaw;
  final double pitch;
  final String? selectedId;
  final String? hoveredId;
  final double ambientOpacity;
  final ValueChanged<Map<String, Offset>> onProjected;

  (Offset, double) _project((double, double, double) p, Size size) {
    final (x0, y0, z0) = p;
    final cosY = math.cos(yaw);
    final sinY = math.sin(yaw);
    final x1 = x0 * cosY - z0 * sinY;
    final z1 = x0 * sinY + z0 * cosY;

    final cosP = math.cos(pitch);
    final sinP = math.sin(pitch);
    final y2 = y0 * cosP - z1 * sinP;
    final z2 = y0 * sinP + z1 * cosP;

    final radius = math.min(size.width, size.height) * 0.36;
    final perspective = 2.4 / (2.4 - z2 * 0.75);
    final sx = size.width * 0.5 + x1 * radius * perspective;
    final sy = size.height * 0.48 + y2 * radius * perspective;
    return (Offset(sx, sy), perspective);
  }

  @override
  void paint(Canvas canvas, Size size) {
    final c = colors;
    final alpha = ambientOpacity.clamp(0.15, 1.0);

    final center = Offset(size.width * 0.5, size.height * 0.48);
    final maxR = math.min(size.width, size.height) * 0.46;
    final bgGlow = Paint()
      ..shader = RadialGradient(
        colors: [
          c.ambientGlow.withValues(alpha: (c.isDark ? 0.16 : 0.10) * alpha),
          c.secondaryGlow.withValues(alpha: (c.isDark ? 0.08 : 0.05) * alpha),
          const Color(0x00000000),
        ],
        stops: const [0.0, 0.55, 1.0],
      ).createShader(Rect.fromCircle(center: center, radius: maxR));
    canvas.drawCircle(center, maxR, bgGlow);

    final orbitPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.0
      ..color = c.accent.withValues(alpha: (c.isDark ? 0.10 : 0.08) * alpha);
    canvas.drawOval(
      Rect.fromCenter(center: center, width: maxR * 1.45, height: maxR * 0.62),
      orbitPaint,
    );
    canvas.drawOval(
      Rect.fromCenter(center: center, width: maxR * 1.05, height: maxR * 0.92),
      orbitPaint,
    );

    final projected = <String, (Offset, double, BrainNode3D)>{};
    final hitCenters = <String, Offset>{};
    for (final n in nodes) {
      final (pt, scale) = _project(n.position, size);
      projected[n.id] = (pt, scale, n);
      hitCenters[n.id] = pt;
    }
    onProjected(hitCenters);

    for (var i = 0; i < synapses.length; i++) {
      final syn = synapses[i];
      final from = projected[syn.fromId];
      final to = projected[syn.toId];
      if (from == null || to == null) continue;

      final p1 = from.$1;
      final p2 = to.$1;
      final linePaint = Paint()
        ..strokeWidth = 1.25
        ..color = c.accent.withValues(alpha: (c.isDark ? 0.24 : 0.18) * alpha);
      canvas.drawLine(p1, p2, linePaint);

      final t = ((time * 0.55 + i * 0.21) % 1.0);
      final sparkPos = Offset.lerp(p1, p2, t)!;
      final sparkGlow = Paint()
        ..color = c.secondaryGlow.withValues(alpha: 0.35 * alpha)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6);
      canvas.drawCircle(sparkPos, 5.0, sparkGlow);
      canvas.drawCircle(
        sparkPos,
        2.4,
        Paint()
          ..color = (c.isDark ? const Color(0xFFFFFFFF) : c.accent).withValues(
            alpha: 0.9 * alpha,
          ),
      );
    }

    final sorted = projected.values.toList()
      ..sort((a, b) => a.$2.compareTo(b.$2));

    for (final (pt, scale, node) in sorted) {
      final isSel = node.id == selectedId || node.id == hoveredId;
      final nodeColor = switch (node.status) {
        BrainNodeStatus.error => c.error,
        BrainNodeStatus.warning || BrainNodeStatus.working => c.working,
        BrainNodeStatus.healthy => c.accent,
      };
      final baseR =
          (node.id == 'ino' ? 11.0 : 7.5) * scale * (isSel ? 1.25 : 1.0);

      final pulse = 1.0 + 0.18 * math.sin(time * 2.8 + node.id.hashCode);
      final haloPaint = Paint()
        ..color = nodeColor.withValues(alpha: (isSel ? 0.38 : 0.20) * alpha)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 10);
      canvas.drawCircle(pt, baseR * 1.9 * pulse, haloPaint);

      final sphereShader = RadialGradient(
        center: const Alignment(-0.35, -0.35),
        radius: 0.9,
        colors: [
          Color.lerp(
            nodeColor,
            const Color(0xFFFFFFFF),
            0.55,
          )!.withValues(alpha: alpha),
          nodeColor.withValues(alpha: alpha),
          Color.lerp(
            nodeColor,
            const Color(0xFF000000),
            0.45,
          )!.withValues(alpha: alpha),
        ],
      ).createShader(Rect.fromCircle(center: pt, radius: baseR));
      canvas.drawCircle(pt, baseR, Paint()..shader = sphereShader);

      if (isSel) {
        canvas.drawCircle(
          pt,
          baseR + 4,
          Paint()
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.6
            ..color = c.textStrong.withValues(alpha: 0.85 * alpha),
        );
      }

      final tp = TextPainter(
        text: TextSpan(
          text: node.label,
          style: IcType.text(
            11.5 * scale.clamp(0.85, 1.15),
            weight: isSel ? FontWeight.w700 : FontWeight.w500,
            color: (isSel ? c.textStrong : c.textBody).withValues(
              alpha: (isSel ? 1.0 : 0.82) * alpha,
            ),
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      tp.paint(canvas, Offset(pt.dx - tp.width / 2, pt.dy + baseR + 5));
    }
  }

  @override
  bool shouldRepaint(covariant _Brain3DPainter old) => true;
}
