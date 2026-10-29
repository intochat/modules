import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';
import 'ui_kit_surface.dart';

/// Figma text field: 14/600 label, 7 gap, then a 48-high box with radius 12,
/// `inputBorder` border, `surface` fill and xs shadow. Hover turns the border
/// `muted`; focus turns it `accent` and adds a 3px `focus` ring outside (the
/// ring alone is too low contrast); an error turns it `error` and shows the
/// message below.
final class UiKitField extends StatefulWidget {
  const UiKitField({
    super.key,
    required this.label,
    this.controller,
    this.placeholder,
    this.error,
    this.secret = false,
    this.onSubmitted,
    this.onChanged,
    this.focusNode,
    this.enabled = true,
    this.multiline = false,
    this.helper,
    this.textInputAction,
    this.fieldKey,
    this.autofillHints,
    this.autofocus = false,
    this.autocorrect = true,
    this.suffix,
  });

  final String label;
  final TextEditingController? controller;
  final String? placeholder;
  final String? error;
  final bool secret;
  final ValueChanged<String>? onSubmitted;
  final ValueChanged<String>? onChanged;
  final FocusNode? focusNode;
  final bool enabled;
  final bool multiline;
  final String? helper;
  final TextInputAction? textInputAction;

  /// Key for the inner [TextField], for callers that address it directly.
  final Key? fieldKey;

  final Iterable<String>? autofillHints;
  final bool autofocus;

  /// When false, also turns off suggestions (usernames, passwords).
  final bool autocorrect;

  /// Trailing widget inside the box, e.g. a reveal-password icon button.
  final Widget? suffix;

  @override
  State<UiKitField> createState() => _UiKitFieldState();
}

class _UiKitFieldState extends State<UiKitField> {
  FocusNode? _ownFocus;
  bool _hover = false;

  FocusNode get _focus => widget.focusNode ?? (_ownFocus ??= FocusNode());

  @override
  void initState() {
    super.initState();
    _focus.addListener(_refresh);
  }

  @override
  void didUpdateWidget(UiKitField old) {
    super.didUpdateWidget(old);
    final previous = old.focusNode ?? _ownFocus;
    if (previous != _focus) {
      previous?.removeListener(_refresh);
      _focus.addListener(_refresh);
    }
  }

  void _refresh() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _focus.removeListener(_refresh);
    _ownFocus?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final radius = UiTokens.of(context).fieldRadius;
    final focused = _focus.hasFocus;
    final error = widget.error;
    final hasError = error != null && error.isNotEmpty;
    final borderColor = hasError
        ? c.error
        : focused
        ? c.accent
        : _hover && widget.enabled
        ? c.muted
        : c.inputBorder;
    final placeholder = widget.placeholder;
    final style = type.body.copyWith(color: c.strong);
    final hintStyle = type.body.copyWith(color: c.subtle);
    final name = widget.label.isNotEmpty ? widget.label : placeholder;
    final textField = TextField(
      key: widget.fieldKey,
      controller: widget.controller,
      focusNode: _focus,
      enabled: widget.enabled,
      obscureText: widget.secret,
      minLines: widget.multiline ? 2 : 1,
      maxLines: widget.multiline ? 5 : 1,
      textInputAction: widget.textInputAction,
      autofillHints: widget.autofillHints,
      autofocus: widget.autofocus,
      autocorrect: widget.autocorrect,
      enableSuggestions: widget.autocorrect,
      style: style,
      cursorColor: c.accent,
      // The box above draws the border; clear every theme border and fill
      // so the app's InputDecorationTheme does not draw a second one inside.
      decoration:
          InputDecoration.collapsed(
            hintText: null,
            hintStyle: hintStyle,
          ).copyWith(
            // The placeholder is visual only: the field's name below already
            // carries it, and a semantic hint would repeat it in the name.
            hint: placeholder == null
                ? null
                : ExcludeSemantics(
                    // Resolved as the decorator resolves a hintText style.
                    child: Text(
                      placeholder,
                      style: Theme.of(context).textTheme.bodyLarge!
                          .merge(style)
                          .merge(hintStyle),
                    ),
                  ),
            enabledBorder: InputBorder.none,
            focusedBorder: InputBorder.none,
            disabledBorder: InputBorder.none,
            errorBorder: InputBorder.none,
            focusedErrorBorder: InputBorder.none,
            filled: false,
          ),
      onChanged: widget.onChanged,
      onSubmitted: widget.onSubmitted,
    );
    // The visible label sits outside the TextField, so name the field for
    // assistive technology explicitly. Merge the name into the TextField's
    // own node: a separate text-field node would render on the web as a
    // second, disabled input, and the real input would lose its name once
    // typing hides the placeholder.
    final input = MergeSemantics(
      child: Semantics(label: name, child: textField),
    );
    final box = DecoratedBox(
      key: const ValueKey('ui_kit_field_box'),
      decoration: BoxDecoration(
        color: c.surface,
        borderRadius: BorderRadius.circular(radius),
        border: Border.all(color: borderColor),
        boxShadow: UiShadows.xs(uiKitShadowBase(context)),
      ),
      child: Padding(
        // Figma's icon button sits 8px from the edge (34px button, 6px pad).
        padding: EdgeInsets.fromLTRB(
          14,
          widget.multiline ? 6 : 0,
          widget.suffix == null ? 14 : 6,
          widget.multiline ? 6 : 0,
        ),
        child: widget.multiline
            ? ConstrainedBox(
                constraints: const BoxConstraints(minHeight: 48),
                child: Align(alignment: Alignment.centerLeft, child: input),
              )
            : SizedBox(
                height: 48,
                child: widget.suffix == null
                    ? Align(alignment: Alignment.centerLeft, child: input)
                    : Row(
                        children: [
                          Expanded(child: input),
                          widget.suffix!,
                        ],
                      ),
              ),
      ),
    );
    final helper = widget.helper;
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (widget.label.isNotEmpty) ...[
          ExcludeSemantics(child: Text(widget.label, style: type.fieldLabel)),
          const SizedBox(height: 7),
        ],
        MouseRegion(
          onEnter: (_) => setState(() => _hover = true),
          onExit: (_) => setState(() => _hover = false),
          child: Stack(
            clipBehavior: Clip.none,
            children: [
              if (focused)
                Positioned(
                  left: -3,
                  top: -3,
                  right: -3,
                  bottom: -3,
                  child: IgnorePointer(
                    child: DecoratedBox(
                      key: const ValueKey('ui_kit_field_ring'),
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(radius + 3),
                        border: Border.all(color: c.focus, width: 3),
                      ),
                    ),
                  ),
                ),
              box,
            ],
          ),
        ),
        if (hasError) ...[
          const SizedBox(height: 6),
          Text(error, style: type.meta.copyWith(color: c.error)),
        ] else if (helper != null) ...[
          const SizedBox(height: 6),
          Text(helper, style: type.meta),
        ],
      ],
    );
  }
}
