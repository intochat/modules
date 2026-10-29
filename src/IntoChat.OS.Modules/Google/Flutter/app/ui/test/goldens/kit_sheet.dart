import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:intochat_os_ui/src/components/button/ui_neuron_select.dart';

/// A fixed sheet of kit components for golden comparison across palettes,
/// mirroring the Figma `ComponentsPage` sections. Kit widgets are used
/// directly; menu, modal, toast, list, badge, avatar and divider are value
/// nodes rendered through [NeuronView]. Each overlay demo gets its own
/// [Overlay] so the open menu, modal and toast stay inside their section.
class KitSheet extends StatelessWidget {
  const KitSheet({super.key});

  /// Semantics label of the menu trigger the golden test opens.
  static const menuTrigger = 'Chat actions';

  static Map<String, dynamic> _button(
    String key,
    String text, {
    int style = 0,
    String? icon,
  }) => {
    'kind': 1,
    'key': key,
    'text': text,
    'action': key,
    'style': style,
    'icon': icon,
  };

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    Widget section(String title, List<Widget> children) => Padding(
      padding: const EdgeInsets.only(bottom: 18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: type.meta),
          const SizedBox(height: 8),
          ...children,
        ],
      ),
    );
    Widget bubble(String text, {required bool user}) => Align(
      alignment: user ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        decoration: BoxDecoration(
          color: user ? c.userBubble : c.assistant,
          border: user ? null : Border.all(color: c.assistantBorder),
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(18),
            topRight: const Radius.circular(18),
            bottomLeft: Radius.circular(user ? 18 : 6),
            bottomRight: Radius.circular(user ? 6 : 18),
          ),
        ),
        child: Text(
          text,
          style: type.body.copyWith(color: user ? c.userText : c.assistantText),
        ),
      ),
    );

    const gap = SizedBox(width: 10, height: 10);
    final left = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Heading 800 Ag', style: type.heading),
        const SizedBox(height: 6),
        Text('Body 400 - the quick brown fox jumps', style: type.body),
        Text('META 500 - 12:04 - run-42', style: type.meta),
        const SizedBox(height: 18),
        section('BUTTONS', [
          Wrap(
            spacing: 10,
            runSpacing: 10,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              UiKitButton(label: 'Continue', onPressed: () {}),
              UiKitButton(
                label: 'Manage',
                style: UiKitButtonStyle.secondary,
                onPressed: () {},
              ),
              UiKitButton(
                label: 'Ghost',
                style: UiKitButtonStyle.ghost,
                onPressed: () {},
              ),
              UiKitButton(
                label: 'Delete',
                style: UiKitButtonStyle.danger,
                onPressed: () {},
              ),
              UiKitButton(
                label: 'Read more',
                style: UiKitButtonStyle.link,
                onPressed: () {},
              ),
              UiKitButton(label: 'New chat', icon: 'plus', onPressed: () {}),
              UiKitButton(label: 'Small', compact: true, onPressed: () {}),
              const UiKitButton(label: 'Disabled'),
            ],
          ),
        ]),
        section('INPUTS', [
          const UiKitField(label: 'Company', placeholder: 'Nordlys AS'),
          gap,
          UiKitField(
            label: 'First name',
            controller: TextEditingController(text: 'Anna'),
          ),
          gap,
          UiKitField(
            label: 'Work email',
            controller: TextEditingController(text: 'anna@'),
            error: 'Enter a complete email address.',
          ),
        ]),
        section('SELECT', [
          UiNeuronSelect(
            label: 'Language',
            options: const [
              {'id': 'en', 'label': 'English'},
              {'id': 'de', 'label': 'Deutsch'},
            ],
            selected: 'en',
            onSelected: (_) {},
          ),
        ]),
      ],
    );

    final middle = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        section('CARD', [
          UiKitSurface(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Quarterly report', style: type.cardTitle),
                const SizedBox(height: 4),
                Text('Updated 2 hours ago', style: type.bodyMuted),
              ],
            ),
          ),
        ]),
        section('TOGGLE / PROGRESS', [
          UiToggle(label: 'Weekly summary', on: true, onChanged: (_) {}),
          UiToggle(label: 'Auto-sync', on: false, onChanged: (_) {}),
          const SizedBox(height: 6),
          const UiProgress(
            determinate: true,
            value: 0.4,
            label: 'Uploading 40%',
          ),
        ]),
        section('BADGES / AVATAR / DIVIDER', [
          const SizedBox(
            height: 64,
            child: _ValueTree({
              'kind': 2,
              'key': 'feedback',
              'children': [
                {
                  'kind': 3,
                  'key': 'badges',
                  'gap': 8,
                  'extents': [100, 56, 76, 60, 34],
                  'children': [
                    {'kind': 10, 'key': 'b0', 'text': 'Coming soon'},
                    {'kind': 10, 'key': 'b1', 'text': 'Done', 'tone': 1},
                    {'kind': 10, 'key': 'b2', 'text': 'Pending', 'tone': 2},
                    {'kind': 10, 'key': 'b3', 'text': 'Error', 'tone': 3},
                    {'kind': 11, 'key': 'a', 'text': 'AB'},
                  ],
                },
                {'kind': 12, 'key': 'd', 'text': 'or'},
              ],
            }),
          ),
        ]),
        section('LIST', [
          const SizedBox(
            height: 130,
            child: _ValueTree({
              'kind': 9,
              'key': 'list',
              'children': [
                {'kind': 0, 'key': 'r0', 'text': 'Q4 pipeline by owner'},
                {'kind': 0, 'key': 'r1', 'text': 'Havbris business card'},
                {'kind': 0, 'key': 'r2', 'text': 'Deals last week'},
              ],
            }),
          ),
        ]),
        section('CHAT', [
          bubble("Pull last week's deals.", user: true),
          bubble('I found 42 deals created or updated last week.', user: false),
        ]),
      ],
    );

    final right = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        section('MENU', [
          SizedBox(
            height: 230,
            child: _ValueTree({
              'kind': 6,
              'key': 'menu',
              'text': menuTrigger,
              'icon': 'more-horizontal',
              'children': [
                {'kind': 0, 'key': 'label', 'text': 'Chat'},
                _button('archive', 'Archive', icon: 'archive'),
                _button('settings', 'Account settings', icon: 'settings'),
                {'kind': 12, 'key': 'sep'},
                _button('signout', 'Sign out', icon: 'log-out'),
              ],
            }),
          ),
        ]),
        section('MODAL', [
          SizedBox(
            height: 330,
            child: _ValueTree({
              'kind': 7,
              'key': 'confirm',
              'text': 'Disconnect Salesforce?',
              'detail': 'Your chats stay.',
              'children': [
                _button('disconnect', 'Disconnect', style: 3),
                _button('cancel', 'Cancel', style: 1),
              ],
            }),
          ),
        ]),
        section('TOAST', [
          SizedBox(
            height: 64,
            child: _ValueTree({
              'kind': 8,
              'key': 'toast',
              'text': 'Chat archived',
              'children': [_button('undo', 'Undo')],
            }),
          ),
        ]),
      ],
    );

    return ColoredBox(
      color: c.page,
      child: Padding(
        padding: const EdgeInsets.all(28),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: left),
            const SizedBox(width: 32),
            Expanded(child: middle),
            const SizedBox(width: 32),
            Expanded(child: right),
          ],
        ),
      ),
    );
  }
}

/// A value-tree root rendered through [NeuronView] inside a private
/// [Overlay], so overlay kinds draw within this box.
class _ValueTree extends StatelessWidget {
  const _ValueTree(this.root);
  final Map<String, dynamic> root;

  @override
  Widget build(BuildContext context) => ClipRect(
    child: Overlay(
      initialEntries: [
        OverlayEntry(
          builder: (_) => NeuronView(
            kind: 'view',
            name: root['key'] as String,
            load: (kind, name) async => {
              'generation': 1,
              'version': 1,
              'root': root,
            },
            watch: (kind, name) => const Stream.empty(),
            onAction: (event) async {},
          ),
        ),
      ],
    ),
  );
}
