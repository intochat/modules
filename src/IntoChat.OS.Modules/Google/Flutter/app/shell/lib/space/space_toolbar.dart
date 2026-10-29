import 'dart:ui';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';

import '../workspace/dock/dock.dart';
import '../workspace/home/app_icon.dart';

class SpaceToolbar extends StatelessWidget {
  const SpaceToolbar({
    super.key,
    required this.entries,
    required this.onHome,
    required this.onRequest,
    this.onBack,
    this.onSettings,
    this.loadIcon,
    this.icon,
    this.iconRoute,
    this.menu,
  });
  final List<AppToolbarEntry> entries;
  final VoidCallback onHome;
  final VoidCallback? onBack, onSettings;
  final DockRequest onRequest;
  final IconLoader? loadIcon;
  final String? icon, iconRoute;
  final Widget? menu;
  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 10),
      child: DecoratedBox(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(18),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: isDark ? 0.42 : 0.10),
              blurRadius: 24,
              offset: const Offset(0, 8),
            ),
          ],
        ),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: BackdropFilter(
            filter: ImageFilter.blur(sigmaX: 24, sigmaY: 24),
            child: Material(
              color: c.surface.withValues(alpha: isDark ? 0.82 : 0.92),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(18),
                side: BorderSide(
                  color: isDark
                      ? Colors.white.withValues(alpha: 0.14)
                      : c.hairline,
                ),
              ),
              child: SizedBox(
                height: 56,
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 8),
                  child: Row(
                    children: [
                      IconButton(
                        tooltip: 'Home',
                        onPressed: onHome,
                        icon: const Icon(Icons.home_outlined),
                      ),
                      if (onBack != null)
                        IconButton(
                          tooltip: 'Back',
                          onPressed: onBack,
                          icon: const Icon(Icons.arrow_back),
                        ),
                      Expanded(
                        child: SingleChildScrollView(
                          scrollDirection: Axis.horizontal,
                          child: Dock(
                            app: '',
                            manifest: AppManifest(
                              title: '',
                              icon: icon,
                              description: '',
                              operations: const [],
                              toolbar: entries,
                            ),
                            onRequest: onRequest,
                            loadIcon: loadIcon,
                            iconRoute: iconRoute,
                          ),
                        ),
                      ),
                      if (onSettings != null)
                        IconButton(
                          tooltip: 'Settings',
                          onPressed: onSettings,
                          icon: const Icon(Icons.settings_outlined),
                        ),
                      ?menu,
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
