class ConnectionView {
  const ConnectionView({
    required this.connectorId,
    required this.connectionId,
    required this.authMethodId,
    required this.status,
    this.account,
    this.expiresAt,
    this.lastProbedAt,
    this.detail,
  });
  final String connectorId, connectionId, authMethodId, status;
  final String? account, detail;
  final DateTime? expiresAt, lastProbedAt;
  factory ConnectionView.fromJson(Map<String, dynamic> json) => ConnectionView(
    connectorId: json['connectorId'] as String,
    connectionId: json['connectionId'] as String,
    authMethodId: json['authMethodId'] as String? ?? '',
    status: json['status'] as String? ?? 'Disconnected',
    account: json['account'] as String?,
    expiresAt: json['expiresAt'] == null
        ? null
        : DateTime.parse(json['expiresAt'] as String),
    lastProbedAt: json['lastProbedAt'] == null
        ? null
        : DateTime.parse(json['lastProbedAt'] as String),
    detail: json['detail'] as String?,
  );
}
