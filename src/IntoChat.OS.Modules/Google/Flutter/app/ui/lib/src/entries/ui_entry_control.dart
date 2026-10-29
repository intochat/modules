import 'package:flutter/material.dart';

import '../components/kit/kit_toast.dart';
import '../components/kit/ui_kit_button.dart';
import '../components/kit/ui_kit_field.dart';
import '../composition/value_nodes/view_badge.dart';
import '../theme/ui_colors.dart';
import '../theme/ui_theme.dart';
import '../theme/ui_tokens.dart';
import 'entries_client.dart';

enum EntryControlMode {
  key,
  collection;

  /// C# `EntryControlMode` (Key 0, Collection 1), as an int or a name.
  static EntryControlMode parse(Object? value) =>
      value == 1 || value == 'collection' || value == 'Collection'
      ? collection
      : key;
}

const _ownerOnlyKeys = 'Only the brain owner can change API keys.';
const _ownerOnlyVariables =
    'Only the brain owner can change variables and secrets.';
const _failed = 'Could not save that change.';
const _invalidName = 'Use letters, digits, _ . - only';

// BrainEntries.IsValidName without ':' so a collection entry can never take the provider: prefix.
final _validName = RegExp(r'^[A-Za-z_][A-Za-z0-9_.\-]{0,63}$');

/// The `entrycontrol` renderer: a brain's API key (key mode) or its
/// variables and secrets (collection mode), read and written over the
/// person's session through [entries]. The neuron carries only the spec; a
/// value lives in a dialog controller until `PUT` and goes nowhere else.
/// [onChanged] tells the neuron a write succeeded; it carries no value.
class UiEntryControl extends StatefulWidget {
  const UiEntryControl({
    super.key,
    required this.mode,
    required this.label,
    required this.entries,
    required this.onChanged,
    this.entryName,
    this.enabled = true,
  });
  final EntryControlMode mode;
  final String label;
  final String? entryName;
  final EntriesClient? entries;
  final Future<void> Function() onChanged;
  final bool enabled;

  @override
  State<UiEntryControl> createState() => _UiEntryControlState();
}

class _UiEntryControlState extends State<UiEntryControl> {
  List<EntrySummary> _rows = const [];
  bool _loading = true, _forbidden = false;
  String? _error;

  bool get _keyMode => widget.mode == EntryControlMode.key;
  String get _ownerOnly => _keyMode ? _ownerOnlyKeys : _ownerOnlyVariables;
  String _failure(Object error) =>
      error is SettingsForbidden ? _ownerOnly : _failed;

  /// Key mode names its provider from the known list, never from the
  /// app-written label, so an app cannot mislabel a key field.
  String get _label {
    if (!_keyMode) return widget.label;
    for (final (id, name) in keyProviders) {
      if (widget.entryName == '$providerEntryPrefix$id') return name;
    }
    return widget.entryName ?? '';
  }

  bool get _locked =>
      _loading || _forbidden || !widget.enabled || widget.entries == null;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(UiEntryControl old) {
    super.didUpdateWidget(old);
    // NeuronView calls the client factory on every rebuild; only a new brain reloads.
    if (old.entries?.brainId != widget.entries?.brainId ||
        old.entryName != widget.entryName ||
        old.mode != widget.mode) {
      _forbidden = false;
      _load();
    }
  }

