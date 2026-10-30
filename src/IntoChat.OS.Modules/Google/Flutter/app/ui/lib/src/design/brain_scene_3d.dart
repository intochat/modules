import 'dart:math' as math;

import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import 'controls.dart';
import 'tokens.dart';

enum BrainNodeStatus { active, working, needsYou, idle, error }

enum BrainNodeKind { assistant, app, storage, compute, model, workflow }

@immutable
class BrainNode3D {
  const BrainNode3D({
    required this.id,
    required this.label,
    required this.uri,
    required this.kind,
    required this.position,
    this.status = BrainNodeStatus.idle,
    this.subtitle,
    this.moduleId = 'IntoChat.OS.Core',
    this.signalsPerMin = 12,
    this.appId,
  });

  final String id;
  final String label;
  final String uri;
  final BrainNodeKind kind;

  /// Normalized 3D coordinates in [-1.0, 1.0].
  final (double x, double y, double z) position;
  final BrainNodeStatus status;
  final String? subtitle;
  final String moduleId;
  final int signalsPerMin;
  final String? appId;

  String get neuronUri => uri;
}

@immutable
class BrainSynapse3D {
  const BrainSynapse3D({
    required this.fromId,
    required this.toId,
    this.signalLabel,
    this.active = false,
  });

  final String fromId;
  final String toId;
  final String? signalLabel;
  final bool active;
}

/// Interactive 3D DigitalBrain Neural Graph canvas with theme-aware 3D aesthetics:
/// - `IcBrainShaderMode.volumetricGlass`: Volumetric glass spheres with specular highlights
/// - `IcBrainShaderMode.architecturalBlueprint`: Architectural blueprint grid, crosshairs & drafting rings
/// - `IcBrainShaderMode.starMapConstellation`: Star-map constellation with starfield & orbital tracks
/// - `IcBrainShaderMode.wireframeHud`: Wireframe HUD with sci-fi reticles & latitude/longitude cages
class BrainScene3D extends StatefulWidget {
  const BrainScene3D({
    super.key,
    this.nodes = defaultCluster,
    this.synapses = defaultSynapses,
    this.selectedId,
    this.onSelectNode,
    this.interactive = true,
    this.autoRotate = true,
    this.showHud = true,
    this.compact = false,
    this.orbitSpeed = 1.0,
    this.shaderMode,
    this.pulseEpoch = 0,
    this.ambientOpacity = 1.0,
  });

  final List<BrainNode3D> nodes;
  final List<BrainSynapse3D> synapses;
  final String? selectedId;
  final ValueChanged<BrainNode3D>? onSelectNode;
  final bool interactive;
  final bool autoRotate;
  final bool showHud;
  final bool compact;
  final double orbitSpeed;
  final IcBrainShaderMode? shaderMode;
  final int pulseEpoch;
  final double ambientOpacity;

