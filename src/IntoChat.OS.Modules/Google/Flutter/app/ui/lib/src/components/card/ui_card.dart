import 'package:flutter/material.dart';

import '../../models/ui_part.dart';
import '../../composition/neuron_view.dart';
import '../kit/ui_kit_surface.dart';
import '../../theme/ui_theme.dart';

final class UiCard extends StatelessWidget {
  const UiCard({super.key, required this.part, this.loadChild});

  final UiCardPart part;
  final NeuronLoader? loadChild;

  @override
  Widget build(BuildContext context) {
    final type = UiType.of(context);
    return UiKitSurface(
      key: Key('ui_card_${part.title}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (part.title.isNotEmpty) ...[
            Text(part.title, style: type.title),
            const SizedBox(height: 8),
          ],
          if (part.body.isNotEmpty) Text(part.body, style: type.body),
          for (final child in part.children)
            SizedBox(
              height: 200,
              child: loadChild == null
                  ? const Text('This component is not connected.')
                  : NeuronView(
                      kind: child.kind,
                      name: child.name,
                      load: loadChild!,
                    ),
            ),
          for (final field in part.fields) ...[
            const SizedBox(height: 8),
            Text(field.label, style: type.meta),
            Text(field.value, style: type.body),
          ],
        ],
      ),
    );
  }
}
