/// The chat surface's user-visible copy. The wording is owned by
/// features/os/Ino/InoCopy.cs (INT-20); a kernel fact pins these to it.
abstract final class ChatCopy {
  static const empty = 'Ask Ino anything, or open an app from the dock.';
  static const failed = "Ino couldn't answer. Try again.";
  static const responding = 'Responding…';
  static const stepFailed = "This step didn't work.";
  static const stopped = 'Response stopped.';
  static const interrupted =
      'Response interrupted. You can send another message.';
  static const copy = 'Copy';
  static const copied = 'Copied';
  static const retry = 'Retry';
  static const send = 'Send';
  static const stop = 'Stop';
  static const jumpToLatest = 'Jump to latest';
  static const open = 'Open';
  static const notInstalled = 'Not installed';
  static const install = 'Install';
  static const answeredBy = 'Answered by';
}
