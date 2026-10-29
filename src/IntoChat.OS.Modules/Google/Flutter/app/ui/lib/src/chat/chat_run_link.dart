import 'package:flutter/widgets.dart';

/// Links a chat renderer with the composer renderer that follows it (the
/// composer's `followedChat`). The chat neuron owns the run: it publishes its
/// Stop here so the composer can show Stop in place of Send, and the composer
/// registers how to send a message again for the chat's Retry.
abstract final class ChatRunLinks {
  /// Chat name -> Stop for its active run.
  static final stops = ValueNotifier<Map<String, VoidCallback>>(const {});
  static final _resend = <String, Future<void> Function(String)>{};

  /// Ticks after a composer starts or stops following a chat, so the chat
  /// renderer re-decides where Stop goes. It ticks after the frame, because
  /// composers follow while the tree is building.
  static final follows = ValueNotifier<int>(0);
  static void _changed() =>
      WidgetsBinding.instance.addPostFrameCallback((_) => follows.value++);

  static void publishStop(String chat, VoidCallback? stop) {
    final current = stops.value[chat];
    if (stop == null ? current == null : identical(current, stop)) return;
    final next = Map<String, VoidCallback>.of(stops.value);
    if (stop == null) {
      next.remove(chat);
    } else {
      next[chat] = stop;
    }
    stops.value = next;
  }

  static void follow(String chat, Future<void> Function(String) resend) {
    _resend[chat] = resend;
    _changed();
  }

  static void unfollow(String chat, Future<void> Function(String) resend) {
    if (_resend[chat] == resend) {
      _resend.remove(chat);
      _changed();
    }
  }

  /// True while a composer follows [chat]: the chat then leaves Stop to it.
  static bool followed(String chat) => _resend.containsKey(chat);

  /// Sends [text] again through the composer following [chat], if any.
  static Future<void> Function(String)? resend(String chat) => _resend[chat];
}
