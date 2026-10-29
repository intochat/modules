import 'package:flutter/material.dart';

import 'theme/ui_icon.dart';

// The dock's icon vocabulary: a toolbar entry names one of these or a file in its bundle.
IconData? shellIcon(String name) => switch (name) {
  'play' => Icons.play_arrow_rounded,
  'pause' => Icons.pause_rounded,
  'stop' => Icons.stop_rounded,
  'mic' => Icons.mic_rounded,
  'send' => Icons.send_rounded,
  'volume' => Icons.volume_up_rounded,
  'backspace' => Icons.backspace_outlined,
  'clear' => Icons.clear_rounded,
  'add' => Icons.add_rounded,
  'refresh' => Icons.refresh_rounded,
  'search' => Icons.search_rounded,
  'settings' => Icons.settings_outlined,
  'home' => Icons.home_outlined,
  'folder' => Icons.folder_outlined,
  'circle' => Icons.circle_outlined,
  'hub' => Icons.hub_outlined,
  'ino' => Icons.auto_awesome_rounded,
  _ => uiIcon(name),
};
