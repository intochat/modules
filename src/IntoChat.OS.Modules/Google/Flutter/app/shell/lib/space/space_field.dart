import 'dart:ui' show lerpDouble;

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';

import 'space_controller.dart';

class SpaceField extends StatefulWidget {
  const SpaceField({
    super.key,
    required this.view,
    required this.onInput,
    this.loadIcon,
  });
  final SpaceView view;
  final SceneInputHandler onInput;
  final SphereIconLoader? loadIcon;
  @override
  State<SpaceField> createState() => _SpaceFieldState();
}

class _SpaceFieldState extends State<SpaceField>
    with SingleTickerProviderStateMixin {
  late final AnimationController _flight = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 420),
    value: 1,
  );
  SceneState? _from;
  SceneState? _drawn;
  double? _fromOffset, _drawnOffset;
  @override
  void didUpdateWidget(SpaceField old) {
    super.didUpdateWidget(old);
    if (widget.view.scene.revision == old.view.scene.revision) return;
    if (_flight.isAnimating &&
        widget.view.focused == old.view.focused &&
        !widget.view.cut &&
        !MediaQuery.disableAnimationsOf(context)) {
      return;
    }
    _from = _drawn ?? old.view.scene;
    _fromOffset = _drawnOffset;
    if (widget.view.animate && !MediaQuery.disableAnimationsOf(context)) {
      _flight.forward(from: 0);
    } else {
      _flight.value = 1;
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) _flight.value = 1;
  }

  @override
  void dispose() {
    _flight.dispose();
    super.dispose();
  }

  SceneState _interpolate(double width) {
    final target = widget.view.scene;
    final previous = _from ?? target;
    final t = Curves.easeInOutCubic.transform(_flight.value);
    final old = {for (final node in previous.definition.nodes) node.id: node};
    double mix(double a, double b) => lerpDouble(a, b, t)!;
    final camera = target.definition.camera,
        before = previous.definition.camera;
    final x = mix(before.x, camera.x),
        y = mix(before.y, camera.y),
        zoom = mix(before.zoom, camera.zoom);
    final targetOffset = widget.view.main != null || widget.view.error != null
        ? width * .22 / zoom
        : 0.0;
    final offset = mix(_fromOffset ?? targetOffset, targetOffset);
    _drawnOffset = offset;
    final state = SceneState(
      name: target.name,
      revision: target.revision,
      definition: SceneDefinition(
        background: target.definition.background,
        camera: SceneCamera(x: x, y: y, zoom: zoom),
        nodes: [
          for (final node in target.definition.nodes)
            SceneNode(
              id: node.id,
              type: node.type,
              parameters: node.parameters,
              inputs: node.inputs,
              tree: node.tree,
              style: node.style,
              children: node.children,
              tweens: node.tweens,
              transform: SceneTransform(
                x: mix(
                  old[node.id]?.transform.x ?? node.transform.x,
                  node.transform.x,
                ),
                y: mix(
                  old[node.id]?.transform.y ?? node.transform.y,
                  node.transform.y,
                ),
                z: node.transform.z,
                scale: mix(
                  old[node.id]?.transform.scale ?? 1,
                  node.transform.scale,
                ),
                rotation: node.transform.rotation,
                opacity: node.transform.opacity,
              ),
            ),
        ],
      ),
    );
    _drawn = state;
    return SceneState(
      name: state.name,
      revision: state.revision,
      definition: SceneDefinition(
        background: state.definition.background,
        nodes: state.definition.nodes,
        camera: SceneCamera(x: x + offset, y: y, zoom: zoom),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) => AnimatedBuilder(
      animation: _flight,
      builder: (context, _) => SceneView(
        sceneKey: widget.view.scene.name,
        state: _interpolate(constraints.maxWidth),
        animateChanges: false,
        onInput: widget.onInput,
        loadIcon: widget.loadIcon,
      ),
    ),
  );
}