  Future<void> _load() async {
    final entries = widget.entries;
    if (entries == null) {
      setState(() {
        _loading = false;
        _error = _failed;
      });
      return;
    }
    setState(() => _loading = true);
    try {
      final rows = await entries.list();
      if (!mounted) return;
      setState(() {
        _rows = [
          for (final row in rows)
            if (_keyMode
                ? row.name == widget.entryName
                : !row.name.startsWith(providerEntryPrefix))
              row,
        ];
        _error = null;
      });
    } catch (error) {
      if (mounted) {
        setState(() {
          _error = _failure(error);
          if (error is SettingsForbidden) _forbidden = true;
        });
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  /// Runs one write. Success toasts, tells the neuron and reloads; a failure
  /// returns its copy and never the exception text.
  Future<String?> _write(Future<void> Function(EntriesClient) write) async {
    final entries = widget.entries;
    if (entries == null) return _failed;
    try {
      await write(entries);
    } catch (error) {
      final copy = _failure(error);
      if (mounted) {
        if (error is SettingsForbidden) setState(() => _forbidden = true);
        showKitToast(context, copy, tone: UiTone.error);
      }
      return copy;
    }
    if (!mounted) return null;
    showKitToast(context, 'Saved.', tone: UiTone.done);
    try {
      await widget.onChanged();
    } catch (_) {
      // The entry is saved; a missed change notice only delays a refresh.
    }
    await _load();
    return null;
  }

  Future<void> _addKey(bool replace) async {
    final name = widget.entryName;
    if (name == null) return;
    await showDialog<void>(
      context: context,
      builder: (_) => _KeyDialog(
        title: '${replace ? 'Replace' : 'Add'} $_label key',
        save: (value) =>
            _write((entries) => entries.put(name, EntryKind.secret, value)),
      ),
    );
  }

  Future<void> _remove(String name, String title, String detail) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialog) => _KitDialog(
        title: title,
        body: detail,
        actions: [
          UiKitButton(
            key: const Key('entry-remove-confirm'),
            label: 'Remove',
            style: UiKitButtonStyle.danger,
            fullWidth: true,
            onPressed: () => Navigator.pop(dialog, true),
          ),
          UiKitButton(
            label: 'Cancel',
            style: UiKitButtonStyle.secondary,
            fullWidth: true,
            onPressed: () => Navigator.pop(dialog, false),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    final failure = await _write((entries) => entries.remove(name));
    if (failure != null && mounted) setState(() => _error = failure);
  }

  Future<void> _addEntry() => showDialog<void>(
    context: context,
    builder: (_) => _EntryDialog(
      save: (name, kind, value) =>
          _write((entries) => entries.put(name, kind, value)),
    ),
  );

  Widget _box(Key key, Widget child) {
    final c = UiColors.of(context);
    return Container(
      key: key,
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(11),
      decoration: BoxDecoration(
        color: c.surface,
        border: Border.all(color: c.hairline),
        borderRadius: BorderRadius.circular(UiTokens.of(context).controlRadius),
      ),
      child: child,
    );
  }

  Widget _keyRow() {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final entry = _rows.isEmpty ? null : _rows.first;
    final status = entry == null
        ? 'Not set'
        : entry.tail == null
        ? 'Set'
        : 'Set ••••${entry.tail}';
    final busy = _locked;
    return _box(
      const Key('entry-key-row'),
      Wrap(
        spacing: 10,
        runSpacing: 8,
        crossAxisAlignment: WrapCrossAlignment.center,
        alignment: WrapAlignment.spaceBetween,
        children: [
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                _label,
                style: type.body.copyWith(
                  color: c.strong,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(width: 10),
              ViewBadge(text: status, tone: entry == null ? 0 : 1),
            ],
          ),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              UiKitButton(
                key: const Key('entry-key-add'),
                label: entry == null ? 'Add' : 'Replace',
                style: UiKitButtonStyle.secondary,
                compact: true,
                onPressed: busy ? null : () => _addKey(entry != null),
              ),
              if (entry != null) ...[
                const SizedBox(width: 8),
                UiKitButton(
                  key: const Key('entry-key-remove'),
                  label: 'Remove',
                  style: UiKitButtonStyle.ghost,
                  compact: true,
                  onPressed: busy
                      ? null
                      : () => _remove(
                          entry.name,
                          'Remove the $_label key?',
                          'Models from $_label stop working in this brain until you add a key again.',
                        ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }

  Widget _entryRow(EntrySummary entry) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final secret = entry.kind == EntryKind.secret;
    final shown = secret ? '••••${entry.tail ?? ''}' : (entry.value ?? '');
    return _box(
      Key('entry-row-${entry.name}'),
      Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  entry.name,
                  style: type.body.copyWith(
                    color: c.strong,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 2),
                Text(shown, style: type.meta, overflow: TextOverflow.ellipsis),
              ],
            ),
          ),
          ViewBadge(text: secret ? 'Secret' : 'Variable', tone: secret ? 2 : 0),
          const SizedBox(width: 8),
          UiKitButton(
            key: Key('entry-remove-${entry.name}'),
            label: 'Remove',
            style: UiKitButtonStyle.ghost,
            compact: true,
            onPressed: _locked
                ? null
                : () => _remove(
                    entry.name,
                    'Remove ${entry.name}?',
                    'Apps and agents that use it will no longer find it.',
                  ),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final error = _error;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        if (_loading) const LinearProgressIndicator(),
        if (error != null)
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: Semantics(liveRegion: true, child: Text(error)),
          ),
        if (_keyMode)
          _keyRow()
        else ...[
          for (final row in _rows) _entryRow(row),
          Align(
            alignment: Alignment.centerLeft,
            child: UiKitButton(
              key: const Key('entry-add'),
              label: 'Add',
              style: UiKitButtonStyle.secondary,
              onPressed: _locked ? null : _addEntry,
            ),
          ),
        ],
      ],
    );
  }
}

/// A kit dialog with full-width actions.
class _KitDialog extends StatelessWidget {
  const _KitDialog({
    required this.title,
    required this.body,
    required this.actions,
    this.field,
  });
  final String title, body;
  final Widget? field;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    return Dialog(
      backgroundColor: c.surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(UiTokens.of(context).modalRadius),
      ),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(title, style: type.heading.copyWith(color: c.strong)),
              const SizedBox(height: 8),
              Text(body, style: type.bodyMuted),
              if (field != null) ...[const SizedBox(height: 16), field!],
              const SizedBox(height: 20),
              for (final (i, action) in actions.indexed) ...[
                if (i > 0) const SizedBox(height: 8),
                action,
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// Add or replace one key. The value lives only in this dialog's controller,
/// which is cleared once the save succeeds and when the dialog closes.
class _KeyDialog extends StatefulWidget {
  const _KeyDialog({required this.title, required this.save});
  final String title;
  final Future<String?> Function(String value) save;
  @override
  State<_KeyDialog> createState() => _KeyDialogState();
}

class _KeyDialogState extends State<_KeyDialog> {
  final _field = TextEditingController();
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _field.clear();
    _field.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final value = _field.text.trim();
    if (value.isEmpty) {
      setState(() => _error = 'Paste a key first.');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    final failure = await widget.save(value);
    if (!mounted) return;
    if (failure == null) {
      _field.clear();
      Navigator.pop(context);
    } else {
      // The person retypes: a failed value never lingers in the field.
      _field.clear();
      setState(() {
        _saving = false;
        _error = failure;
      });
    }
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !_saving,
    child: _KitDialog(
      title: widget.title,
      body: 'The key is sealed for this brain and cannot be read back.',
      field: UiKitField(
        label: 'API key',
        controller: _field,
        secret: true,
        enabled: !_saving,
        error: _error,
        autocorrect: false,
        fieldKey: const Key('entry-key-field'),
        onSubmitted: (_) => _save(),
      ),
      actions: [
        UiKitButton(
          key: const Key('entry-key-save'),
          label: 'Save key',
          fullWidth: true,
          onPressed: _saving ? null : _save,
        ),
        UiKitButton(
          label: 'Cancel',
          style: UiKitButtonStyle.secondary,
          fullWidth: true,
          onPressed: _saving ? null : () => Navigator.pop(context),
        ),
      ],
    ),
  );
}

/// Name, kind and value of a new entry. The value lives only in this
/// dialog's controller, which is cleared once the save succeeds.
class _EntryDialog extends StatefulWidget {
  const _EntryDialog({required this.save});
  final Future<String?> Function(String name, EntryKind kind, String value)
  save;
  @override
  State<_EntryDialog> createState() => _EntryDialogState();
}

class _EntryDialogState extends State<_EntryDialog> {
  final _name = TextEditingController();
  final _value = TextEditingController();
  EntryKind _kind = EntryKind.variable;
  bool _saving = false;
  String? _nameError, _error;

  @override
  void dispose() {
    _value.clear();
    _value.dispose();
    _name.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final name = _name.text.trim();
    if (!_validName.hasMatch(name)) {
      setState(() => _nameError = _invalidName);
      return;
    }
    if (_value.text.isEmpty) {
      setState(() {
        _nameError = null;
        _error = 'Enter a value first.';
      });
      return;
    }
    setState(() {
      _saving = true;
      _nameError = _error = null;
    });
    final failure = await widget.save(name, _kind, _value.text);
    if (!mounted) return;
    if (failure == null) {
      _value.clear();
      Navigator.pop(context);
    } else {
      _value.clear();
      setState(() {
        _saving = false;
        _error = failure;
      });
    }
  }

  Widget _kindButton(EntryKind kind, String label) => UiKitButton(
    key: Key('entry-kind-${kind.name}'),
    label: label,
    style: _kind == kind
        ? UiKitButtonStyle.primary
        : UiKitButtonStyle.secondary,
    compact: true,
    onPressed: _saving ? null : () => setState(() => _kind = kind),
  );

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !_saving,
    child: _KitDialog(
      title: 'Add a variable or secret',
      body: 'A variable can be read back. A secret is sealed and cannot.',
      field: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          UiKitField(
            label: 'Name',
            controller: _name,
            enabled: !_saving,
            error: _nameError,
            autocorrect: false,
            fieldKey: const Key('entry-name-field'),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              _kindButton(EntryKind.variable, 'Variable'),
              const SizedBox(width: 8),
              _kindButton(EntryKind.secret, 'Secret'),
            ],
          ),
          const SizedBox(height: 12),
          UiKitField(
            label: 'Value',
            controller: _value,
            secret: _kind == EntryKind.secret,
            enabled: !_saving,
            error: _error,
            autocorrect: false,
            fieldKey: const Key('entry-value-field'),
            onSubmitted: (_) => _save(),
          ),
        ],
      ),
      actions: [
        UiKitButton(
          key: const Key('entry-save'),
          label: 'Save',
          fullWidth: true,
          onPressed: _saving ? null : _save,
        ),
        UiKitButton(
          label: 'Cancel',
          style: UiKitButtonStyle.secondary,
          fullWidth: true,
          onPressed: _saving ? null : () => Navigator.pop(context),
        ),
      ],
    ),
  );
}