  static const List<BrainNode3D> defaultCluster = [
    BrainNode3D(
      id: 'ino',
      label: 'Ino Coordinator',
      uri: 'neuron://local/ino/main',
      kind: BrainNodeKind.assistant,
      position: (0.0, 0.0, 0.0),
      status: BrainNodeStatus.working,
      subtitle: 'Orchestrating 4 active synapses',
      moduleId: 'IntoChat.OS.Assistant',
      signalsPerMin: 142,
      appId: 'ino',
    ),
    BrainNode3D(
      id: 'claude-sonnet',
      label: 'Claude 4.5 Sonnet',
      uri: 'neuron://anthropic/claude-sonnet-4-5',
      kind: BrainNodeKind.model,
      position: (-0.58, -0.34, 0.42),
      status: BrainNodeStatus.working,
      subtitle: 'Streaming LFP cards · 68 tok/s',
      moduleId: 'IntoChat.AI.Anthropic',
      signalsPerMin: 96,
      appId: 'ino',
    ),
    BrainNode3D(
      id: 'files-store',
      label: 'Files Vault',
      uri: 'neuron://local/storage/files',
      kind: BrainNodeKind.storage,
      position: (0.62, -0.26, 0.35),
      status: BrainNodeStatus.active,
      subtitle: '1,284 artifacts indexed',
      moduleId: 'IntoChat.OS.Storage',
      signalsPerMin: 38,
      appId: 'files',
    ),
    BrainNode3D(
      id: 'crm-sync',
      label: 'Salesforce Sync',
      uri: 'neuron://apps/crm/pipeline-q2',
      kind: BrainNodeKind.app,
      position: (-0.52, 0.46, -0.30),
      status: BrainNodeStatus.needsYou,
      subtitle: 'Awaiting discount approval (18%)',
      moduleId: 'IntoChat.Apps.CRM',
      signalsPerMin: 19,
      appId: 'post',
    ),
    BrainNode3D(
      id: 'sandbox-csharp',
      label: 'C# App Sandbox',
      uri: 'neuron://runtime/sandbox/csharp',
      kind: BrainNodeKind.compute,
      position: (0.54, 0.44, -0.38),
      status: BrainNodeStatus.active,
      subtitle: 'Orleans grain pool · 12ms p99',
      moduleId: 'DigitalBrain.Sandbox',
      signalsPerMin: 64,
      appId: 'creator',
    ),
    BrainNode3D(
      id: 'memory-graph',
      label: 'Semantic Memory',
      uri: 'neuron://local/memory/episodic',
      kind: BrainNodeKind.storage,
      position: (0.06, -0.64, -0.48),
      status: BrainNodeStatus.active,
      subtitle: 'Vector recall · 99.4% hit',
      moduleId: 'DigitalBrain.Memory',
      signalsPerMin: 51,
      appId: 'circles',
    ),
    BrainNode3D(
      id: 'deploy-gate',
      label: 'BDD Scenario Gate',
      uri: 'neuron://runtime/testing/bdd-gate',
      kind: BrainNodeKind.workflow,
      position: (-0.12, 0.66, 0.48),
      status: BrainNodeStatus.active,
      subtitle: '14/14 [Scenario] green',
      moduleId: 'DigitalBrain.Testing',
      signalsPerMin: 27,
      appId: 'gallery',
    ),
  ];

  static const List<BrainSynapse3D> defaultSynapses = [
    BrainSynapse3D(
      fromId: 'ino',
      toId: 'claude-sonnet',
      signalLabel: 'LfpPromptSignal',
      active: true,
    ),
    BrainSynapse3D(
      fromId: 'ino',
      toId: 'files-store',
      signalLabel: 'ReadArtifactSignal',
      active: true,
    ),
    BrainSynapse3D(
      fromId: 'ino',
      toId: 'crm-sync',
      signalLabel: 'ApprovalRequestSignal',
      active: true,
    ),
    BrainSynapse3D(
      fromId: 'ino',
      toId: 'sandbox-csharp',
      signalLabel: 'CompileAppSignal',
      active: true,
    ),
    BrainSynapse3D(
      fromId: 'ino',
      toId: 'memory-graph',
      signalLabel: 'RecallContextSignal',
      active: false,
    ),
    BrainSynapse3D(
      fromId: 'sandbox-csharp',
      toId: 'deploy-gate',
      signalLabel: 'VerifyScenarioSignal',
      active: true,
    ),
    BrainSynapse3D(
      fromId: 'claude-sonnet',
      toId: 'memory-graph',
      signalLabel: 'EmbedEpisodeSignal',
      active: false,
    ),
  ];

  @override
  State<BrainScene3D> createState() => _BrainScene3DState();
}

