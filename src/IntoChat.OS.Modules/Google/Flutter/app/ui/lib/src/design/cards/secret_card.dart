import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum SecretState { pending, entering, saved, skipped }

class SecretRequestData {
  const SecretRequestData({required this.name, required this.why, this.savedIn = 'Variables and secrets'});

  /// What Ino needs, e.g. "Sveve API key".
  final String name;

  /// One line on what it is for.
  final String why;

  /// Where it lands: "Variables and secrets", or "API keys" for a model key.
  final String savedIn;
}

/// Credential request: Ino needs a password, token or key. You never type it
/// into the session: the button opens a secure window, and the value goes
/// straight to Settings. The session only ever says that it was saved.
class SecretCard extends StatelessWidget {
  const SecretCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onEnter,
    this.onSkip,
  });

  final CardSource source;
  final SecretRequestData data;
  final SecretState state;
  final VoidCallback? onEnter;
  final VoidCallback? onSkip;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = data;
    switch (state) {
      case SecretState.saved:
        return CardResult(
          source: source,
          tone: ResultTone.done,
          text: '${d.name} saved',
          detail: 'in ${d.savedIn}, never in this session',
        );
      case SecretState.skipped:
        return CardResult(source: source, tone: ResultTone.neutral, text: 'You skipped the ${d.name}');
      case SecretState.pending:
      case SecretState.entering:
        final entering = state == SecretState.entering;
        return CardShell(
          source: source,
          status: entering
              ? CardChip(
                  label: 'In the secure window',
                  fg: c.working,
                  bg: c.workingBg,
                  icon: const WorkingSpinner(size: 12),
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
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 36,
                    height: 36,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(color: c.surfaceInk, borderRadius: BorderRadius.circular(IcRadius.sm)),
                    child: Icon(LucideIcons.keyRound, size: 18, color: c.isDark ? c.textStrong : c.surfacePage),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'I need your ${d.name}',
                          style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
                        ),
                        const SizedBox(height: 3),
                        Text(d.why, style: IcType.text(14, color: c.textBody, height: 1.4)),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Icon(LucideIcons.lock, size: 13, color: c.textMuted),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      'You type it in a secure window. It goes to ${d.savedIn} and never into this session.',
                      style: IcType.text(12.5, color: c.textMuted, height: 1.4),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 14),
              PromptOptions(
                options: [
                  PromptOption(
                    entering ? 'Window is open' : 'Enter it securely',
                    icon: LucideIcons.lock,
                    detail: entering ? 'Bring the secure window back' : 'Opens a secure window',
                    onTap: onEnter,
                  ),
                  PromptOption('Skip', onTap: entering ? null : onSkip),
                ],
              ),
            ],
          ),
        );
    }
  }
}
