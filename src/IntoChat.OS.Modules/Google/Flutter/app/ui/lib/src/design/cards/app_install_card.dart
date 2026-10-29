import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum AppInstallState { pending, installed, dismissed, alreadyInstalled, notFound }

class AppInstallData {
  const AppInstallData({
    required this.name,
    required this.handle,
    required this.publisher,
    required this.version,
    required this.summary,
    required this.icon,
    this.worksThrough = const [],
    this.link = '',
  });

  final String name;

  /// `owner/app`.
  final String handle;
  final String publisher;
  final String version;
  final String summary;
  final Widget icon;

  /// Connections it needs: a small tile and the service name.
  final List<(Widget, String)> worksThrough;

  /// The pasted link, shown when it leads nowhere.
  final String link;
}

/// App install: you pasted an app link into a session. Ino shows what the app
/// is; installing always goes through the review in Apps, so you see what it
/// may do before anything is installed.
class AppInstallCard extends StatelessWidget {
  const AppInstallCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onReview,
    this.onDismiss,
    this.onOpen,
  });

  final CardSource source;
  final AppInstallData data;
  final AppInstallState state;
  final VoidCallback? onReview;
  final VoidCallback? onDismiss;

  /// Opens the app page (installed or already installed).
  final VoidCallback? onOpen;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = data;
    switch (state) {
      case AppInstallState.installed:
        return CardResult(
          source: source,
          tone: ResultTone.done,
          text: '${d.name} installed',
          detail: '${d.handle} ${d.version}',
        );
      case AppInstallState.dismissed:
        return CardResult(source: source, tone: ResultTone.neutral, text: 'Not installed', detail: d.name);
      case AppInstallState.notFound:
        return CardShell(
          source: source,
          status: CardChip(
            label: 'No app here',
            fg: c.error,
            bg: c.errorBg,
            icon: Icon(LucideIcons.triangleAlert, size: 13, color: c.error),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                'That link doesn\'t point to an app',
                style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
              ),
              const SizedBox(height: 4),
              Text(
                d.link,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: IcType.text(13, color: c.textMuted),
              ),
              const SizedBox(height: 8),
              Text(
                'It may be mistyped, or the app was removed. Ask whoever shared it for a new link.',
                style: IcType.text(14, color: c.textBody, height: 1.4),
              ),
            ],
          ),
        );
      case AppInstallState.pending:
      case AppInstallState.alreadyInstalled:
        final installed = state == AppInstallState.alreadyInstalled;
        return CardShell(
          source: source,
          status: installed
              ? CardChip(
                  label: 'Installed',
                  fg: c.done,
                  bg: c.doneBg,
                  icon: Icon(LucideIcons.check, size: 13, color: c.done),
                )
              : CardChip(
                  label: 'Needs your answer',
                  fg: c.accent,
                  bg: c.accentTint,
                  icon: Icon(LucideIcons.hand, size: 13, color: c.accent),
                ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                children: [
                  SizedBox(width: 44, height: 44, child: d.icon),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          d.name,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          '${d.handle}  ·  ${d.version}  ·  Made by ${d.publisher}',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: IcType.text(12.5, color: c.textMuted, tnum: true),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Text(d.summary, style: IcType.text(14, color: c.textBody, height: 1.4)),
              if (d.worksThrough.isNotEmpty && !installed) ...[
                const SizedBox(height: 12),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(
                      'Works through',
                      style: IcType.text(12.5, weight: FontWeight.w600, color: c.textMuted),
                    ),
                    for (final (icon, name) in d.worksThrough)
                      Container(
                        padding: const EdgeInsets.fromLTRB(4, 3, 8, 3),
                        decoration: BoxDecoration(
                          color: c.surfaceSunk,
                          borderRadius: BorderRadius.circular(IcRadius.xs),
                          border: Border.all(color: c.borderHairline),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            SizedBox(width: 16, height: 16, child: icon),
                            const SizedBox(width: 6),
                            Text(
                              name,
                            style: IcType.text(12, weight: FontWeight.w600, color: c.textBody),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              ],
              const SizedBox(height: 14),
              if (installed)
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        'You already have it. Its settings are on its app page.',
                        style: IcType.text(13, color: c.textMuted),
                      ),
                    ),
                    Btn(label: 'Open app page', onTap: onOpen),
                  ],
                )
              else
                PromptOptions(
                  options: [
                    PromptOption(
                      'Review and install',
                      detail: 'You see what it may do before anything is installed',
                      onTap: onReview,
                    ),
                    PromptOption('Not now', onTap: onDismiss),
                  ],
                ),
            ],
          ),
        );
    }
  }
}