class _BrainScene3DState extends State<BrainScene3D>
    with SingleTickerProviderStateMixin {
  late final AnimationController _ticker;
  double _yaw = 0.42;
  double _pitch = -0.22;
  double _zoom = 1.0;
  String? _hoveredId;
  Offset? _lastDrag;
  final Map<String, Offset> _projectedCenters = {};

  @override
  void initState() {
    super.initState();
    _ticker = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 20),
    );
    final isTest = WidgetsBinding.instance.runtimeType.toString().contains(
      'Test',
    );
    if (!isTest) {
      _ticker.repeat();
    }
  }

  @override
  void dispose() {
    _ticker.dispose();
    super.dispose();
  }

  void _handleTapUp(TapUpDetails details) {
    if (!widget.interactive) return;
    final pos = details.localPosition;
    String? hitId;
    double bestDist = 34.0;
    for (final entry in _projectedCenters.entries) {
      final d = (entry.value - pos).distance;
      if (d < bestDist) {
        bestDist = d;
        hitId = entry.key;
      }
    }
    if (hitId != null && widget.onSelectNode != null) {
      final node = widget.nodes.firstWhere((n) => n.id == hitId);
      widget.onSelectNode!(node);
    }
  }

  void _handleHover(Offset pos) {
    String? hitId;
    double bestDist = 32.0;
    for (final entry in _projectedCenters.entries) {
      final d = (entry.value - pos).distance;
      if (d < bestDist) {
        bestDist = d;
        hitId = entry.key;
      }
    }
    if (hitId != _hoveredId) {
      setState(() => _hoveredId = hitId);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final effectiveShader = widget.shaderMode ?? c.preset.defaultShaderMode;
    return LayoutBuilder(
      builder: (context, constraints) {
        final size = Size(constraints.maxWidth, constraints.maxHeight);
        final scene = MouseRegion(
          onHover: widget.interactive
              ? (e) => _handleHover(e.localPosition)
              : null,
          onExit: (_) => setState(() => _hoveredId = null),
          cursor: _hoveredId != null
              ? SystemMouseCursors.click
              : (widget.interactive
                    ? SystemMouseCursors.grab
                    : MouseCursor.defer),
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTapUp: _handleTapUp,
            onPanStart: widget.interactive
                ? (d) => _lastDrag = d.localPosition
                : null,
            onPanUpdate: widget.interactive
                ? (d) {
                    final delta =
                        d.localPosition - (_lastDrag ?? d.localPosition);
                    _lastDrag = d.localPosition;
                    setState(() {
                      _yaw += delta.dx * 0.009;
                      _pitch = (_pitch + delta.dy * 0.007).clamp(-1.1, 1.1);
                    });
                  }
                : null,
            child: Stack(
              fit: StackFit.expand,
              children: [
                AnimatedBuilder(
                  animation: _ticker,
                  builder: (context, _) {
                    final t = _ticker.value;
                    final effectiveYaw =
                        _yaw +
                        (widget.autoRotate
                            ? t * math.pi * 2 * 0.18 * widget.orbitSpeed
                            : 0);
                    return CustomPaint(
                      size: size,
                      painter: _BrainScenePainter(
                        colors: c,
                        nodes: widget.nodes,
                        synapses: widget.synapses,
                        yaw: effectiveYaw,
                        pitch: _pitch,
                        zoom: _zoom,
                        pulsePhase: (t * 6.0 + widget.pulseEpoch * 0.35) % 1.0,
                        selectedId: widget.selectedId,
                        hoveredId: _hoveredId,
                        compact: widget.compact,
                        shaderMode: effectiveShader,
                        onProjected: (map) {
                          _projectedCenters
                            ..clear()
                            ..addAll(map);
                        },
                      ),
                    );
                  },
                ),
                if (widget.showHud && !widget.compact)
                  Positioned(
                    left: 16,
                    top: 14,
                    child: IgnorePointer(
                      child: Row(
                        children: [
                          TagPill(
                            label:
                                '3D NEURAL TOPOLOGY · ${effectiveShader.label.toUpperCase()}',
                            tone: TagTone.accent,
                            icon: LucideIcons.orbit,
                          ),
                          const SizedBox(width: 8),
                          TagPill(
                            label:
                                '${widget.nodes.length} NEURONS · ${widget.synapses.length} SYNAPSES',
                            tone: TagTone.neutral,
                          ),
                        ],
                      ),
                    ),
                  ),
                if (widget.showHud && widget.interactive && !widget.compact)
                  Positioned(
                    right: 16,
                    bottom: 14,
                    child: IcAcrylicBox(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 5,
                      ),
                      borderRadius: BorderRadius.circular(IcRadius.pill),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          _HudIconBtn(
                            icon: LucideIcons.zoomIn,
                            tooltip: 'Zoom in',
                            onTap: () => setState(
                              () => _zoom = (_zoom + 0.15).clamp(0.65, 1.65),
                            ),
                          ),
                          const SizedBox(width: 4),
                          _HudIconBtn(
                            icon: LucideIcons.zoomOut,
                            tooltip: 'Zoom out',
                            onTap: () => setState(
                              () => _zoom = (_zoom - 0.15).clamp(0.65, 1.65),
                            ),
                          ),
                          const SizedBox(width: 4),
                          _HudIconBtn(
                            icon: LucideIcons.rotateCcw,
                            tooltip: 'Reset orbit',
                            onTap: () => setState(() {
                              _yaw = 0.42;
                              _pitch = -0.22;
                              _zoom = 1.0;
                            }),
                          ),
                        ],
                      ),
                    ),
                  ),
              ],
            ),
          ),
        );
        if (widget.ambientOpacity < 1.0) {
          return Opacity(
            opacity: widget.ambientOpacity.clamp(0.0, 1.0),
            child: scene,
          );
        }
        return scene;
      },
    );
  }
}

