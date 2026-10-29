import 'dart:convert';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../components/kit/ui_kit_icon_button.dart';
import '../components/kit/ui_kit_surface.dart';
import '../theme/ui_colors.dart';
import '../theme/ui_shadows.dart';
import '../theme/ui_theme.dart';
import '../theme/ui_tokens.dart';
import '../voice_input/ui_voice_input.dart';
import 'chat_copy.dart';
import 'chat_run_link.dart';

class UiComposer extends StatefulWidget {
  const UiComposer({
    super.key,
    required this.state,
    required this.dispatch,
    this.enabled = true,
  });
  final Map<String, dynamic> state;
  final Future<void> Function(Map<String, dynamic>) dispatch;
  final bool enabled;

  @override
  State<UiComposer> createState() => _UiComposerState();
}

class _UiComposerState extends State<UiComposer> {
  late final _text = TextEditingController(
    text: widget.state['draft'] as String? ?? '',
  );
  final _focus = FocusNode();
  final _random = Random.secure();
  int _edits = 0;
  int _editVersion = 0;
  bool _sending = false;
  bool _unsaved = false;
  String? _captureId, _error;

  String _id() {
    final bytes = List<int>.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  Future<void> _event(
    String action, {
    String? value,
    String? id,
    String? mimeType,
  }) => widget.dispatch({
    'action': action,
    'context': widget.state['context'],
    'selectionRevision': widget.state['generation'],
    'value': value,
    'operationId': id,
    'mimeType': mimeType,
  });

  Future<void> _edit(String value) async {
    await _saveEdit(value);
  }

  Future<bool> _saveEdit(String value) async {
    final version = ++_editVersion;
    final context = widget.state['context'];
    final generation = widget.state['generation'];
    bool current() =>
        mounted &&
        version == _editVersion &&
        context == widget.state['context'] &&
        generation == widget.state['generation'];
    _edits++;
    _unsaved = true;
    try {
      await _event('edit', value: value);
      if (!current()) return false;
      setState(() {
        _unsaved = false;
        _error = null;
      });
      return true;
    } catch (error) {
      if (current()) setState(() => _error = '$error');
      return false;
    } finally {
      _edits--;
    }
  }

  Future<void> _submit() async {
    if (_sending || !widget.enabled || _text.text.trim().isEmpty) return;
    final value = _text.text;
    final context = widget.state['context'];
    final generation = widget.state['generation'];
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      // Retry an unacknowledged edit before asking the neuron to capture it.
      if (_unsaved) {
        if (!await _saveEdit(value)) return;
      }
      if (!mounted ||
          context != widget.state['context'] ||
          generation != widget.state['generation']) {
        return;
      }
      await _event('submit', value: value, id: _id());
    } catch (error) {
      if (mounted) setState(() => _error = '$error');
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  String? get _followed => _chatOf(widget.state);

  static String? _chatOf(Map<String, dynamic> state) =>
      switch (state['followedChat']) {
        final String chat when chat.isNotEmpty => chat,
        _ => null,
      };

  // The chat's Retry: put the message back and send it, unless a different
  // draft is in progress, which is never overwritten.
  Future<void> _resend(String text) async {
    if (!widget.enabled || _sending) return;
    if (_text.text.trim().isNotEmpty && _text.text != text) return;
    _text.value = TextEditingValue(
      text: text,
      selection: TextSelection.collapsed(offset: text.length),
    );
    if (!await _saveEdit(text)) return;
    await _submit();
  }

  @override
  void initState() {
    super.initState();
    if (_followed case final chat?) ChatRunLinks.follow(chat, _resend);
  }

  @override
  void didUpdateWidget(UiComposer oldWidget) {
    super.didUpdateWidget(oldWidget);
    final before = _chatOf(oldWidget.state);
    if (before != _followed) {
      if (before != null) ChatRunLinks.unfollow(before, _resend);
      if (_followed case final chat?) ChatRunLinks.follow(chat, _resend);
    }
    final draft = widget.state['draft'] as String? ?? '';
    if (_edits == 0 && !_unsaved && _text.text != draft) {
      _text.value = TextEditingValue(
        text: draft,
        selection: TextSelection.collapsed(offset: draft.length),
      );
    }
  }

  @override
  void dispose() {
    if (_followed case final chat?) ChatRunLinks.unfollow(chat, _resend);
    _text.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final voice = widget.state['voice'] as Map?;
    final status = voice?['status'];
    final transcribing = status == 'transcribing';
    final message =
        _error ??
        widget.state['error'] as String? ??
        voice?['error'] as String? ??
        switch (status) {
          'transcribing' => 'Transcribing… You can cancel or keep editing.',
          'ready' => 'Transcript ready. Review and edit it, then Send.',
          'cancelled' => 'Recording cancelled. Your draft is unchanged.',
          _ => null,
        };
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final type = UiType.of(context);
    final focused = _focus.hasFocus;
    final voiceButton = transcribing
        ? Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              SizedBox.square(
                dimension: 18,
                child: CircularProgressIndicator(
                  strokeWidth: 2,
                  color: c.muted,
                ),
              ),
              UiKitIconButton(
                icon: 'x',
                label: 'Cancel transcription',
                size: 38,
                iconSize: 18,
                onPressed: () =>
                    _event('voice-cancel', id: voice!['id'] as String),
              ),
            ],
          )
        : UiVoiceInput(
            key: ValueKey(widget.state['context']),
            enabled: widget.enabled,
            label: 'Record a voice draft',
            onBegin: () async {
              setState(() => _error = null);
              _captureId = _id();
              await _event('voice-begin', id: _captureId);
            },
            onCancel: () async {
              if (_captureId != null) {
                await _event('voice-cancel', id: _captureId);
              }
            },
            onCaptureError: (error) async {
              const message =
                  'Voice input is unavailable. Your draft is unchanged. '
                  'Check your microphone and connection, then try again or type your message.';
              if (mounted) setState(() => _error = message);
              if (_captureId != null) {
                await _event('voice-error', id: _captureId, value: message);
              }
            },
            onAudio: (audio, mimeType) => _event(
              'voice-audio',
              id: _captureId,
              value: base64Encode(audio),
              mimeType: mimeType,
            ),
          );
    // Stop replaces Send while the followed chat has a run (Figma
    // `.send-button.stop`); the chat neuron owns the run and its Stop.
    final send = ValueListenableBuilder<Map<String, VoidCallback>>(
      valueListenable: ChatRunLinks.stops,
      builder: (context, stops, _) {
        final stop = _followed == null ? null : stops[_followed];
        return stop != null
            ? UiKitIconButton(
                icon: 'square',
                label: ChatCopy.stop,
                style: UiKitIconButtonStyle.strong,
                size: 38,
                iconSize: 16,
                onPressed: widget.enabled ? stop : null,
              )
            : UiKitIconButton(
                icon: 'send',
                label: ChatCopy.send,
                style: UiKitIconButtonStyle.primary,
                size: 38,
                iconSize: 18,
                onPressed: widget.enabled && !_sending ? _submit : null,
              );
      },
    );
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 4, 8, 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Expanded(
            // Figma `.composer`: min(760px, 100%), centred.
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 760),
                child: Opacity(
                  opacity: widget.enabled ? 1 : 0.65,
                  child: Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: c.surface,
                      borderRadius: BorderRadius.circular(tokens.modalRadius),
                      border: Border.all(
                        color: focused ? c.accent : c.inputBorder,
                      ),
                      boxShadow: [
                        if (focused) BoxShadow(color: c.focus, spreadRadius: 3),
                        ...UiShadows.lg(uiKitShadowBase(context)),
                      ],
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        voiceButton,
                        const SizedBox(width: 4),
                        Expanded(
                          child: Focus(
                            onFocusChange: (_) => setState(() {}),
                            onKeyEvent: (_, event) {
                              if (event is KeyDownEvent &&
                                  event.logicalKey ==
                                      LogicalKeyboardKey.enter &&
                                  !HardwareKeyboard.instance.isShiftPressed &&
                                  !_text.value.composing.isValid) {
                                _submit();
                                return KeyEventResult.handled;
                              }
                              return KeyEventResult.ignored;
                            },
                            child: TextField(
                              controller: _text,
                              focusNode: _focus,
                              enabled: widget.enabled,
                              onChanged: _edit,
                              expands: true,
                              minLines: null,
                              maxLines: null,
                              maxLength: 32000,
                              textAlignVertical: TextAlignVertical.top,
                              keyboardType: TextInputType.multiline,
                              textInputAction: TextInputAction.newline,
                              cursorColor: c.accent,
                              style: type.body.copyWith(
                                color: c.strong,
                                height: 1.5,
                              ),
                              decoration: InputDecoration(
                                hintText:
                                    widget.state['label'] as String? ??
                                    'Message',
                                hintStyle: type.body.copyWith(color: c.muted),
                                counterText: '',
                                border: InputBorder.none,
                                enabledBorder: InputBorder.none,
                                focusedBorder: InputBorder.none,
                                disabledBorder: InputBorder.none,
                                filled: false,
                                isDense: true,
                                contentPadding: const EdgeInsets.symmetric(
                                  vertical: 9,
                                  horizontal: 6,
                                ),
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(width: 4),
                        send,
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
          if (message != null)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Tooltip(
                message: message,
                child: Text(
                  message,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  textAlign: TextAlign.center,
                  style: type.caption.copyWith(color: c.muted),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
