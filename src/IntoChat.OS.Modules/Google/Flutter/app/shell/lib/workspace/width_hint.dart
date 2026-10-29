import 'package:flutter/widgets.dart';

/// The desktop lays out one window below this many logical pixels.
const narrowBelow = 900.0;

/// Reports whether the shell's window is narrower than [threshold] logical pixels:
/// once when first shown, then only when the width crosses it.
class WidthHint extends StatefulWidget {
  const WidthHint({
    super.key,
    required this.onNarrow,
    required this.child,
    this.threshold = narrowBelow,
  });

  final ValueChanged<bool> onNarrow;
  final Widget child;
  final double threshold;

  @override
  State<WidthHint> createState() => _WidthHintState();
}

class _WidthHintState extends State<WidthHint> {
  bool? _narrow;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final narrow = MediaQuery.sizeOf(context).width < widget.threshold;
    if (narrow == _narrow) return;
    _narrow = narrow;
    widget.onNarrow(narrow);
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