class _HudIconBtn extends StatelessWidget {
  const _HudIconBtn({
    required this.icon,
    required this.tooltip,
    required this.onTap,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 24,
        height: 24,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: c.surfaceSunk.withValues(alpha: 0.6),
          shape: BoxShape.circle,
        ),
        child: Icon(icon, size: 12, color: c.textMuted),
      ),
    );
  }
}

class _ProjectedNode {
  const _ProjectedNode({
    required this.node,
    required this.screen,
    required this.depth,
    required this.scale,
  });

  final BrainNode3D node;
  final Offset screen;
  final double depth;
  final double scale;
}

class _BrainScenePainter extends CustomPainter {
  _BrainScenePainter({
    required this.colors,
    required this.nodes,
    required this.synapses,
    required this.yaw,
    required this.pitch,
    required this.zoom,
    required this.pulsePhase,
    required this.selectedId,
    required this.hoveredId,
    required this.compact,
    required this.shaderMode,
    required this.onProjected,
  });

  final IcColors colors;
  final List<BrainNode3D> nodes;
  final List<BrainSynapse3D> synapses;
  final double yaw;
  final double pitch;
  final double zoom;
  final double pulsePhase;
  final String? selectedId;
  final String? hoveredId;
  final bool compact;
  final IcBrainShaderMode shaderMode;
  final ValueChanged<Map<String, Offset>> onProjected;

  Color _nodeColor(BrainNode3D n) {
    return switch (n.status) {
      BrainNodeStatus.working => colors.accent,
      BrainNodeStatus.needsYou => colors.working,
      BrainNodeStatus.active => colors.done,
      BrainNodeStatus.error => colors.error,
      BrainNodeStatus.idle => colors.textMuted,
    };
  }

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final radius = math.min(size.width, size.height) * 0.36 * zoom;

    // 1. Theme-aware background atmosphere & orbital grid
    _paintBackgroundAtmosphere(canvas, size, center, radius);

    // 2. Project 3D coordinates with perspective camera
    final cosY = math.cos(yaw);
    final sinY = math.sin(yaw);
    final cosP = math.cos(pitch);
    final sinP = math.sin(pitch);

