import 'package:flutter/material.dart';

import '../components/kit/kit_toast.dart';
import '../components/kit/ui_kit_button.dart';
import '../components/kit/ui_kit_field.dart';
import '../components/kit/ui_kit_select.dart';
import '../components/kit/ui_kit_switch.dart';
import '../theme/ui_colors.dart';
import '../theme/ui_theme.dart';
import 'local_preferences.dart';

const _failed = 'Could not save that change.';

/// One choice of a `Choice` preference.
final class PreferenceChoiceItem {
  const PreferenceChoiceItem(
    this.id,
    this.label, {
    this.enabled = true,
    this.detail,
  });

  final String id, label;
  final bool enabled;
  final String? detail;
}

/// The `preference` neuron's control. A `local*` store reads and writes
/// [local]; any other store calls [onSet], which sends `Set` to the neuron
/// over the person's session. A failed write reverts and shows a generic
/// toast.
class UiPreferenceControl extends StatefulWidget {
  const UiPreferenceControl({
    super.key,
    required this.store,
    required this.label,
    required this.kind,
    required this.onSet,
    this.choices = const [],
    this.value,
    this.local,
    this.enabled = true,
  });

  final PreferenceStore store;
  final String label;
  final PreferenceKind kind;
  final List<PreferenceChoiceItem> choices;

  /// The neuron's value for a brain store; ignored for a `local*` store.
  final String? value;
  final LocalPreferences? local;
  final Future<void> Function(String value) onSet;
  final bool enabled;

  @override
  State<UiPreferenceControl> createState() => _UiPreferenceControlState();
}

class _UiPreferenceControlState extends State<UiPreferenceControl> {
  late String? _value = _source;
  late final _text = TextEditingController(text: _value ?? '');
  bool _busy = false;

  String? get _source =>
      widget.store.isLocal ? widget.local?.read(widget.store) : widget.value;

  bool get _canWrite =>
      widget.enabled &&
      !_busy &&
      (!widget.store.isLocal || widget.local != null);

  @override
  void didUpdateWidget(UiPreferenceControl old) {
    super.didUpdateWidget(old);
    if (_busy) return;
    final next = _source;
    if (next != _value) {
      _value = next;
      _text.text = next ?? '';
    }
  }

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  Future<void> _save(String next, {bool announce = false}) async {
    final previous = _value;
    setState(() {
      _value = next;
      _busy = true;
    });
    try {
      if (widget.store.isLocal) {
        final local = widget.local;
        if (local == null) throw StateError('No device preferences.');
        await local.write(widget.store, next);
      } else {
        await widget.onSet(next);
      }
      if (mounted && announce) {
        showKitToast(context, 'Saved.', tone: UiTone.done);
      }
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _value = previous;
        _text.text = previous ?? '';
      });
      showKitToast(context, _failed, tone: UiTone.error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final label = Text(
      widget.label,
      style: type.body.copyWith(color: c.strong, fontWeight: FontWeight.w600),
    );
    switch (widget.kind) {
      case PreferenceKind.toggle:
        final on = _value == 'on';
        return Row(
          key: const Key('preference-toggle'),
          children: [
            Expanded(child: label),
            UiKitSwitch(
              value: on,
              semanticLabel: widget.label,
              onChanged: _canWrite ? (v) => _save(v ? 'on' : 'off') : null,
            ),
          ],
        );
      case PreferenceKind.choice:
        return UiKitSelect<String>(
          key: const Key('preference-choice'),
          label: widget.label,
          value: widget.choices.any((choice) => choice.id == _value)
              ? _value
              : null,
          items: [
            for (final choice in widget.choices)
              UiKitSelectItem(
                choice.id,
                choice.label,
                enabled: choice.enabled,
                detail: choice.detail,
              ),
          ],
          onChanged: _canWrite
              ? (id) {
                  if (id != _value) _save(id);
                }
              : null,
        );
      case PreferenceKind.text:
        return Row(
          key: const Key('preference-text'),
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              child: UiKitField(
                label: widget.label,
                controller: _text,
                enabled: _canWrite,
                onSubmitted: (_) => _saveText(),
              ),
            ),
            const SizedBox(width: 8),
            UiKitButton(
              key: const Key('preference-text-save'),
              label: 'Save',
              style: UiKitButtonStyle.secondary,
              onPressed: _canWrite ? _saveText : null,
            ),
          ],
        );
    }
  }

  void _saveText() {
    final next = _text.text.trim();
    if (next == (_value ?? '')) return;
    _save(next, announce: true);
  }
}
