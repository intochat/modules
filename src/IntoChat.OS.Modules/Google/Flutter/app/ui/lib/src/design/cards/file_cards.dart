import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

/// A file as cards show it: name, path, icon, size.
class FileRef {
  const FileRef({required this.name, required this.path, required this.icon, this.size = '', this.where = ''});

  final String name;

  /// Full path, e.g. "IntoChat storage / 30 Projects / Nordlys AS / CONTEXT.md".
  final String path;
  final IconData icon;
  final String size;

  /// The folder it sits in, short: "30 Projects › Nordlys AS".
  final String where;
}

/// File reference chip: in Ino's input and in messages. Click opens the file
/// selected and flashed in Files. Hover shows the path.
class FileChip extends StatefulWidget {
  const FileChip({super.key, required this.file, this.onTap, this.onRemove, this.missing = false});

  final FileRef file;
  final VoidCallback? onTap;

  /// Input only: the x that takes the file off the message.
  final VoidCallback? onRemove;

  /// The file went to Trash or away since it was attached.
  final bool missing;

  @override
  State<FileChip> createState() => _FileChipState();
}

class _FileChipState extends State<FileChip> {
  final _portal = OverlayPortalController();
  final _link = LayerLink();
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final f = widget.file;
    final fg = widget.missing ? c.textMuted : c.textStrong;
    return MouseRegion(
      cursor: widget.onTap == null ? MouseCursor.defer : SystemMouseCursors.click,
      onEnter: (_) {
        setState(() => _hover = true);
        Future.delayed(const Duration(milliseconds: 380), () {
          if (mounted && _hover && !_portal.isShowing) _portal.show();
        });
      },
      onExit: (_) {
        setState(() => _hover = false);
        if (_portal.isShowing) _portal.hide();
      },
      child: CompositedTransformTarget(
        link: _link,
        child: OverlayPortal(
          controller: _portal,
          overlayChildBuilder: (_) => CompositedTransformFollower(
            link: _link,
            targetAnchor: Alignment.topLeft,
            followerAnchor: Alignment.bottomLeft,
            offset: const Offset(0, -6),
            child: Align(
              alignment: Alignment.bottomLeft,
              widthFactor: 1,
              heightFactor: 1,
              child: IgnorePointer(
                child: Container(
                  constraints: const BoxConstraints(maxWidth: 420),
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
                  decoration: BoxDecoration(
                    color: c.isDark ? c.surfaceCard : c.surfaceInk,
                    borderRadius: BorderRadius.circular(IcRadius.xs),
                    border: c.isDark ? Border.all(color: c.borderHairline) : null,
                    boxShadow: IcShadow.sm(c),
                  ),
                  child: Text(
                    widget.missing ? '${f.path}  ·  in Trash' : f.path,
                    style: IcType.text(12, weight: FontWeight.w500, color: const Color(0xFFFBFAF7)),
                  ),
                ),
              ),
            ),
          ),
          child: Stop(
            onActivate: widget.onTap,
            builder: (context, v) => GestureDetector(
              onTap: widget.onTap,
              child: SelectionRing(
                on: v.selected,
                radius: IcRadius.sm,
                child: AnimatedContainer(
                  duration: IcMotion.fast,
                  height: 28,
                  padding: EdgeInsets.only(left: 8, right: widget.onRemove == null ? 10 : 4),
                  decoration: BoxDecoration(
                    color: _hover && widget.onTap != null ? c.accentTint : c.surfaceCard,
                    borderRadius: BorderRadius.circular(IcRadius.sm),
                    border: Border.all(color: _hover && widget.onTap != null ? c.accent : c.borderInput),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(f.icon, size: 14, color: widget.missing ? c.textMuted : c.accent),
                      const SizedBox(width: 6),
                      ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 260),
                        child: Text(
                          f.name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: IcType.text(12.5, weight: FontWeight.w600, color: fg).copyWith(
                            decoration: widget.missing ? TextDecoration.lineThrough : null,
                            decorationColor: c.textMuted,
                          ),
                        ),
                      ),
                      if (widget.onRemove != null) ...[
                        const SizedBox(width: 2),
                        Stop(
                          onActivate: widget.onRemove,
                          builder: (context, v) => GestureDetector(
                            onTap: widget.onRemove,
                            child: MouseRegion(
                              cursor: SystemMouseCursors.click,
                              child: Container(
                                padding: const EdgeInsets.all(4),
                                decoration: BoxDecoration(
                                  color: v.selected ? c.accent : null,
                                  borderRadius: BorderRadius.circular(IcRadius.xs),
                                ),
                                child: Icon(LucideIcons.x, size: 13, color: v.selected ? c.onAccent : c.textMuted),
                              ),
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

enum FileCardState { writing, saved, failed, conflict }

class FileCardData {
  const FileCardData({
    required this.file,
    this.verb = 'Saved',
    this.note,
    this.versions = const [],
    this.errorText = 'Couldn\'t save it. Google Drive didn\'t answer. Nothing was lost; I still have the file.',
  });

  final FileRef file;

  /// "Saved", "Updated", "Merged": what Ino did, in the saved line.
  final String verb;

  /// One plain line under the file, e.g. "14 rows, the new close dates".
  final String? note;

  /// Conflict: the two versions side by side, each with where it came from.
  final List<(FileRef, String)> versions;
  final String errorText;
}

/// File card: a file Ino created or changed. Every write shows as one, so the
/// person always sees what changed.
class FileCard extends StatelessWidget {
  const FileCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onOpen,
    this.onReveal,
    this.onSaveTo,
    this.onRetry,
    this.onMerge,
    this.onKeep,
  });

  final CardSource source;
  final FileCardData data;
  final FileCardState state;
  final VoidCallback? onOpen;
  final VoidCallback? onReveal;

  /// "Save to…": another folder.
  final VoidCallback? onSaveTo;
  final VoidCallback? onRetry;

  // Conflict
  final VoidCallback? onMerge;
  final ValueChanged<int>? onKeep;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final f = data.file;
    Widget fileRow(FileRef f, {String? line, bool dim = false}) => Row(
      children: [
        Container(
          width: 40,
          height: 40,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: c.surfaceSunk,
            borderRadius: BorderRadius.circular(IcRadius.sm),
            border: Border.all(color: c.borderHairline),
          ),
          child: Icon(f.icon, size: 19, color: dim ? c.textMuted : c.accent),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                f.name,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: IcType.text(14, weight: FontWeight.w600, color: c.textStrong),
              ),
              const SizedBox(height: 2),
              Text(
                line ?? [if (f.size.isNotEmpty) f.size, f.where].join(' · '),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: IcType.text(12.5, color: c.textMuted, tnum: true),
              ),
            ],
          ),
        ),
      ],
    );

    final status = switch (state) {
      FileCardState.writing => CardChip(
        label: 'Writing',
        fg: c.textBody,
        bg: c.surfaceSunk,
        icon: WorkingSpinner(size: 11, color: c.textBody),
      ),
      FileCardState.saved => CardChip(
        label: data.verb,
        fg: c.done,
        bg: c.doneBg,
        icon: Icon(LucideIcons.check, size: 12, color: c.done),
      ),
      FileCardState.failed => CardChip(
        label: 'Not saved',
        fg: c.error,
        bg: c.errorBg,
        icon: Icon(LucideIcons.triangleAlert, size: 12, color: c.error),
      ),
      FileCardState.conflict => CardChip(
        label: 'Two versions',
        fg: c.textStrong,
        bg: c.accentTint,
        icon: Icon(LucideIcons.hand, size: 12, color: c.accent),
      ),
    };

    return CardShell(
      source: source,
      status: status,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (state == FileCardState.conflict) ...[
            Text(
              'Two machines changed the same file. I can merge them, or you keep one.',
              style: IcType.text(13.5, color: c.textBody, height: 1.45),
            ),
            const SizedBox(height: 12),
            for (var i = 0; i < data.versions.length; i++) ...[
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(IcRadius.button),
                  border: Border.all(color: c.borderHairline),
                ),
                child: Row(
                  children: [
                    Container(
                      width: 22,
                      height: 22,
                      alignment: Alignment.center,
                      decoration: BoxDecoration(color: c.surfaceSunk, borderRadius: BorderRadius.circular(IcRadius.xs)),
                      child: Text(
                        'V${i + 1}',
                        style: IcType.text(11, weight: FontWeight.w700, color: c.textBody, tnum: true),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(child: fileRow(data.versions[i].$1, line: data.versions[i].$2)),
                  ],
                ),
              ),
              const SizedBox(height: 8),
            ],
            const SizedBox(height: 4),
            PromptOptions(
              options: [
                PromptOption('Merge them', icon: LucideIcons.gitMerge, onTap: onMerge),
                for (var i = 0; i < data.versions.length; i++)
                  PromptOption(
                    'Keep V${i + 1}',
                    detail: data.versions[i].$2,
                    onTap: onKeep == null ? null : () => onKeep!(i),
                  ),
                PromptOption('Show in Files', icon: LucideIcons.folderSearch, onTap: onReveal),
              ],
            ),
          ] else ...[
            fileRow(
              f,
              line: state == FileCardState.writing
                  ? 'Saving to ${f.where}'
                  : [
                      if (f.size.isNotEmpty) f.size,
                      '${state == FileCardState.saved ? 'In' : 'For'} ${f.where}',
                    ].join(' · '),
              dim: state == FileCardState.writing,
            ),
            if (data.note != null && state != FileCardState.failed) ...[
              const SizedBox(height: 10),
              Text(data.note!, style: IcType.text(13, color: c.textBody, height: 1.45)),
            ],
            const SizedBox(height: 14),
            switch (state) {
              FileCardState.writing => const Row(
                children: [Skeleton(width: 84, height: 30), SizedBox(width: 8), Skeleton(width: 120, height: 30)],
              ),
              FileCardState.failed => CardError(text: data.errorText, onRetry: onRetry),
              _ => Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  Btn(
                    label: 'Open',
                    icon: LucideIcons.squareArrowOutUpRight,
                    primary: true,
                    small: true,
                    onTap: onOpen,
                  ),
                  Btn(label: 'Reveal in Files', icon: LucideIcons.folderSearch, small: true, onTap: onReveal),
                  Btn(label: 'Save to…', icon: LucideIcons.folderInput, small: true, onTap: onSaveTo),
                ],
              ),
            },
          ],
        ],
      ),
    );
  }
}

