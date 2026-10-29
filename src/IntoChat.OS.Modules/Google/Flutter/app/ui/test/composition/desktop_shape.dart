// Wire JSON shaped like features/os/Desktop/DesktopLayout.cs and
// features/os/Ino/InoView.cs; references become text so no child loads run.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

Map<String, dynamic> shapeButton(String key, String text) => {
  'kind': 1,
  'key': key,
  'text': text,
  'action': key,
  'style': 1,
};

Map<String, dynamic> _text(String key, String text) => {
  'kind': 0,
  'key': key,
  'text': text,
};

Map<String, dynamic> _row(
  String key,
  List<Map<String, dynamic>> children, {
  List<num>? extents,
  num gap = 0,
}) => {
  'kind': 3,
  'key': key,
  'children': children,
  'extents': ?extents,
  'gap': gap,
};

Map<String, dynamic> _column(
  String key,
  List<Map<String, dynamic>> children, {
  List<num>? extents,
  num gap = 0,
}) => {
  'kind': 2,
  'key': key,
  'children': children,
  'extents': ?extents,
  'gap': gap,
};

/// Ino's main view: a 40 px header row whose button has a 160 px slot.
Map<String, dynamic> inoShape() => _column(
  'main',
  [
    _row(
      'header',
      [_text('status', 'Ino is ready'), shapeButton('new', 'New conversation')],
      extents: [0, 160],
    ),
    _text('transcript', 'Transcript'),
    _text('composer', 'Composer'),
  ],
  extents: [40, 0, 152],
);

Map<String, dynamic> _iconButton(String key, String icon, String label) => {
  'kind': 1,
  'key': key,
  'text': label,
  'action': key,
  'style': 2,
  'icon': icon,
};

Map<String, dynamic> _tab(String key, String label, bool active) => {
  'kind': 1,
  'key': key,
  'text': label,
  'action': key,
  'style': active ? 0 : 2,
};

/// A window mirroring DesktopLayout.Window: a 40 px header, a 38 px tab strip
/// (Tabs kind 13) and the active tab's content filling the rest.
Map<String, dynamic> _window(
  String id,
  String title, {
  int tabs = 1,
  bool pin = true,
}) => _column(
  'frame-$id',
  [
    _row(
      'chrome-$id',
      [
        {
          'kind': 1,
          'key': 'focus-$id',
          'text': title,
          'action': 'focus:$id',
          'style': 2,
        },
        _iconButton('newtab-$id', 'plus', 'New tab'),
        _iconButton('star-$id', 'star', 'Add to favourites'),
        if (pin) _iconButton('pin-$id', 'pin', 'Pin'),
        _iconButton('close-$id', 'x', 'Close tab'),
      ],
      gap: 4,
      extents: [0, 32, 32, if (pin) 32, 32],
    ),
    {
      'kind': 13,
      'key': 'tabs-$id',
      'children': [
        for (var i = 0; i < tabs; i++)
          _tab('tab-$id-$i', i == 0 ? title : 'Tab ${i + 1}', i == 0),
      ],
    },
    id == 'ino' ? inoShape() : _text('content-$id', '$title content'),
  ],
  gap: 0,
  extents: [40, 38, 0],
);

/// The desktop: windows above the 68 px dock, mirroring DesktopLayout.Render.
/// [narrow] shows only the focused window and collapses favourites to a menu;
/// [toast] adds the 48 px refusal row.
Map<String, dynamic> desktopShape({
  bool narrow = false,
  int favourites = 0,
  String? toast,
  int filesTabs = 3,
}) {
  final windows = [
    if (!narrow) _window('ino', 'Ino', pin: false),
    _window('files', 'Files', tabs: filesTabs),
  ];
  final favouriteButtons = [
    for (var i = 0; i < favourites; i++)
      {
        'kind': 1,
        'key': 'fav-$i',
        'text': i.isEven ? 'Notes' : 'Report',
        'action': 'fav-$i',
        'style': 1,
        'icon': i.isEven ? 'grid' : 'list',
      },
  ];
  final favouriteWidths = [
    for (final button in favouriteButtons)
      dockWidth(button['text']! as String, icon: true),
  ];
  final usage = _text('usage', 'Usage');
  final avatar = shapeButton('avatar', 'Vlad');
  final settings = shapeButton('more-settings', 'Settings');
  final classic = shapeButton('more-classic', 'Classic layout');
  // DesktopLayout.Packed: label-sized slots only.
  final apps = _row(
    'apps',
    [
      {
        'kind': 1,
        'key': 'dock-ino',
        'text': 'Ino',
        'action': 'open:ino',
        'style': 0,
        'icon': 'ino',
      },
      if (favourites > 0)
        narrow
            ? {
                'kind': 6,
                'key': 'favourites',
                'text': 'Favourites',
                'icon': 'star',
                'children': favouriteButtons,
              }
            : _row(
                'favourites',
                favouriteButtons,
                gap: 4,
                extents: favouriteWidths,
              ),
      {
        'kind': 1,
        'key': 'dock-files',
        'text': 'Files',
        'action': 'open:files',
        'style': 2,
      },
      _iconButton('newtab', 'plus', 'New tab'),
      {
        'kind': 1,
        'key': 'gallery',
        'text': 'Apps',
        'action': 'gallery',
        'style': 2,
        'icon': 'grid',
      },
    ],
    gap: 4,
    extents: [
      dockWidth('Ino', icon: true),
      if (favourites > 0)
        narrow
            ? 40
            : favouriteWidths.fold<num>(0, (sum, w) => sum + w) +
                  4 * (favourites - 1),
      dockWidth('Files'),
      40,
      40,
    ],
  );
  return _column(
    'desktop',
    [
      _column('windows', [_row('windows-0', windows)], extents: [0]),
      if (toast != null) {'kind': 8, 'key': 'toast', 'text': toast, 'tone': 3},
      narrow
          ? _row(
              'dock',
              [
                apps,
                {
                  'kind': 6,
                  'key': 'more',
                  'text': 'More',
                  'icon': 'more-horizontal',
                  'children': [usage, avatar, settings, classic],
                },
              ],
              extents: [0, 40],
            )
          : _row(
              'dock',
              [
                apps,
                _row(
                  'right',
                  [
                    usage,
                    avatar,
                    _row('more', [settings, classic], gap: 4),
                  ],
                  gap: 4,
                  extents: [72, 72, 0],
                ),
              ],
              extents: [0, 360],
            ),
    ],
    extents: [0, if (toast != null) 48, 68],
  );
}

/// DesktopLayout.Width: the kit's 48 px button for [label].
num dockWidth(String label, {bool icon = false}) =>
    36 + 14 * label.length + (icon ? 28 : 0);

/// Pumps [root] as a view value at [size].
Future<void> pumpShape(
  WidgetTester tester,
  Map<String, dynamic> root, {
  ThemeData? theme,
  Size size = const Size(1280, 800),
}) async {
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
              name: 'desktop',
              load: (kind, name) async => {
                'generation': 1,
                'version': 1,
                'root': root,
              },
              watch: (kind, name) => const Stream.empty(),
              onAction: (event) async {},
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pump();
  await tester.pump();
}