    final projected = <String, _ProjectedNode>{};
    final hitMap = <String, Offset>{};

    for (final n in nodes) {
      final (x, y, z) = n.position;
      final rx = x * cosY - z * sinY;
      final rz = x * sinY + z * cosY;
      final ry = y * cosP - rz * sinP;
      final rz2 = y * sinP + rz * cosP;

      const fov = 2.6;
      final scale = fov / (fov + rz2 * 0.65);
      final sx = center.dx + rx * radius * scale;
      final sy = center.dy + ry * radius * scale;

      final screenPos = Offset(sx, sy);
      projected[n.id] = _ProjectedNode(
        node: n,
        screen: screenPos,
        depth: rz2,
        scale: scale,
      );
      hitMap[n.id] = screenPos;
    }

    onProjected(hitMap);

    // 3. Draw synapses (sorted back-to-front by average depth)
    for (final syn in synapses) {
      final from = projected[syn.fromId];
      final to = projected[syn.toId];
      if (from == null || to == null) continue;

      final avgDepth = (from.depth + to.depth) * 0.5;
      final depthAlpha = ((1.0 - avgDepth * 0.35).clamp(0.25, 1.0));
      final isHighlighted =
          selectedId == syn.fromId ||
          selectedId == syn.toId ||
          hoveredId == syn.fromId ||
          hoveredId == syn.toId;

      final lineColor = (syn.active || isHighlighted)
          ? colors.accent.withValues(
              alpha: (isHighlighted ? 0.68 : 0.38) * depthAlpha,
            )
          : colors.borderStrong.withValues(alpha: 0.25 * depthAlpha);

      final mid = Offset(
        (from.screen.dx + to.screen.dx) * 0.5,
        (from.screen.dy + to.screen.dy) * 0.5 -
            (shaderMode == IcBrainShaderMode.architecturalBlueprint
                ? 0
                : 18 * from.scale),
      );

      final path = Path()
        ..moveTo(from.screen.dx, from.screen.dy)
        ..quadraticBezierTo(mid.dx, mid.dy, to.screen.dx, to.screen.dy);

      canvas.drawPath(
        path,
        Paint()
          ..color = lineColor
          ..style = PaintingStyle.stroke
          ..strokeWidth = (isHighlighted ? 2.0 : 1.2) * from.scale,
      );

      if (syn.active) {
        final t = (pulsePhase + syn.fromId.hashCode * 0.17) % 1.0;
        final invT = 1 - t;
        final px =
            invT * invT * from.screen.dx +
            2 * invT * t * mid.dx +
            t * t * to.screen.dx;
        final py =
            invT * invT * from.screen.dy +
            2 * invT * t * mid.dy +
            t * t * to.screen.dy;
        final pulsePos = Offset(px, py);

        canvas.drawCircle(
          pulsePos,
          5.5 * from.scale,
          Paint()
            ..color = colors.accent.withValues(alpha: 0.35 * depthAlpha)
            ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6),
        );
        canvas.drawCircle(
          pulsePos,
          2.6 * from.scale,
          Paint()..color = colors.surfaceInk.withValues(alpha: 0.95),
        );
      }
    }

    // 4. Sort nodes back-to-front (highest depth drawn first)
    final sortedNodes = projected.values.toList()
      ..sort((a, b) => b.depth.compareTo(a.depth));

    for (final p in sortedNodes) {
      final n = p.node;
      final isSelected = n.id == selectedId;
      final isHovered = n.id == hoveredId;
      final isCore = n.kind == BrainNodeKind.assistant;
      final baseRadius = (isCore ? 20.0 : 13.5) * p.scale;
      final depthAlpha = (1.0 - p.depth * 0.28).clamp(0.35, 1.0);
      final toneColor = _nodeColor(n);

      _paintNodeByShaderMode(
        canvas,
        p,
        baseRadius,
        depthAlpha,
        toneColor,
        isSelected: isSelected,
        isHovered: isHovered,
      );

      // Node label pill below sphere
      if (!compact) {
        final labelStyle = IcType.text(
          (isCore ? 12.0 : 11.0) * p.scale.clamp(0.85, 1.15),
          weight: (isSelected || isCore) ? FontWeight.w600 : FontWeight.w500,
          color: colors.textStrong.withValues(alpha: depthAlpha),
        );
        final tp = TextPainter(
          text: TextSpan(text: n.label, style: labelStyle),
          textDirection: TextDirection.ltr,
        )..layout();

        final pillRect = RRect.fromRectAndRadius(
          Rect.fromCenter(
            center: p.screen + Offset(0, baseRadius + 14 * p.scale),
            width: tp.width + 14,
            height: tp.height + 6,
          ),
          const Radius.circular(999),
        );

        canvas.drawRRect(
          pillRect,
          Paint()
            ..color = colors.surfaceElevated.withValues(
              alpha: 0.82 * depthAlpha,
            ),
        );
        canvas.drawRRect(
          pillRect,
          Paint()
            ..color = (isSelected ? toneColor : colors.borderHairline)
                .withValues(alpha: 0.65 * depthAlpha)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1,
        );

        tp.paint(
          canvas,
          Offset(
            pillRect.center.dx - tp.width / 2,
            pillRect.center.dy - tp.height / 2,
          ),
        );
      }
    }
  }

  void _paintBackgroundAtmosphere(
    Canvas canvas,
    Size size,
    Offset center,
    double radius,
  ) {
    final bgGlow = Paint()
      ..shader = RadialGradient(
        colors: [
          colors.accent.withValues(alpha: colors.isDark ? 0.16 : 0.09),
          colors.glowColor.withValues(alpha: colors.isDark ? 0.06 : 0.03),
          const Color(0x00000000),
        ],
        stops: const [0.0, 0.55, 1.0],
      ).createShader(Rect.fromCircle(center: center, radius: radius * 1.6));
    canvas.drawCircle(center, radius * 1.6, bgGlow);

    switch (shaderMode) {
      case IcBrainShaderMode.architecturalBlueprint:
        // Crisp architectural blueprint grid + crosshairs + drafting rings
        final gridPaint = Paint()
          ..color = colors.borderHairline.withValues(alpha: 0.45)
          ..strokeWidth = 1;
        const step = 36.0;
        for (double x = center.dx % step; x < size.width; x += step) {
          canvas.drawLine(Offset(x, 0), Offset(x, size.height), gridPaint);
        }
        for (double y = center.dy % step; y < size.height; y += step) {
          canvas.drawLine(Offset(0, y), Offset(size.width, y), gridPaint);
        }
        final axisPaint = Paint()
          ..color = colors.accent.withValues(alpha: 0.25)
          ..strokeWidth = 1;
        canvas.drawLine(
          Offset(center.dx, 0),
          Offset(center.dx, size.height),
          axisPaint,
        );
        canvas.drawLine(
          Offset(0, center.dy),
          Offset(size.width, center.dy),
          axisPaint,
        );
        for (final ringScale in [0.55, 0.95, 1.32]) {
          canvas.drawCircle(
            center,
            radius * ringScale,
            Paint()
              ..color = colors.borderStrong.withValues(alpha: 0.4)
              ..style = PaintingStyle.stroke
              ..strokeWidth = 1,
          );
        }
      case IcBrainShaderMode.starMapConstellation:
        // Starfield particles + thin astronomical declination ellipses
        final starPaint = Paint()
          ..color = colors.textMuted.withValues(alpha: 0.35);
        for (var i = 0; i < 36; i++) {
          final sx = ((i * 73 + 19) % 100) / 100.0 * size.width;
          final sy = ((i * 41 + 37) % 100) / 100.0 * size.height;
          final sr = (i % 3 == 0) ? 1.4 : 0.9;
          canvas.drawCircle(Offset(sx, sy), sr, starPaint);
        }
        final ringPaint = Paint()
          ..color = colors.borderHairline.withValues(alpha: 0.45)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 0.8;
        for (final ringScale in [0.6, 1.0, 1.38]) {
          canvas.drawOval(
            Rect.fromCenter(
              center: center,
              width: radius * 2.3 * ringScale,
              height: radius * 0.85 * ringScale,
            ),
            ringPaint,
          );
        }
      case IcBrainShaderMode.wireframeHud:
        // Sci-Fi HUD reticle rings + radial radar spokes
        final hudPaint = Paint()
          ..color = colors.accent.withValues(alpha: 0.28)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.0;
        for (final ringScale in [0.5, 0.9, 1.25]) {
          canvas.drawCircle(center, radius * ringScale, hudPaint);
        }
        for (var i = 0; i < 8; i++) {
          final a = i * math.pi / 4 + yaw * 0.25;
          canvas.drawLine(
            center + Offset(math.cos(a), math.sin(a)) * (radius * 0.35),
            center + Offset(math.cos(a), math.sin(a)) * (radius * 1.28),
            Paint()
              ..color = colors.accent.withValues(alpha: 0.14)
              ..strokeWidth = 1,
          );
        }
      case IcBrainShaderMode.volumetricGlass:
        final ringPaint = Paint()
          ..color = colors.borderHairline.withValues(
            alpha: colors.isDark ? 0.35 : 0.5,
          )
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1;
        for (final ringScale in [0.55, 0.95, 1.32]) {
          canvas.drawOval(
            Rect.fromCenter(
              center: center,
              width: radius * 2.2 * ringScale,
              height: radius * 0.78 * ringScale * (0.65 + pitch.abs() * 0.35),
            ),
            ringPaint,
          );
        }
    }
  }

  void _paintNodeByShaderMode(
    Canvas canvas,
    _ProjectedNode p,
    double baseRadius,
    double depthAlpha,
    Color toneColor, {
    required bool isSelected,
    required bool isHovered,
  }) {
    if (isSelected || isHovered || p.node.kind == BrainNodeKind.assistant) {
      canvas.drawCircle(
        p.screen,
        baseRadius * (isSelected ? 2.1 : 1.65),
        Paint()
          ..color = toneColor.withValues(
            alpha: (isSelected ? 0.32 : 0.18) * depthAlpha,
          )
          ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 14),
      );
    }

    switch (shaderMode) {
      case IcBrainShaderMode.wireframeHud:
        // Wireframe cage sphere (concentric + latitude/longitude arcs)
        final wirePaint = Paint()
          ..color = toneColor.withValues(alpha: 0.88 * depthAlpha)
          ..style = PaintingStyle.stroke
          ..strokeWidth = isSelected ? 1.8 : 1.2;
        canvas.drawCircle(
          p.screen,
          baseRadius,
          Paint()
            ..color = colors.surfacePage.withValues(alpha: 0.78 * depthAlpha),
        );
        canvas.drawCircle(p.screen, baseRadius, wirePaint);
        canvas.drawOval(
          Rect.fromCenter(
            center: p.screen,
            width: baseRadius * 2,
            height: baseRadius * 0.85,
          ),
          wirePaint,
        );
        canvas.drawOval(
          Rect.fromCenter(
            center: p.screen,
            width: baseRadius * 0.85,
            height: baseRadius * 2,
          ),
          wirePaint,
        );
        canvas.drawCircle(
          p.screen,
          3.2 * p.scale,
          Paint()..color = toneColor.withValues(alpha: depthAlpha),
        );
      case IcBrainShaderMode.architecturalBlueprint:
        // Technical drafting node: double ring + crosshair ticks
        canvas.drawCircle(
          p.screen,
          baseRadius,
          Paint()
            ..color = colors.surfaceCard.withValues(alpha: 0.92 * depthAlpha),
        );
        canvas.drawCircle(
          p.screen,
          baseRadius,
          Paint()
            ..color = toneColor.withValues(alpha: 0.9 * depthAlpha)
            ..style = PaintingStyle.stroke
            ..strokeWidth = isSelected ? 2.2 : 1.5,
        );
        canvas.drawCircle(
          p.screen,
          baseRadius * 0.62,
          Paint()
            ..color = toneColor.withValues(alpha: 0.45 * depthAlpha)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.0,
        );
        canvas.drawCircle(
          p.screen,
          3.0 * p.scale,
          Paint()..color = toneColor.withValues(alpha: depthAlpha),
        );
      case IcBrainShaderMode.starMapConstellation:
        // Glowing star core + 4-point diffraction spikes + orbital ring
        canvas.drawCircle(
          p.screen,
          baseRadius,
          Paint()
            ..color = toneColor.withValues(alpha: 0.35 * depthAlpha)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.0,
        );
        canvas.drawCircle(
          p.screen,
          baseRadius * 0.55,
          Paint()..color = toneColor.withValues(alpha: 0.88 * depthAlpha),
        );
        final spikePaint = Paint()
          ..color = colors.surfaceInk.withValues(alpha: 0.85 * depthAlpha)
          ..strokeWidth = 1.2;
        canvas.drawLine(
          p.screen - Offset(baseRadius * 0.9, 0),
          p.screen + Offset(baseRadius * 0.9, 0),
          spikePaint,
        );
        canvas.drawLine(
          p.screen - Offset(0, baseRadius * 0.9),
          p.screen + Offset(0, baseRadius * 0.9),
          spikePaint,
        );
      case IcBrainShaderMode.volumetricGlass:
        // Volumetric 3D sphere with radial specular shader
        final sphereRect = Rect.fromCircle(
          center: p.screen,
          radius: baseRadius,
        );
        final spherePaint = Paint()
          ..shader = RadialGradient(
            center: const Alignment(-0.38, -0.38),
            radius: 0.95,
            colors: [
              Color.lerp(toneColor, const Color(0xFFFFFFFF), 0.55)!.withValues(
                alpha: depthAlpha,
              ),
              toneColor.withValues(alpha: 0.92 * depthAlpha),
              Color.lerp(toneColor, const Color(0xFF000000), 0.55)!.withValues(
                alpha: depthAlpha,
              ),
            ],
            stops: const [0.0, 0.52, 1.0],
          ).createShader(sphereRect);
        canvas.drawCircle(p.screen, baseRadius, spherePaint);

        // Specular rim border
        canvas.drawCircle(
          p.screen,
          baseRadius,
          Paint()
            ..color = (isSelected ? colors.surfaceInk : colors.glassHighlight)
                .withValues(alpha: (isSelected ? 0.9 : 0.55) * depthAlpha)
            ..style = PaintingStyle.stroke
            ..strokeWidth = isSelected ? 2.2 : 1.1,
        );
    }
  }

  @override
  bool shouldRepaint(covariant _BrainScenePainter oldDelegate) =>
      oldDelegate.yaw != yaw ||
      oldDelegate.pitch != pitch ||
      oldDelegate.zoom != zoom ||
      oldDelegate.pulsePhase != pulsePhase ||
      oldDelegate.selectedId != selectedId ||
      oldDelegate.hoveredId != hoveredId ||
      oldDelegate.colors != colors ||
      oldDelegate.shaderMode != shaderMode ||
      oldDelegate.nodes != nodes ||
      oldDelegate.synapses != synapses;
}

const List<BrainNode3D> defaultBrainNodes = BrainScene3D.defaultCluster;
const List<BrainSynapse3D> defaultBrainSynapses = BrainScene3D.defaultSynapses;