enum SaveToState { pending, saving, saved, cancelled }

class SaveToData {
  SaveToData({required this.fileName, required this.folder, required this.folders, required this.icon, this.why});

  /// Pre-filled from the naming convention; the person corrects it in place.
  String fileName;

  /// Pre-filled from the project structure, e.g. "IntoChat storage › 30 Projects › Nordlys AS".
  String folder;

  /// Other folders that fit, for the folder menu.
  final List<String> folders;
  final IconData icon;

  /// Why this folder: "It's the project folder for Nordlys AS."
  final String? why;

  String? decidedAt;
}

/// Save-to picker: when Ino must choose where a file goes. Folder and name
/// are pre-filled; the person corrects them in place and approves. Never a form.
class SaveToPicker extends StatefulWidget {
  const SaveToPicker({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onApprove,
    this.onCancel,
  });

  final CardSource source;
  final SaveToData data;
  final SaveToState state;
  final VoidCallback? onApprove;
  final VoidCallback? onCancel;

  @override
  State<SaveToPicker> createState() => _SaveToPickerState();
}

class _SaveToPickerState extends State<SaveToPicker> {
  bool _editingName = false;
  bool _foldersOpen = false;

  /// Started from the keyboard: Return or Esc go back to the card's list.
  bool _byKey = false;
  late final _name = TextEditingController(text: widget.data.fileName);
  final _focus = FocusNode();

