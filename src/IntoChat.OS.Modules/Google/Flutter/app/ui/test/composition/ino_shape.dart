// Wire JSON shaped like features/os/Ino/InoView.cs (sidebar + main column)
// and InoModels.FirstRun, with the chat and composer neurons it references
// served by the loader so the real transcript and composer render.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

const inoData = 'brain/apps/ino-data';

Map<String, dynamic> _node(
  int kind,
  String key, {
  String? text,
  String? detail,
  String? action,
  int? style,
  String? icon,
  bool enabled = true,
  List<Map<String, dynamic>>? children,
  List<num>? extents,
  Map<String, dynamic>? reference,
}) => {
  'kind': kind,
  'key': key,
  'text': ?text,
  'detail': ?detail,
  'action': ?action,
  'style': ?style,
  'icon': ?icon,
  'enabled': enabled,
  'children': children ?? const [],
  'extents': ?extents,
  'reference': ?reference,
};

Map<String, dynamic> _ref(String key, String kind, String name) =>
    _node(5, key, reference: {'kind': kind, 'name': name});

/// The model menu as InoModels.Menu builds it, "Auto" selected.
Map<String, dynamic> inoModelMenu() => _node(
  6,
  'model',
  text: 'Auto',
  icon: 'sparkle',
  children: [
    _node(
      1,
      'model-auto',
      text: 'Auto, cheapest model with tools',
      action: 'model:auto',
      style: 0,
    ),
    _node(
      1,
      'model-0',
      text: 'GPT-5 mini',
      action: 'model:gpt-5-mini',
      style: 1,
    ),
  ],
);

/// InoModels.FirstRun on a hosted brain: Cloud (soon) and Add an API key.
Map<String, dynamic> inoFirstRun() => _node(
  4,
  'first-run',
  text: 'How should Ino run?',
  detail: 'Ino needs a model before it can answer.',
  children: [
    _node(
      1,
      'setup-cloud',
      text: 'IntoChat Cloud',
      detail: 'Coming soon',
      action: '',
      style: 2,
      enabled: false,
    ),
    _node(0, 'setup-cloud-soon', text: 'Coming soon'),
    _ref('setup-keys', 'button', '$inoData/setup-keys'),
  ],
);

Map<String, dynamic> inoView({
  List<String> chats = const ['Deals last week', 'Notes for Erik'],
  bool menu = true,
  Map<String, dynamic>? firstRun,
}) {
  final rows = [
    for (var i = 0; i < chats.length; i++)
      _node(
        3,
        'chat-$i',
        extents: [0, 32],
        children: [
          _node(
            1,
            'open-$i',
            text: chats[i],
            action: 'chat:c-$i',
            style: i == 0 ? 1 : 2,
          ),
          _node(
            6,
            'menu-$i',
            text: 'Chat actions',
            icon: 'more',
            children: [
              _node(
                1,
                'rename-$i',
                text: 'Rename',
                action: 'rename:c-$i',
                style: 1,
                icon: 'edit',
              ),
              _node(
                1,
                'archive-$i',
                text: 'Archive',
                action: 'archive:c-$i',
                style: 1,
                icon: 'archive',
              ),
            ],
          ),
        ],
      ),
  ];
  final sidebar = _node(
    2,
    'sidebar',
    extents: [40, 56, 0],
    children: [
      _node(1, 'new', text: 'New chat', action: 'new', style: 0, icon: 'plus'),
      _ref('search', 'textfield', '$inoData/chat-search'),
      rows.isEmpty
          ? _node(0, 'chats-empty', text: 'Your chats will appear here.')
          : _node(9, 'chats', children: rows),
    ],
  );
  final header = _node(
    3,
    'header',
    extents: menu ? [0, 160] : [0],
    children: [
      _ref('status', 'text', '$inoData/status'),
      if (menu) inoModelMenu(),
    ],
  );
  final transcript = _ref('transcript', 'chat', '$inoData/transcript');
  final composer = _ref('composer', 'composer', '$inoData/composer');
  final main = firstRun == null
      ? _node(
          2,
          'main',
          extents: [48, 0, 152],
          children: [header, transcript, composer],
        )
      : _node(
          2,
          'main',
          extents: [48, 340, 0, 152],
          children: [header, firstRun, transcript, composer],
        );
  return _node(3, 'ino', extents: [260, 0], children: [sidebar, main]);
}

Map<String, dynamic> inoUser(String id, String text) => {
  'id': '$id/user',
  'role': 0,
  'text': text,
};

Map<String, dynamic> inoAnswer(
  String id,
  String text, {
  String status = 'completed',
  String? model,
  Map<String, dynamic>? app,
}) => {
  'id': '$id/assistant',
  'role': 1,
  'text': text,
  'status': status,
  'model': ?model,
  'app': ?app,
};

/// A conversation with a toolbar, an app card and a failed last turn.
List<Map<String, dynamic>> inoConversation() => [
  inoUser('r1', "Pull last week's deals as a spreadsheet."),
  inoAnswer(
    'r1',
    'I found 42 deals created or updated last week. The three largest are '
        'Havbris Shipping, Nordlys Energi and Fjord Retail.',
    model: 'gpt-5-mini',
  ),
  inoAnswer(
    'post/link-a',
    "Here's the app from your link.",
    app: {
      'app': 'notes',
      'title': 'Notes',
      'icon': 'file',
      'installed': true,
      'input': 'page=7',
    },
  ),
  inoAnswer(
    'post/link-b',
    "Here's the app from your link.",
    app: {
      'app': 'ledger',
      'title': 'Ledger',
      'icon': '',
      'installed': false,
      'input': '',
    },
  ),
  inoUser('r2', 'Now email it to Erik.'),
  inoAnswer('r2', '', status: 'failed'),
];

Future<void> pumpIno(
  WidgetTester tester,
  Map<String, dynamic> root, {
  List<Map<String, dynamic>> messages = const [],
  bool running = false,
  String draft = '',
  ThemeData? theme,
  Size size = const Size(1440, 900),
}) async {
  Map<String, dynamic> load(String kind, String name) => switch (kind) {
    'view' => {'generation': 1, 'version': 1, 'root': root},
    'chat' => {
      'projection': {
        'source': '$inoData/conversations/main',
        'generation': 1,
        'version': 1,
        'messages': messages,
        'actions': [
          if (running) {'id': 'stop/run-1', 'label': 'Stop', 'payload': '{}'},
        ],
      },
    },
    'composer' => {
      'context': '$inoData/conversations/main',
      'generation': 1,
      'label': 'Message Ino',
      'draft': draft,
      'followedChat': '$inoData/transcript',
    },
    'text' => {'markdown': running ? 'Ino is answering' : 'Ino is ready'},
    'textfield' => {'label': 'Search chats', 'value': ''},
    'button' => {'label': 'Add an API key'},
    _ => const <String, dynamic>{},
  };
  await tester.pumpWidget(
    MaterialApp(
      debugShowCheckedModeBanner: false,
      theme: theme,
      home: Material(
        child: Align(
          alignment: Alignment.topLeft,
          child: SizedBox(
            width: size.width,
            height: size.height,
            child: NeuronView(
              kind: 'view',
              name: 'brain/apps/ino',
              load: (kind, name) async => load(kind, name),
              watch: (kind, name) => const Stream.empty(),
              onAction: (event) async {},
              onActivate: (activation) {},
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pump();
  await tester.pump();
}
