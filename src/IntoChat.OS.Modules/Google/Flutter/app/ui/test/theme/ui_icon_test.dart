import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/src/shell_icon.dart';
import 'package:intochat_os_ui/src/theme/ui_icon.dart';

const _figmaNames = [
  'eye',
  'eye-off',
  'check',
  'alert',
  'chevron',
  'sun',
  'moon',
  'monitor',
  'settings',
  'arrow-up-right',
  'panel-left',
  'plus',
  'search',
  'plug',
  'more-horizontal',
  'archive',
  'menu',
  'camera',
  'paperclip',
  'arrow-up',
  'square',
  'copy',
  'rotate',
  'link',
  'spreadsheet',
  'download',
  'external-link',
  'file',
  'image',
  'arrow-left',
  'arrow-down',
  'grid',
  'star',
  'code',
  'pin-off',
  'pin',
  'sort',
  'list',
  'clock',
  'fork',
  'filter',
  'columns',
  'grip',
  'trash',
  'local-machine',
  'user',
  'log-out',
  'x',
  'ino',
  'more',
  'edit',
];

void main() {
  test('every Figma icon name resolves', () {
    expect(_figmaNames.toSet().length, 51);
    for (final name in _figmaNames) {
      expect(uiIcon(name), isNotNull, reason: name);
    }
  });

  test('unknown name resolves to null', () {
    expect(uiIcon('no-such-icon'), isNull);
  });

  test('shellIcon keeps its mappings and falls back to uiIcon', () {
    expect(shellIcon('play'), isNotNull);
    expect(shellIcon('trash'), uiIcon('trash'));
    expect(shellIcon('no-such-icon'), isNull);
  });

  test('default size', () => expect(uiIconSize, 18));
}
