/// Money is micro-credits: 1 credit = 1 USD / 1_000_000. Display as dollars.
class ComputeSummary {
  const ComputeSummary({
    required this.balanceMicros,
    required this.spentMicros,
    this.rows = const [],
    this.knownListPriceMicros,
    this.unpricedCount,
    this.pendingCount,
    this.unknownCount,
  });
  final int balanceMicros, spentMicros;
  final List<ComputeRow> rows;
  final int? knownListPriceMicros, unpricedCount, pendingCount, unknownCount;
  factory ComputeSummary.fromJson(Map<String, dynamic> json) => ComputeSummary(
    balanceMicros: (json['balanceMicros'] as num?)?.toInt() ?? 0,
    spentMicros: (json['spentMicros'] as num?)?.toInt() ?? 0,
    knownListPriceMicros: (json['knownListPriceMicros'] as num?)?.toInt(),
    unpricedCount: (json['unpricedCount'] as num?)?.toInt(),
    pendingCount: (json['pendingCount'] as num?)?.toInt(),
    unknownCount: (json['unknownCount'] as num?)?.toInt(),
    rows: [
      for (final row in json['rows'] as List? ?? const [])
        ComputeRow.fromJson(Map<String, dynamic>.from(row as Map)),
    ],
  );
}

class ComputeRow {
  const ComputeRow({
    required this.key,
    required this.micros,
    this.count = 0,
    this.knownListPriceMicros,
    this.unpricedCount,
    this.pendingCount,
    this.unknownCount,
  });
  final String key;
  final int micros, count;
  final int? knownListPriceMicros, unpricedCount, pendingCount, unknownCount;
  factory ComputeRow.fromJson(Map<String, dynamic> json) => ComputeRow(
    key: json['key'] as String,
    micros: (json['micros'] as num?)?.toInt() ?? 0,
    count: (json['count'] as num?)?.toInt() ?? 0,
    knownListPriceMicros: (json['knownListPriceMicros'] as num?)?.toInt(),
    unpricedCount: (json['unpricedCount'] as num?)?.toInt(),
    pendingCount: (json['pendingCount'] as num?)?.toInt(),
    unknownCount: (json['unknownCount'] as num?)?.toInt(),
  );
}

class ComputeCost {
  const ComputeCost({
    required this.traceId,
    required this.costId,
    required this.kind,
    required this.item,
    this.quantity = 0,
    required this.unit,
    required this.micros,
    this.synapse,
    this.app,
    this.neuron,
    this.at,
    this.listPriceMicros,
    this.estimatedListPriceMicros,
    this.pricingStatus,
    this.tariffVersion,
    this.fundingKind,
    this.outcome,
    this.invocationId,
    this.attemptState,
    this.providerRequestId,
    this.chargePolicy,
  });
  final String traceId, costId, kind, item, unit;
  final double quantity;
  final int micros;
  final String? synapse, app, neuron;
  final DateTime? at;
  final int? listPriceMicros, estimatedListPriceMicros;
  final String? pricingStatus,
      tariffVersion,
      fundingKind,
      outcome,
      invocationId,
      attemptState,
      providerRequestId,
      chargePolicy;

  bool get isPending => const {
    'reserved',
    'dispatchstarted',
    'outcomeknown',
    'uncertain',
  }.contains(attemptState);
  factory ComputeCost.fromJson(Map<String, dynamic> json) => ComputeCost(
    traceId: json['traceId'] as String,
    costId: json['costId'] as String,
    kind: json['kind'] as String,
    item: json['item'] as String,
    quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
    unit: json['unit'] as String,
    micros: (json['micros'] as num?)?.toInt() ?? 0,
    synapse: json['synapse'] as String?,
    app: json['app'] as String?,
    neuron: json['neuron'] as String?,
    at: DateTime.tryParse(json['at'] as String? ?? ''),
    listPriceMicros: (json['listPriceMicros'] as num?)?.toInt(),
    estimatedListPriceMicros: (json['estimatedListPriceMicros'] as num?)
        ?.toInt(),
    pricingStatus: json['pricingStatus'] as String?,
    tariffVersion: json['tariffVersion'] as String?,
    fundingKind: json['fundingKind'] as String?,
    outcome: json['outcome'] as String?,
    invocationId: json['invocationId'] as String?,
    attemptState: json['attemptState'] as String?,
    providerRequestId: json['providerRequestId'] as String?,
    chargePolicy: json['chargePolicy'] as String?,
  );
}

class ComputeTracePage {
  const ComputeTracePage({
    this.rows = const [],
    this.chargedMicros = 0,
    this.knownListPriceMicros = 0,
    this.unpricedCount = 0,
    this.pendingCount = 0,
    this.unknownCount = 0,
    this.snapshotWatermark = 0,
    this.continuationToken,
    this.status = 'detailunavailable',
  });
  final List<ComputeCost> rows;
  final int chargedMicros,
      knownListPriceMicros,
      unpricedCount,
      pendingCount,
      unknownCount,
      snapshotWatermark;
  final String? continuationToken;
  final String status;
  factory ComputeTracePage.fromJson(Map<String, dynamic> json) =>
      ComputeTracePage(
        rows: [
          for (final row in json['rows'] as List? ?? const [])
            ComputeCost.fromJson(Map<String, dynamic>.from(row as Map)),
        ],
        chargedMicros: (json['chargedMicros'] as num?)?.toInt() ?? 0,
        knownListPriceMicros:
            (json['knownListPriceMicros'] as num?)?.toInt() ?? 0,
        unpricedCount: (json['unpricedCount'] as num?)?.toInt() ?? 0,
        pendingCount: (json['pendingCount'] as num?)?.toInt() ?? 0,
        unknownCount: (json['unknownCount'] as num?)?.toInt() ?? 0,
        snapshotWatermark: (json['snapshotWatermark'] as num?)?.toInt() ?? 0,
        continuationToken: json['continuationToken'] as String?,
        status: json['status'] as String? ?? 'detailunavailable',
      );
}
