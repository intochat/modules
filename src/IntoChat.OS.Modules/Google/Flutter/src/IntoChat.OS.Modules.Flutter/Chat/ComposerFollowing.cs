namespace IntoChat.OS.Modules.Flutter.Chat;

// The reaction only queues this one-way turn, so a chat publishing its selection never waits for the composer to call back
// into it.
[DigitalBrain.Contracts.NeuronAlias("ui.composer-reconciliation"), DigitalBrain.Contracts.DefaultNeuronType(UIVocabulary.ComposerType)]
internal interface IComposerReconciliation
{
    [DigitalBrain.OneWayCall] Task Reconcile(string chat);
}

[DigitalBrain.Contracts.NeuronAlias("ui.composer-migration"), DigitalBrain.Contracts.DefaultNeuronType(UIVocabulary.ComposerType)]
public interface IComposerMigration
{
    Task AdoptFollow(string chat);
}
