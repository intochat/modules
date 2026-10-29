import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

class ChoiceOption {
  const ChoiceOption(this.title, {this.detail, this.icon});

  final String title;
  final String? detail;
  final IconData? icon;
}

/// Choice: 2 to 5 options when the chat can't tell which one you mean, as a
/// numbered list that ends with "Chat about this".
class ChoiceCard extends StatelessWidget {
  const ChoiceCard({
    super.key,
    required this.source,
    required this.question,
    required this.options,
    this.picked,
    this.onPick,
  });

  final CardSource source;
  final String question;
  final List<ChoiceOption> options;

  /// Index of the answer; null while waiting.
  final int? picked;
  final ValueChanged<int>? onPick;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    if (picked != null && picked! >= 0 && picked! < options.length) {
      return CardResult(
        source: source,
        tone: ResultTone.neutral,
        text: 'You picked ${options[picked!].title}',
        detail: question,
      );
    }
    return CardShell(
      source: source,
      status: CardChip(
        label: 'Needs your answer',
        fg: c.accent,
        bg: c.accentTint,
        icon: Icon(LucideIcons.hand, size: 13, color: c.accent),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            question,
            style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong, height: 1.4),
          ),
          const SizedBox(height: 12),
          PromptOptions(
            options: [
              for (var i = 0; i < options.length; i++)
                PromptOption(
                  options[i].title,
                  detail: options[i].detail,
                  icon: options[i].icon,
                  onTap: onPick == null ? null : () => onPick!(i),
                ),
            ],
          ),
        ],
      ),
    );
  }
}
