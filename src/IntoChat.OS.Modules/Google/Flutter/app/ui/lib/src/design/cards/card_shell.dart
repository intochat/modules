import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';

/// The app a card comes from, shown small at the top of the card.
class CardSource {
  const CardSource(this.name, this.icon);

  final String name;
  final Widget icon;
}

/// Alias for [CardShell].
typedef CardFrame = CardShell;

/// Frame shared by every inline card: radius 14, padding 18, hairline, shadow sm.
/// With keyboard focus: accent edge and ring. Docked in place of the composer
/// (see [PromptScope]): the composer's width and shadow.
class CardShell extends StatelessWidget {
  const CardShell({
    super.key,
    required this.source,
    required this.child,
    this.status,
    this.width = 560,
  });

  final CardSource source;
  final Widget child;

  /// Optional chip on the right of the source line.
  final Widget? status;
  final double width;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final focused = PromptKeys.maybeOf(context)?.focused ?? false;
    final docked = PromptScope.maybeOf(context)?.docked ?? false;
    return ConstrainedBox(
      constraints: BoxConstraints(maxWidth: docked ? double.infinity : width),
      child: AnimatedContainer(
        duration: IcMotion.fast,
        padding: EdgeInsets.all(focused ? 17.5 : 18),
        decoration: BoxDecoration(
          color: c.surfaceCard,
          borderRadius: BorderRadius.circular(docked ? IcRadius.panel : IcRadius.card),
          border: Border.all(
            color: focused ? c.accent : (docked ? c.borderInput : c.borderHairline),
            width: focused ? 1.5 : 1,
          ),
          boxShadow: [
            if (focused) BoxShadow(color: c.ring, spreadRadius: docked ? 3 : 2),
            ...(docked ? IcShadow.lg(c) : IcShadow.sm(c)),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                SizedBox(width: 16, height: 16, child: source.icon),
                const SizedBox(width: 8),
                Text(
                  source.name,
                  style: IcType.text(12, weight: FontWeight.w600, color: c.textMuted),
                ),
                const Spacer(),
                ?status,
              ],
            ),
            const SizedBox(height: 12),
            child,
          ],
        ),
      ),
    );
  }
}

/// Empty state inside a card.
class CardEmpty extends StatelessWidget {
  const CardEmpty({
    super.key,
    required this.text,
    this.icon = LucideIcons.inbox,
  });

  final String text;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 12),
      child: Row(
        children: [
          Icon(icon, size: 16, color: c.textMuted),
          const SizedBox(width: 10),
          Expanded(
            child: Text(text, style: IcType.text(14, color: c.textBody)),
          ),
        ],
      ),
    );
  }
}

/// Small status pill.
class CardChip extends StatelessWidget {
  const CardChip({
    super.key,
    required this.label,
    required this.fg,
    required this.bg,
    this.icon,
  });

  final String label;
  final Color fg;
  final Color bg;
  final Widget? icon;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
    decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(IcRadius.pill)),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (icon != null) ...[icon!, const SizedBox(width: 5)],
        Text(
          label,
          style: IcType.text(12, weight: FontWeight.w600, color: fg),
        ),
      ],
    ),
  );
}

enum ResultTone { done, rejected, error, neutral }

/// An answered card collapses to one line. Click it to see what was decided.
class CardResult extends StatefulWidget {
  const CardResult({
    super.key,
    required this.source,
    required this.text,
    required this.tone,
    this.detail,
    this.expanded,
    this.width = 560,
  });

  final CardSource source;
  final String text;
  final String? detail;
  final ResultTone tone;

  /// Read-only view of the card, shown when the line is opened.
  final Widget? expanded;
  final double width;

  @override
  State<CardResult> createState() => _CardResultState();
}

