import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';
import 'ui_kit_surface.dart';

/// One choice in a [UiKitSelect]. A disabled item is listed but cannot be
/// chosen; [detail] explains why or adds context under the label.
final class UiKitSelectItem<T> {
  const UiKitSelectItem(
    this.value,
    this.label, {
    this.enabled = true,
    this.detail,
  });

  final T value;
  final String label;
  final bool enabled;
  final String? detail;
}

/// Figma select: a full-width 48 high box, radius 12, `inputBorder`, with a
/// 3 px `focus` ring. A null [onChanged] disables it. Lists over eight
/// items get a search box.
///
/// [itemBuilder] replaces the default row (label and detail); it receives the
/// width available for the row. [triggerBuilder] replaces the box, for
/// callers that supply their own trigger.
final class UiKitSelect<T> extends StatefulWidget {
  const UiKitSelect({
    super.key,
    required this.value,
    required this.items,
    required this.onChanged,
    this.label,
    this.hint,
    this.itemBuilder,
    this.triggerBuilder,
  });

  final T? value;
  final List<UiKitSelectItem<T>> items;
  final ValueChanged<T>? onChanged;
  final String? label;
  final String? hint;
  final Widget Function(
    BuildContext context,
    UiKitSelectItem<T> item,
    double width,
  )?
  itemBuilder;
  final Widget Function(BuildContext context, MenuController menu)?
  triggerBuilder;

  @override
  State<UiKitSelect<T>> createState() => _UiKitSelectState<T>();
}

class _UiKitSelectState<T> extends State<UiKitSelect<T>> {
  final _menu = MenuController();
  final _search = TextEditingController();
  final _searchFocus = FocusNode();
  String _query = '';
  bool _focused = false;

  @override
  void dispose() {
    _search.dispose();
    _searchFocus.dispose();
    super.dispose();
  }

  void _choose(UiKitSelectItem<T> item) {
    if (!item.enabled) return;
    _menu.close();
    widget.onChanged?.call(item.value);
  }

  void _toggle() => _menu.isOpen ? _menu.close() : _menu.open();

