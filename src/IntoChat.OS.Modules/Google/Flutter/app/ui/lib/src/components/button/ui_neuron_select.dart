import 'package:flutter/material.dart';

import '../kit/ui_kit_select.dart';
import 'ui_neuron_button.dart';

/// A generic choice menu. The owner supplies its options and their order.
/// Without a [visual] it renders as a full-width [UiKitSelect]; options that
/// carry a `visual` keep their button rows.
class UiNeuronSelect extends StatelessWidget {
  const UiNeuronSelect({
    super.key,
    required this.label,
    required this.options,
    this.selected,
    this.visual,
    this.onSelected,
  });
  final String label;
  final List<Map> options;
  final String? selected;
  final Map? visual;
  final ValueChanged<String>? onSelected;

  @override
  Widget build(BuildContext context) {
    final byId = {for (final o in options) '${o['id']}': o};
    return UiKitSelect<String>(
      value: selected,
      hint: label,
      onChanged: onSelected,
      items: [
        for (final o in options)
          UiKitSelectItem(
            '${o['id']}',
            '${o['label']}',
            enabled: o['enabled'] != false,
          ),
      ],
      itemBuilder: (context, item, width) {
        final option = byId[item.value];
        if (option?['visual'] is! Map) {
          return Text(item.label);
        }
        return SizedBox(
          width: width,
          child: UiNeuronButton(
            label: item.label,
            visual: {...option!['visual'] as Map, 'compact': false},
            menuChild: true,
          ),
        );
      },
      triggerBuilder: visual == null
          ? null
          : (context, controller) => Tooltip(
              message: label,
              child: TextButton(
                onPressed: onSelected == null
                    ? null
                    : () => controller.isOpen
                          ? controller.close()
                          : controller.open(),
                style: TextButton.styleFrom(padding: EdgeInsets.zero),
                child: UiNeuronButton(
                  label: label,
                  visual: visual!,
                  menuChild: true,
                ),
              ),
            ),
    );
  }
}
