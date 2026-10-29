/// Every string Ino shows on its own: first run, empty states and errors.
library;

import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

abstract final class InoCopy {
  static const firstRunGreeting =
      'Hi, I’m Ino. Before I can help, I need a model to think with. You can change this later.';
  static const firstRunQuestion = 'How should Ino run?';
  static const firstRunCloud = 'IntoChat Cloud';
  static const firstRunCloudDetail =
      'Pay as you go from prepaid credits. You top up in Billing.';
  static const firstRunKey = 'My own API key';
  static const firstRunKeyDetail =
      'Anthropic, OpenAI, xAI or DeepSeek. You pay them directly.';
  static const firstRunLocal = 'On this computer';
  static const firstRunLocalDetail =
      'Ollama found with 2 models. Free and private, but slower.';
  static const firstRunCloudDone =
      'Done. I run on IntoChat Cloud and use your credits in Billing. Ask me anything, or try “push all Acme deals that slipped to next month”.';
  static const firstRunLocalDone =
      'Ready. Your answers stay on this computer. Local models are slower and some apps need a cloud model; you can switch in Settings › AI models.';
  static const firstRunKeySkipped = 'OK, no key for now. Pick another way:';
  static const firstRunHeld =
      'Got it. I’ll answer that as soon as you pick how I run.';

  static const emptySessionTitle = 'What do you need?';
  static const samplePrompts = [
    (LucideIcons.sheet, 'Pull last week’s deals as a spreadsheet'),
    (LucideIcons.clock, 'Which accounts had no activity in 60 days?'),
    (LucideIcons.mail, 'Draft a follow-up to my last meeting'),
    (
      LucideIcons.calendarClock,
      'Push all Acme deals that slipped to next month',
    ),
  ];
  static const composerPlaceholder = 'Ask anything.';
  static const todayEmpty = 'Nothing open. New sessions land here.';
  static const favouritesEmpty = 'Drag a session here to keep it on top';
  static const historyEmpty =
      'Nothing in History yet. Sessions you close land here.';
  static String historyNoMatch(String q) => 'No session called “$q”.';
}

class InoError {
  const InoError(
    this.key,
    this.icon,
    this.text, {
    this.action,
    this.working = false,
    this.when = '',
  });

  final String key;
  final IconData icon;
  final String text;
  final String? action;
  final bool working;
  final String when;
}

const inoErrors = [
  InoError(
    'model-timeout',
    LucideIcons.timerOff,
    'Claude Sonnet 5.5 didn’t answer in 60 seconds. Nothing changed.',
    action: 'Try again',
    when: 'The model took too long.',
  ),
  InoError(
    'model-busy',
    LucideIcons.clock,
    'Anthropic is busy. I’ll try again in 20 seconds.',
    action: 'Try now',
    working: true,
    when: 'The provider is rate limiting.',
  ),
  InoError(
    'key-rejected',
    LucideIcons.keyRound,
    'Anthropic rejected your API key. Replace it in Settings › API keys and I’ll carry on.',
    action: 'Replace key',
    when: 'Your own provider key is wrong or revoked.',
  ),
  InoError(
    'local-down',
    LucideIcons.laptop,
    'Ollama isn’t running on this computer. Start it, or switch to a cloud model.',
    action: 'Switch model',
    when: 'The local model server is off.',
  ),
  InoError(
    'out-of-credits',
    LucideIcons.wallet,
    'You’re out of credits, so I stopped halfway. Add credits and I’ll finish this.',
    action: 'Add credits',
    when: 'Credit balance hit zero.',
  ),
  InoError(
    'spend-limit',
    LucideIcons.gauge,
    'You hit your €100 monthly limit. Raise it in Billing, or wait until 1 November.',
    action: 'Open Billing',
    when: 'The monthly spend limit is reached.',
  ),
  InoError(
    'offline',
    LucideIcons.wifiOff,
    'You’re offline. I’ll send this when you’re back.',
    working: true,
    when: 'No network.',
  ),
  InoError(
    'signed-out',
    LucideIcons.plug,
    'Slack signed you out. Sign in again and I’ll pick up where I stopped.',
    action: 'Sign in again',
    when: 'A connection needs a new sign-in.',
  ),
  InoError(
    'not-allowed',
    LucideIcons.ban,
    'Your Salesforce connection doesn’t allow Delete, so I didn’t remove the 3 duplicates.',
    action: 'Open connection',
    when: 'The connection’s rights don’t cover the change.',
  ),
  InoError(
    'app-off',
    LucideIcons.powerOff,
    'Morning Brief is off. Turn it on and ask again.',
    action: 'Turn on',
    when: 'The app that does this is switched off.',
  ),
  InoError(
    'no-app',
    LucideIcons.layoutGrid,
    'None of your apps can read Tripletex yet. Paste a link to one and I’ll install it.',
    action: 'Install app',
    when: 'No installed app covers the request.',
  ),
  InoError(
    'app-failed',
    LucideIcons.triangleAlert,
    'Salesforce stopped answering after 12 of 14 changes. The other 2 didn’t go through.',
    action: 'Retry the 2',
    when: 'An app failed partway through a job.',
  ),
  InoError(
    'file-too-big',
    LucideIcons.fileWarning,
    'That file is 48 MB. I read files up to 25 MB.',
    when: 'An attachment is over the limit.',
  ),
  InoError(
    'file-type',
    LucideIcons.fileX,
    'I can’t read .pages files. Export it as PDF or Word and send it again.',
    when: 'An attachment type isn’t supported.',
  ),
  InoError(
    'too-long',
    LucideIcons.scrollText,
    'This session is too long for Claude Sonnet 5.5. I can start a new one with a summary of this.',
    action: 'New session with summary',
    when: 'The session no longer fits the model.',
  ),
  InoError(
    'unknown',
    LucideIcons.circleAlert,
    'Something broke on our side. Nothing changed. Try again in a minute.',
    action: 'Try again',
    when: 'Anything else.',
  ),
];

InoError inoError(String key) => inoErrors.firstWhere((e) => e.key == key);
