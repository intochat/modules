import 'package:flutter/material.dart';
import 'package:flutter/physics.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../../theme/ui_tokens.dart';

// Semantic artwork and state come from the app; this control only renders them.
class UiNeuronButton extends StatefulWidget {
  const UiNeuronButton({
    super.key,
    required this.label,
    required this.visual,
    this.onPressed,
    this.menuChild = false,
  });
  final String label;
  final Map visual;
  final VoidCallback? onPressed;
  final bool menuChild;
  @override
  State<UiNeuronButton> createState() => _UiNeuronButtonState();
}

class _UiNeuronButtonState extends State<UiNeuronButton>
    with SingleTickerProviderStateMixin {
  late final AnimationController _scale = AnimationController.unbounded(
    vsync: this,
    value: 1,
  );
  bool _hover = false;
  void _press(bool pressed) {
    if (MediaQuery.disableAnimationsOf(context)) return;
    _scale.animateWith(
      SpringSimulation(
        const SpringDescription(mass: 1, stiffness: 650, damping: 51),
        _scale.value,
        pressed ? .94 : 1,
        _scale.velocity,
      ),
    );
  }

  @override
  void dispose() {
    _scale.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final tokens = UiTokens.of(context);
    final selected = widget.visual['selected'] == true;
    final compact = widget.visual['compact'] == true;
    final avatar = widget.visual['avatar'] == true;
    final accent = selected || widget.visual['tone'] == 'accent';
    final color = accent ? colors.primary : colors.onSurface;
    final svg = widget.visual['svg'] as String?;
    final url = widget.visual['imageUrl'] as String?;
    final initials = widget.label.trim().isEmpty
        ? '?'
        : widget.label.trim().characters.first.toUpperCase();
    final icon = switch (widget.visual['icon']) {
      'apps' => Icons.apps_rounded,
      'more' => Icons.more_horiz_rounded,
      'usage' => Icons.speed_rounded,
      'person' => Icons.person_outline_rounded,
      'close' => Icons.close_rounded,
      'settings' => Icons.settings_outlined,
      'refresh' => Icons.refresh_rounded,
      _ => Icons.widgets_outlined,
    };
    Widget artwork = Icon(icon, color: color, size: 23);
    if (svg != null && svg.isNotEmpty) {
      artwork = SvgPicture.string(
        svg,
        width: 25,
        height: 25,
        theme: SvgTheme(currentColor: color),
      );
    }
    if (avatar) {
      artwork = CircleAvatar(
        radius: 16,
        backgroundColor: colors.primaryContainer,
        foregroundColor: colors.onPrimaryContainer,
        child: url != null && url.isNotEmpty
            ? ClipOval(
                child: Image.network(
                  url,
                  width: 32,
                  height: 32,
                  fit: BoxFit.cover,
                  errorBuilder: (_, _, _) => Text(initials),
                ),
              )
            : Text(
                initials,
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                ),
              ),
      );
    }
    // Menu rows share an artwork column regardless of SVG bounds or label length.
    // Compact menu children are the trigger itself and keep the regular control.
    if (widget.menuChild && !compact) {
      return ConstrainedBox(
        constraints: const BoxConstraints(minHeight: 40),
        child: Row(
          children: [
            SizedBox(width: 32, height: 32, child: Center(child: artwork)),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                widget.label,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 14, color: color),
              ),
            ),
          ],
        ),
      );
    }
    final duration = MediaQuery.disableAnimationsOf(context)
        ? Duration.zero
        : tokens.quick;
    Widget child = AnimatedContainer(
      duration: duration,
      constraints: const BoxConstraints(minHeight: 44, minWidth: 44),
      padding: EdgeInsets.symmetric(horizontal: compact ? 4 : 12, vertical: 6),
      decoration: BoxDecoration(
        color: selected
            ? colors.primaryContainer
            : _hover
            ? colors.surfaceContainerHighest
            : Colors.transparent,
        borderRadius: BorderRadius.circular(tokens.controlRadius + 2),
        border: Border.all(
          color: selected
              ? colors.primary.withValues(alpha: .35)
              : Colors.transparent,
        ),
      ),
      child: Stack(
        alignment: Alignment.center,
        clipBehavior: Clip.none,
        children: [
          Row(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              artwork,
              if (!compact) ...[
                const SizedBox(width: 8),
                Flexible(
                  child: Text(
                    widget.label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: color,
                    ),
                  ),
                ),
              ],
            ],
          ),
          if (widget.visual['running'] == true)
            Positioned(
              bottom: -5,
              child: AnimatedContainer(
                duration: duration,
                width: selected ? 14 : 4,
                height: 3,
                decoration: BoxDecoration(
                  color: selected ? colors.primary : colors.onSurfaceVariant,
                  borderRadius: BorderRadius.circular(3),
                ),
              ),
            ),
        ],
      ),
    );
    if (!widget.menuChild) {
      child = Semantics(
        button: true,
        selected: selected,
        label: widget.label,
        enabled: widget.onPressed != null,
        child: ExcludeSemantics(
          child: Material(
            color: Colors.transparent,
            child: InkWell(
              onTap: widget.onPressed,
              onHighlightChanged: _press,
              borderRadius: BorderRadius.circular(tokens.controlRadius + 2),
              child: child,
            ),
          ),
        ),
      );
    }
    return Tooltip(
      message: widget.label,
      excludeFromSemantics: true,
      child: MouseRegion(
        onEnter: (_) => setState(() => _hover = true),
        onExit: (_) => setState(() => _hover = false),
        child: ScaleTransition(
          scale: _scale,
          child: Opacity(
            opacity: widget.onPressed == null && !widget.menuChild ? .4 : 1,
            child: child,
          ),
        ),
      ),
    );
  }
}