  @override
  Widget build(BuildContext context) {
    final type = UiType.of(context);
    final matches = widget.items
        .where((item) => item.label.toLowerCase().contains(_query))
        .toList();
    final searchable = widget.items.length > 8;
    final selected = widget.items
        .where((item) => item.value == widget.value)
        .firstOrNull;
    final name = widget.label ?? widget.hint ?? '';
    final enabled = widget.onChanged != null;
    return LayoutBuilder(
      builder: (context, constraints) {
        final full = constraints.maxWidth.isFinite;
        final menuWidth = (full ? constraints.maxWidth : 280.0).clamp(
          240.0,
          480.0,
        );
        // Leading check column and menu item padding.
        final rowWidth = menuWidth - (widget.value == null ? 32 : 64);
        final anchor = MenuAnchor(
          controller: _menu,
          onOpen: () {
            _search.clear();
            setState(() => _query = '');
            if (searchable) {
              WidgetsBinding.instance.addPostFrameCallback((_) {
                if (mounted && _menu.isOpen) _searchFocus.requestFocus();
              });
            }
          },
          menuChildren: [
            SizedBox(
              width: menuWidth,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (searchable)
                    Padding(
                      padding: const EdgeInsets.all(8),
                      child: TextField(
                        controller: _search,
                        focusNode: _searchFocus,
                        decoration: const InputDecoration(
                          labelText: 'Search options',
                          prefixIcon: Icon(Icons.search),
                          isDense: true,
                        ),
                        onChanged: (v) =>
                            setState(() => _query = v.trim().toLowerCase()),
                        onSubmitted: (_) {
                          final first = matches
                              .where((item) => item.enabled)
                              .firstOrNull;
                          if (first != null) _choose(first);
                        },
                      ),
                    ),
                  ConstrainedBox(
                    constraints: BoxConstraints(
                      maxHeight: MediaQuery.sizeOf(context).height * .55,
                    ),
                    child: SingleChildScrollView(
                      primary: false,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          if (matches.isEmpty)
                            Padding(
                              padding: const EdgeInsets.all(16),
                              child: Semantics(
                                liveRegion: true,
                                child: const Text('No matching options'),
                              ),
                            ),
                          for (final item in matches)
                            MenuItemButton(
                              onPressed: item.enabled
                                  ? () => _choose(item)
                                  : null,
                              leadingIcon: widget.value == null
                                  ? null
                                  : SizedBox(
                                      width: 20,
                                      child: item.value == widget.value
                                          ? const Icon(Icons.check, size: 18)
                                          : null,
                                    ),
                              child:
                                  widget.itemBuilder?.call(
                                    context,
                                    item,
                                    rowWidth,
                                  ) ??
                                  _row(item, type, rowWidth),
                            ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
          builder: (context, controller, _) =>
              widget.triggerBuilder?.call(context, controller) ??
              _box(context, selected, full),
        );
        final field = Semantics(
          button: true,
          enabled: enabled,
          label: name,
          child: FocusableActionDetector(
            enabled: enabled,
            onFocusChange: (v) => setState(() => _focused = v),
            mouseCursor: enabled
                ? SystemMouseCursors.click
                : SystemMouseCursors.basic,
            actions: {
              ActivateIntent: CallbackAction<ActivateIntent>(
                onInvoke: (_) => _toggle(),
              ),
            },
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: enabled ? _toggle : null,
              child: Opacity(opacity: enabled ? 1 : 0.45, child: anchor),
            ),
          ),
        );
        return Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: full && widget.triggerBuilder == null
              ? CrossAxisAlignment.stretch
              : CrossAxisAlignment.start,
          children: [
            if (widget.label != null && widget.label!.isNotEmpty) ...[
              ExcludeSemantics(
                child: Text(widget.label!, style: type.fieldLabel),
              ),
              const SizedBox(height: 7),
            ],
            field,
          ],
        );
      },
    );
  }

  Widget _box(BuildContext context, UiKitSelectItem<T>? selected, bool full) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final radius = UiTokens.of(context).fieldRadius;
    final box = DecoratedBox(
      key: const ValueKey('ui_kit_select_box'),
      decoration: BoxDecoration(
        color: c.surface,
        borderRadius: BorderRadius.circular(radius),
        border: Border.all(color: _focused ? c.accent : c.inputBorder),
        boxShadow: UiShadows.xs(uiKitShadowBase(context)),
      ),
      child: SizedBox(
        height: 48,
        width: full ? double.infinity : null,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14),
          child: Row(
            mainAxisSize: full ? MainAxisSize.max : MainAxisSize.min,
            children: [
              Flexible(
                child: Text(
                  selected?.label ?? widget.hint ?? '',
                  overflow: TextOverflow.ellipsis,
                  style: type.body.copyWith(
                    color: selected == null ? c.subtle : c.strong,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Icon(Icons.keyboard_arrow_down, size: 18, color: c.muted),
            ],
          ),
        ),
      ),
    );
    return Stack(
      clipBehavior: Clip.none,
      children: [
        if (_focused)
          Positioned(
            left: -3,
            top: -3,
            right: -3,
            bottom: -3,
            child: IgnorePointer(
              child: DecoratedBox(
                key: const ValueKey('ui_kit_select_ring'),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(radius + 3),
                  border: Border.all(color: c.focus, width: 3),
                ),
              ),
            ),
          ),
        box,
      ],
    );
  }

  Widget _row(UiKitSelectItem<T> item, UiTypeSet type, double width) =>
      SizedBox(
        width: width,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(item.label, style: type.menuItem),
            if (item.detail != null) Text(item.detail!, style: type.meta),
          ],
        ),
      );
}
