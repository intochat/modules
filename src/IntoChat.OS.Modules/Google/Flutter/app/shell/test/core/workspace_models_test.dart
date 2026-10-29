import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('typed workspace preserves view identity context and pins', () {
    final state = WorkspaceSnapshot.fromJson({
      'revision': 3,
      'path': ['brain'],
      'open': [],
      'openViews': [
        {
          'id': 'person',
          'app': 'brain/apps/profile',
          'variant': 'full',
          'context': 'person:alice',
        },
      ],
      'focusedViewId': 'person',
      'pins': [
        {
          'id': 'calc',
          'app': 'brain/apps/calculator',
          'variant': 'compact',
          'context': '',
        },
      ],
    });
    expect(state.openViews.single.context, 'person:alice');
    expect(state.focusedViewId, 'person');
    expect(state.pins.single.variant, 'compact');
    expect(() => state.openViews.clear(), throwsUnsupportedError);
  });
  test('workspace snapshot retains navigation path and open apps', () {
    final state = WorkspaceSnapshot.fromJson({
      'revision': 4,
      'path': ['brain', 'brain/apps/ino', 'brain/apps/files'],
      'open': ['brain/apps/ino', 'brain/apps/files'],
    });
    expect(state.revision, 4);
    expect(state.path.last, 'brain/apps/files');
    expect(state.open, ['brain/apps/ino', 'brain/apps/files']);
    expect(() => state.path.clear(), throwsUnsupportedError);
    expect(() => state.open.clear(), throwsUnsupportedError);
  });

  test('query table contract preserves scalar cells and server counts', () {
    final table = TableSnapshot.fromJson({
      'id': 'table',
      'title': 'Leads',
      'revision': 2,
      'kind': 'table',
      'columns': [
        {'id': 'id', 'label': 'Id', 'type': 'number'},
        {'id': 'name', 'label': 'Name', 'type': 'text'},
        {'id': 'date', 'label': 'Date', 'type': 'date'},
      ],
      'rows': [
        {
          'id': '55',
          'cells': [55, null, '2026-09-20'],
        },
      ],
      'filters': [
        {'columnId': 'id', 'operator': 'gte', 'value': 55},
      ],
      'sort': {'columnId': 'id', 'descending': true},
      'visibleColumns': ['id', 'name', 'date'],
      'totalRows': 60,
      'filteredRows': 6,
      'offset': 0,
      'limit': 25,
    });
    expect(table.rows.single.cells, [55, null, '2026-09-20']);
    expect(table.totalRows, 60);
    expect(table.filteredRows, 6);
    expect(table.filters.single.value, 55);
    expect(table.sort!.descending, true);
  });
}
