namespace IntoChat.OS.Modules.Microsoft.GitHub;

// Installation, Repository and App are the delivery's routing hints; the neuron accepts only its own.
public sealed record RepositoryEvent(
    string DeliveryId,
    string Event,
    string? Action,
    int? Number,
    string BodyHash,
    long? Installation = null,
    long? Repository = null,
    long? App = null);