  @override
  void initState() {
    super.initState();
    _focus.addListener(() {
      if (!_focus.hasFocus && _editingName) _commitName(back: false);
    });
  }

  @override
  void dispose() {
    _name.dispose();
    _focus.dispose();
    super.dispose();
  }

  void _commitName({bool back = true}) {
    final t = _name.text.trim();
    if (t.isNotEmpty) widget.data.fileName = t;
    setState(() => _editingName = false);
    if (back && _byKey) PromptKeys.maybeOf(context)?.refocus();
    _byKey = false;
  }

  void _startName({bool byKey = false}) {
    _byKey = byKey;
    setState(() => _editingName = true);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final dot = _name.text.lastIndexOf('.');
      _name.selection = TextSelection(baseOffset: 0, extentOffset: dot > 0 ? dot : _name.text.length);
      _focus.requestFocus();
    });
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = widget.data;
    if (widget.state == SaveToState.saved || widget.state == SaveToState.cancelled) {
      final saved = widget.state == SaveToState.saved;
      return CardResult(
        source: widget.source,
        tone: saved ? ResultTone.done : ResultTone.rejected,
        text: saved ? 'Saved by Jeff${d.decidedAt == null ? '' : ', ${d.decidedAt}'}' : 'Not saved',
        detail: saved ? '${d.fileName} in ${d.folder}' : d.fileName,
      );
    }
    final pending = widget.state == SaveToState.pending;
    Widget editableRow({
      required String label,
      required Widget value,
      required VoidCallback? onEdit,
      VoidCallback? onKey,
    }) => Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          SizedBox(
            width: 64,
            child: Text(label, style: IcType.text(13, color: c.textMuted)),
          ),
          Expanded(
            child: Stop(
              onActivate: onKey ?? onEdit,
              builder: (context, v) => MouseRegion(
                cursor: onEdit == null ? MouseCursor.defer : SystemMouseCursors.click,
                child: GestureDetector(
                  onTap: onEdit,
                  child: SelectionRing(
                    on: v.selected,
                    radius: IcRadius.sm,
                    child: Container(
                      constraints: const BoxConstraints(minHeight: 34),
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
                      decoration: BoxDecoration(
                        color: c.surfaceSunk,
                        borderRadius: BorderRadius.circular(IcRadius.sm),
                        border: Border.all(color: c.borderHairline),
                      ),
                      child: value,
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );

    return CardShell(
      source: widget.source,
      status: pending
          ? CardChip(
              label: 'Needs you',
              fg: c.textStrong,
              bg: c.accentTint,
              icon: Icon(LucideIcons.hand, size: 12, color: c.accent),
            )
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text('Save it here?', style: IcType.heading(17, color: c.textStrong)),
          if (d.why != null) ...[
            const SizedBox(height: 4),
            Text(d.why!, style: IcType.text(13, color: c.textMuted, height: 1.4)),
          ],
          const SizedBox(height: 10),
          editableRow(
            label: 'Name',
            onEdit: pending && !_editingName ? _startName : null,
            onKey: pending && !_editingName ? () => _startName(byKey: true) : null,
            value: Row(
              children: [
                Icon(d.icon, size: 15, color: c.accent),
                const SizedBox(width: 8),
                Expanded(
                  child: _editingName
                      ? Focus(
                          onKeyEvent: (_, e) {
                            if (e is KeyDownEvent && e.logicalKey == LogicalKeyboardKey.escape) {
                              _name.text = d.fileName;
                              _commitName();
                              return KeyEventResult.handled;
                            }
                            return KeyEventResult.ignored;
                          },
                          child: Field(
                            controller: _name,
                            focusNode: _focus,
                            style: IcType.text(13.5, weight: FontWeight.w600, color: c.textStrong),
                            onSubmitted: (_) => _commitName(),
                          ),
                        )
                      : Text(
                          d.fileName,
                          style: IcType.text(13.5, weight: FontWeight.w600, color: c.textStrong),
                        ),
                ),
                if (pending && !_editingName) Icon(LucideIcons.pencil, size: 13, color: c.textMuted),
              ],
            ),
          ),
          editableRow(
            label: 'Folder',
            onEdit: pending ? () => setState(() => _foldersOpen = !_foldersOpen) : null,
            value: Row(
              children: [
                Icon(LucideIcons.folder, size: 15, color: c.accent),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    d.folder,
                    style: IcType.text(13.5, weight: FontWeight.w600, color: c.textStrong),
                  ),
                ),
                if (pending) Icon(LucideIcons.chevronsUpDown, size: 14, color: c.textMuted),
              ],
            ),
          ),
          if (_foldersOpen && pending)
            Container(
              margin: const EdgeInsets.only(left: 64, top: 2, bottom: 6),
              padding: const EdgeInsets.all(4),
              decoration: BoxDecoration(
                color: c.surfaceCard,
                borderRadius: BorderRadius.circular(IcRadius.sm),
                border: Border.all(color: c.borderInput),
                boxShadow: IcShadow.md(c),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  for (final f in d.folders)
                    _FolderOption(
                      label: f,
                      selected: f == d.folder,
                      onTap: () => setState(() {
                        d.folder = f;
                        _foldersOpen = false;
                      }),
                    ),
                ],
              ),
            ),
          const SizedBox(height: 12),
          if (widget.state == SaveToState.saving)
            const CardWorking(text: 'Saving')
          else
            PromptOptions(
              options: [
                PromptOption(
                  'Save here',
                  onTap: () {
                    if (_editingName) _commitName(back: false);
                    widget.onApprove?.call();
                  },
                ),
                PromptOption('Don\'t save', onTap: widget.onCancel),
              ],
            ),
        ],
      ),
    );
  }
}

class _FolderOption extends StatefulWidget {
  const _FolderOption({required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  State<_FolderOption> createState() => _FolderOptionState();
}

class _FolderOptionState extends State<_FolderOption> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Stop(
      onActivate: widget.onTap,
      builder: (context, v) {
        final lit = _hover || v.selected;
        return MouseRegion(
          cursor: SystemMouseCursors.click,
          onEnter: (_) => setState(() => _hover = true),
          onExit: (_) => setState(() => _hover = false),
          child: GestureDetector(
            onTap: widget.onTap,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
              decoration: BoxDecoration(
                color: lit ? c.accent : const Color(0x00000000),
                borderRadius: BorderRadius.circular(IcRadius.xs),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      widget.label,
                      style: IcType.text(13, weight: FontWeight.w500, color: lit ? c.onAccent : c.textBody),
                    ),
                  ),
                  if (widget.selected) Icon(LucideIcons.check, size: 14, color: lit ? c.onAccent : c.accent),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}
