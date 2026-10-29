import 'package:flutter/material.dart';

import '../components/kit/ui_kit_button.dart';
import '../components/kit/ui_kit_surface.dart';
import '../composition/value_nodes/view_badge.dart';
import '../theme/ui_colors.dart';
import '../theme/ui_icon.dart';
import '../theme/ui_shadows.dart';
import '../theme/ui_theme.dart';
import '../theme/ui_tokens.dart';
import 'chat_copy.dart';

/// An app in the transcript (Figma `.file-card` shape): icon, title, Open.
/// Open dispatches the shell's workspace action with the card's opaque input.
/// An app that is not installed shows a badge, and Install when its publisher is known: Install only opens the
/// Apps detail, where the person consents (chat never installs).
final class ChatAppCard extends StatelessWidget {
  const ChatAppCard({super.key, required this.card, this.onOpen});

  final Map card;
  final void Function(Map<String, dynamic> action)? onOpen;

  /// The workspace activation the shell already understands.
  static Map<String, dynamic> openAction(Map card) => {
    'kind': 'workspace',
    'application': '${card['app'] ?? ''}',
    'operation': 'open',
    'input': '${card['input'] ?? ''}',
  };

  /// Opens the app's Apps detail (the one consented install path); null without a publisher.
  static Map<String, dynamic>? installAction(Map card) {
    final publisher = '${card['publisher'] ?? ''}';
    final app = '${card['app'] ?? ''}';
    if (publisher.isEmpty || app.isEmpty) return null;
    final release = '${card['release'] ?? ''}';
    return {
      'kind': 'workspace',
      'application': 'apps',
      'operation': 'open',
      'input': 'app:$publisher/$app${release.isEmpty ? '' : '@$release'}',
    };
  }

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final app = '${card['app'] ?? ''}';
    final title = '${card['title'] ?? ''}'.trim();
    final installed = card['installed'] != false;
    final install = installed ? null : installAction(card);
    Widget notInstalled() {
      const badge = ViewBadge(text: ChatCopy.notInstalled, tone: 0);
      if (install == null) return badge;
      return Wrap(
        spacing: 8,
        runSpacing: 8,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          badge,
          UiKitButton(
            label: ChatCopy.install,
            compact: true,
            style: UiKitButtonStyle.primary,
            onPressed: onOpen == null ? null : () => onOpen!(install),
          ),
        ],
      );
    }

    final glyph = uiIcon('${card['icon'] ?? ''}') ?? uiIcon('grid');
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: c.assistant,
        borderRadius: BorderRadius.circular(tokens.panelRadius),
        border: Border.all(color: c.assistantBorder),
        boxShadow: UiShadows.sm(uiKitShadowBase(context)),
      ),
      // Narrow transcripts stack the action under the name.
      child: LayoutBuilder(
        builder: (context, constraints) {
          final narrow = constraints.maxWidth < 280;
          final icon = Container(
            width: 40,
            height: 40,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: c.doneBg,
              borderRadius: BorderRadius.circular(tokens.radiusSmall),
            ),
            child: Icon(glyph, size: uiIconSize, color: c.done),
          );
          final name = Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              // Plain text: a manifest title is never interpreted as markup.
              Text(
                title.isEmpty ? app : title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: UiType.of(context).rowTitle.copyWith(color: c.strong),
              ),
              Text(
                app,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: UiType.of(context).caption
                    .copyWith(color: c.muted, fontFamily: UiType.mono),
              ),
            ],
          );
          final action = installed
              ? UiKitButton(
                  label: ChatCopy.open,
                  icon: 'external',
                  compact: true,
                  style: UiKitButtonStyle.secondary,
                  onPressed: onOpen == null
                      ? null
                      : () => onOpen!(openAction(card)),
                )
              : notInstalled();
          if (narrow) {
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                name,
                const SizedBox(height: 10),
                Flexible(child: action),
              ],
            );
          }
          return Row(
            children: [
              icon,
              const SizedBox(width: 12),
              Expanded(child: name),
              const SizedBox(width: 12),
              action,
            ],
          );
        },
      ),
    );
  }
}
