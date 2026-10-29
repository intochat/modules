import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';

/// List value node: lazily built rows, min height 40, hairline separators.
/// In an unbounded slot the list shrink-wraps up to [unboundedMaxHeight].
final class ViewList extends StatelessWidget {
  const ViewList({super.key, required this.count, required this.row});
  final int count;
  final Widget Function(int index) row;

  static const unboundedMaxHeight = 480.0;

  @override
  Widget build(BuildContext context) {
    final hairline = UiColors.of(context).hairline;
    Widget item(BuildContext context, int i) => Container(
      constraints: const BoxConstraints(minHeight: 40),
      alignment: AlignmentDirectional.centerStart,
      decoration: i == count - 1
          ? null
          : BoxDecoration(
              border: Border(bottom: BorderSide(color: hairline)),
            ),
      child: row(i),
    );
    return LayoutBuilder(
      builder: (context, constraints) => constraints.hasBoundedHeight
          ? ListView.builder(itemCount: count, itemBuilder: item)
          : ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: unboundedMaxHeight),
              child: ListView.builder(
                shrinkWrap: true,
                itemCount: count,
                itemBuilder: item,
              ),
            ),
    );
  }
}
