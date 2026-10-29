import 'dart:async';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import '../home/app_icon.dart';

typedef DockRequest = Future<void> Function(String operation, String input);

// The focused app's toolbar. Every entry posts the app a request and waits for nothing: the
// app's answer is neuron state its surface shows.
class Dock extends StatelessWidget {
  const Dock({
    super.key,
    required this.app,
    required this.manifest,
    required this.onRequest,
    this.loadIcon,
    this.iconRoute,
  });

  final String app;
  final AppManifest manifest;
  final DockRequest onRequest;
  final IconLoader? loadIcon;
  // The route serving the app's own icon file; an entry naming that file renders it.
  final String? iconRoute;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    // Floating Ino is centered in the room the dock is given; a dock in a scroller wraps its content.
    builder: (context, constraints) => Row(
      mainAxisSize: MainAxisSize.min,
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        for (final entry in manifest.toolbar)
          _DockEntry(
            key: ValueKey('dock-entry-${entry.operation}'),
            entry: entry,
            manifestIcon: manifest.icon,
            iconRoute: iconRoute,
            loadIcon: loadIcon,
            onRequest: onRequest,
          ),
      ],
    ),
  );
}

class _DockEntry extends StatefulWidget {
  const _DockEntry({
    super.key,
    required this.entry,
    required this.onRequest,
    this.manifestIcon,
    this.iconRoute,
    this.loadIcon,
  });

  final AppToolbarEntry entry;
  final DockRequest onRequest;
  final String? manifestIcon, iconRoute;
  final IconLoader? loadIcon;

  @override
  State<_DockEntry> createState() => _DockEntryState();
}

class _DockEntryState extends State<_DockEntry> {
  bool _on = false;
  double? _value;

  AppToolbarEntry get entry => widget.entry;
  String get label => entry.label ?? entry.operation;

  void _post(String input) =>
      unawaited(widget.onRequest(entry.operation, input));

  Widget _icon() {
    final name = entry.icon;
    final data = name == null ? null : shellIcon(name);
    if (data != null) return Icon(data, size: 22);
    if (name != null &&
        name == widget.manifestIcon &&
        widget.iconRoute != null) {
      return AppIcon(
        title: label,
        path: widget.iconRoute,
        load: widget.loadIcon,
        size: 22,
      );
    }
    return Text(label, style: UiType.of(context).control);
  }

  static String _text(double value) => value == value.roundToDouble()
      ? value.toInt().toString()
      : value.toString();

  @override
  Widget build(BuildContext context) {
    final min = entry.min ?? 0, max = entry.max ?? 1;
    final c = UiColors.of(context);
    return switch (entry.kind) {
      AppToolbarKind.button => IconButton(
        key: ValueKey('dock-${entry.operation}'),
        tooltip: label,
        color: c.body,
        style: IconButton.styleFrom(
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(
              UiTokens.of(context).radiusSmall,
            ),
          ),
        ),
        onPressed: () => _post(''),
        icon: _icon(),
      ),
      AppToolbarKind.toggle => Tooltip(
        message: label,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8),
          child: UiKitSwitch(
            key: ValueKey('dock-${entry.operation}'),
            value: _on,
            semanticLabel: label,
            onChanged: (value) {
              setState(() => _on = value);
              _post(value ? 'on' : 'off');
            },
          ),
        ),
      ),
      AppToolbarKind.slider => SizedBox(
        width: 140,
        child: Slider(
          key: ValueKey('dock-${entry.operation}'),
          activeColor: c.accent,
          inactiveColor: c.inputBorder,
          min: min,
          max: max,
          value: (_value ?? min).clamp(min, max),
          label: label,
          onChanged: (value) => setState(() => _value = value),
          onChangeEnd: (value) => _post(_text(value)),
        ),
      ),
      AppToolbarKind.text => DockTextField(
        key: ValueKey('dock-${entry.operation}'),
        hint: label,
        onSubmit: _post,
      ),
    };
  }
}

// The text kind: one line; submit posts the trimmed text and clears the field.
class DockTextField extends StatefulWidget {
  const DockTextField({super.key, required this.hint, required this.onSubmit});

  final String hint;
  final ValueChanged<String> onSubmit;

  @override
  State<DockTextField> createState() => _DockTextFieldState();
}

class _DockTextFieldState extends State<DockTextField> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _submit(String text) {
    final trimmed = text.trim();
    if (trimmed.isEmpty) return;
    widget.onSubmit(trimmed);
    _controller.clear();
  }

  @override
  Widget build(BuildContext context) => SizedBox(
    width: 360,
    child: UiKitField(
      label: '',
      placeholder: widget.hint,
      controller: _controller,
      textInputAction: TextInputAction.send,
      onSubmitted: _submit,
    ),
  );
}