class _CardResultState extends State<CardResult> {
  bool _open = false;
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final (icon, fg, bg) = switch (widget.tone) {
      ResultTone.done => (LucideIcons.check, c.done, c.doneBg),
      ResultTone.rejected => (LucideIcons.x, c.textMuted, c.surfaceSunk),
      ResultTone.error => (LucideIcons.triangleAlert, c.error, c.errorBg),
      ResultTone.neutral => (LucideIcons.circleCheck, c.textMuted, c.surfaceSunk),
    };
    final canOpen = widget.expanded != null;
    return ConstrainedBox(
      constraints: BoxConstraints(maxWidth: widget.width),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Stop(
            onActivate: canOpen ? () => setState(() => _open = !_open) : null,
            builder: (context, v) => SelectionRing(
              on: v.selected,
              radius: IcRadius.input,
              child: MouseRegion(
                cursor: canOpen ? SystemMouseCursors.click : MouseCursor.defer,
                onEnter: (_) => setState(() => _hover = true),
                onExit: (_) => setState(() => _hover = false),
                child: GestureDetector(
                  onTap: canOpen ? () => setState(() => _open = !_open) : null,
                  child: AnimatedContainer(
                    duration: IcMotion.fast,
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                    decoration: BoxDecoration(
                      color: c.surfaceCard,
                      borderRadius: BorderRadius.circular(IcRadius.input),
                      border: Border.all(color: _hover && canOpen ? c.borderInput : c.borderHairline),
                    ),
                    child: Row(
                      children: [
                        Container(
                          width: 22,
                          height: 22,
                          decoration: BoxDecoration(color: bg, shape: BoxShape.circle),
                          child: Icon(icon, size: 13, color: fg),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text.rich(
                            TextSpan(
                              children: [
                                TextSpan(
                                  text: widget.text,
                                  style: IcType.text(13, weight: FontWeight.w600, color: c.textStrong),
                                ),
                                if (widget.detail != null)
                                  TextSpan(
                                    text: '  ·  ${widget.detail}',
                                    style: IcType.text(13, color: c.textMuted),
                                  ),
                              ],
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: 8),
                        SizedBox(width: 14, height: 14, child: widget.source.icon),
                        if (canOpen) ...[
                          const SizedBox(width: 8),
                          Icon(_open ? LucideIcons.chevronUp : LucideIcons.chevronDown, size: 14, color: c.textMuted),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
          if (_open && widget.expanded != null) ...[const SizedBox(height: 8), widget.expanded!],
        ],
      ),
    );
  }
}

/// A line of text with a spinner, for "saving" and "loading" states.
class CardWorking extends StatelessWidget {
  const CardWorking({super.key, required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Row(
      children: [
        WorkingSpinner(size: 15, color: c.textStrong),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            text,
            style: IcType.text(13, weight: FontWeight.w500, color: c.textBody),
          ),
        ),
      ],
    );
  }
}

/// Error message with an optional Retry button.
class CardError extends StatelessWidget {
  const CardError({super.key, required this.text, this.onRetry, this.retryLabel = 'Try again'});

  final String text;
  final VoidCallback? onRetry;
  final String retryLabel;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: c.errorBg, borderRadius: BorderRadius.circular(IcRadius.button)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.only(top: 1),
            child: Icon(LucideIcons.triangleAlert, size: 15, color: c.error),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(text, style: IcType.text(13, color: c.textBody, height: 1.45)),
          ),
          if (onRetry != null) ...[const SizedBox(width: 10), Btn(label: retryLabel, small: true, onTap: onRetry)],
        ],
      ),
    );
  }
}

/// Grey bars standing in for text while a card loads.
class Skeleton extends StatefulWidget {
  const Skeleton({super.key, required this.width, this.height = 12});

  final double width;
  final double height;

  @override
  State<Skeleton> createState() => _SkeletonState();
}

class _SkeletonState extends State<Skeleton> with SingleTickerProviderStateMixin {
  late final AnimationController _a = AnimationController(vsync: this, duration: const Duration(milliseconds: 1100))
    ..repeat(reverse: true);

  @override
  void dispose() {
    _a.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final box = Container(
      width: widget.width,
      height: widget.height,
      decoration: BoxDecoration(color: c.surfaceSunk, borderRadius: BorderRadius.circular(IcRadius.xs)),
    );
    if (MediaQuery.disableAnimationsOf(context)) return box;
    return FadeTransition(opacity: Tween(begin: 0.55, end: 1.0).animate(_a), child: box);
  }
}
