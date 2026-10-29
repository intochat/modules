import 'package:flutter/material.dart';

import '../../models/ui_part.dart';
import '../kit/ui_kit_button.dart';

/// Product button control. Used on surfaces and inside chat CustomMessage rows.
final class UiButton extends StatelessWidget {
  const UiButton({
    super.key,
    required this.part,
    this.onPressed,
    this.dense = false,
  });

  final UiButtonPart part;
  final ValueChanged<UiButtonPart>? onPressed;
  final bool dense;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: UiKitButton(
        key: Key('ui_button_${part.buttonId}'),
        label: part.label,
        compact: dense,
        onPressed: onPressed == null ? null : () => onPressed!(part),
      ),
    );
  }
}
