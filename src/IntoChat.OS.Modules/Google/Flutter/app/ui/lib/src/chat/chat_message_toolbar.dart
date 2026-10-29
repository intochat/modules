import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../components/kit/kit_toast.dart';
import '../components/kit/ui_kit_icon_button.dart';
import '../theme/ui_colors.dart';
import '../theme/ui_theme.dart';
import 'chat_copy.dart';

/// Figma `AssistantToolbar`: the answering model's label on the left, then
/// copy and (on the last answer only) retry.
final class ChatMessageToolbar extends StatelessWidget {
  const ChatMessageToolbar({
    super.key,
    required this.text,
    this.model,
    this.onRetry,
  });

  final String text;
  final String? model;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final label = model?.trim() ?? '';
    return SizedBox(
      height: 28,
      child: Row(
        children: [
          Expanded(
            child: Align(
              alignment: Alignment.centerLeft,
              child: label.isEmpty
                  ? null
                  : Semantics(
                      label: '${ChatCopy.answeredBy} $label',
                      excludeSemantics: true,
                      child: Text(
                        label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: UiType.of(context).caption
                            .copyWith(color: c.muted, fontFamily: UiType.mono),
                      ),
                    ),
            ),
          ),
          if (text.isNotEmpty)
            UiKitIconButton(
              icon: 'copy',
              label: ChatCopy.copy,
              onPressed: () async {
                await Clipboard.setData(ClipboardData(text: text));
                if (context.mounted) showKitToast(context, ChatCopy.copied);
              },
            ),
          if (onRetry != null) ...[
            const SizedBox(width: 4),
            UiKitIconButton(
              icon: 'refresh',
              label: ChatCopy.retry,
              onPressed: onRetry,
            ),
          ],
        ],
      ),
    );
  }
}
