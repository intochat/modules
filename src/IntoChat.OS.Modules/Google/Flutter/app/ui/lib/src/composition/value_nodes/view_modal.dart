import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../components/kit/ui_kit_surface.dart';
import '../../theme/ui_colors.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';

/// Modal value node, shown while it is in the tree. Escape or the backdrop
/// triggers the first Secondary or Ghost Button child (the cancel button);
/// without one they do nothing.
final class ViewModal extends StatefulWidget {
  const ViewModal({
    super.key,
    required this.node,
    required this.children,
    required this.action,
  });
  final Map node;

  /// Child maps paired with their rendered widgets.
  final List<(Map, Widget)> children;
  final VoidCallback? Function(Map node) action;

  @override
  State<ViewModal> createState() => _ViewModalState();
}

class _ViewModalState extends State<ViewModal> {
  final _portal = OverlayPortalController();
  // Focus held before the modal opened; restored when it leaves the tree.
  FocusNode? _previous;
  bool _cancelled = false;
  final _scope = FocusScopeNode(debugLabel: 'view modal');

  @override
  void initState() {
    super.initState();
    _previous = FocusManager.instance.primaryFocus;
    _portal.show();
    // Autofocus yields to existing focus, so take it explicitly.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _scope.requestFocus();
    });
  }

  @override
  void dispose() {
    final previous = _previous;
    if (previous != null && previous.context != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (previous.context != null) previous.requestFocus();
      });
    }
    _scope.dispose();
    super.dispose();
  }

  /// One-shot per mounted modal: repeated Escape or backdrop taps do not
  /// dispatch the cancel action twice.
  void _cancel() {
    if (_cancelled) return;
    for (final (child, _) in widget.children) {
      if (child['kind'] == 1 && (child['style'] == 1 || child['style'] == 2)) {
        final cancel = widget.action(child);
        if (cancel == null) return;
        _cancelled = true;
        cancel();
        return;
      }
    }
  }

  @override
  Widget build(BuildContext context) => OverlayPortal(
    controller: _portal,
    overlayChildBuilder: _overlay,
    child: const SizedBox.shrink(),
  );

  Widget _overlay(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final title = widget.node['text'] as String? ?? '';
    final detail = widget.node['detail'] as String? ?? '';
    final buttons = [
      for (final (m, w) in widget.children)
        if (m['kind'] == 1) w,
    ];
    final body = [
      for (final (m, w) in widget.children)
        if (m['kind'] != 1) w,
    ];
    return CallbackShortcuts(
      bindings: {const SingleActivator(LogicalKeyboardKey.escape): _cancel},
      child: FocusScope(
        node: _scope,
        autofocus: true,
        child: Stack(
          children: [
            Positioned.fill(
              child: GestureDetector(
                onTap: _cancel,
                // Figma backdrop is a dark scrim in both themes (rgba 23,20,15,.55);
                // `strong` would turn it light in dark mode.
                child: ColoredBox(
                  color: uiKitShadowBase(context).withValues(alpha: .55),
                ),
              ),
            ),
            Center(
              child: Material(
                type: MaterialType.transparency,
                child: Container(
                  width: double.infinity,
                  constraints: const BoxConstraints(maxWidth: 440),
                  margin: const EdgeInsets.all(24),
                  padding: const EdgeInsets.all(32),
                  decoration: BoxDecoration(
                    color: c.surface,
                    borderRadius: BorderRadius.circular(
                      UiTokens.of(context).modalRadius,
                    ),
                    border: Border.all(color: c.hairline),
                    boxShadow: UiShadows.xl(uiKitShadowBase(context)),
                  ),
                  child: Semantics(
                    scopesRoute: true,
                    namesRoute: true,
                    explicitChildNodes: true,
                    label: title,
                    // The content scrolls in a short viewport; the buttons
                    // stay pinned below it so cancel is always reachable.
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Flexible(
                          child: SingleChildScrollView(
                            primary: false,
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                if (title.isNotEmpty)
                                  Text(
                                    title,
                                    style: type.heading,
                                    textAlign: TextAlign.center,
                                  ),
                                if (detail.isNotEmpty) ...[
                                  const SizedBox(height: 8),
                                  Text(
                                    detail,
                                    style: type.bodyMuted,
                                    textAlign: TextAlign.center,
                                  ),
                                ],
                                for (final w in body) ...[
                                  const SizedBox(height: 16),
                                  w,
                                ],
                              ],
                            ),
                          ),
                        ),
                        if (buttons.isNotEmpty) ...[
                          const SizedBox(height: 24),
                          // Figma: action buttons are full width.
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              for (var i = 0; i < buttons.length; i++) ...[
                                if (i > 0) const SizedBox(height: 10),
                                buttons[i],
                              ],
                            ],
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
