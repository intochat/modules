namespace IntoChat.OS.Modules.Flutter;

internal static class CompositionValidation
{
    public static void Children(IReadOnlyList<UiChildRef> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        if (children.Count > 128) { throw new ArgumentException("A node supports at most 128 children."); }
        foreach (var child in children)
        {
            if (child is not null && child.Kind is not ("viewport" or "surface" or "layout" or "collection" or "imagecanvas" or "card" or "button" or "textfield" or "tabs" or "text" or UIVocabulary.TableType or UIVocabulary.ChartType or UIVocabulary.SceneType or UIVocabulary.ChatType or UIVocabulary.ComposerType or UIVocabulary.SelectType or UIVocabulary.FileInputType or UIVocabulary.VoiceInputType or UIVocabulary.WebBrowserType or UIVocabulary.FormType or UIVocabulary.ToggleType or UIVocabulary.ProgressType or UIVocabulary.InfoBarType or UIVocabulary.ImageType or UIVocabulary.ExpanderType)) { throw new ArgumentException("Unsupported composition child kind."); }
            if (child is null || string.IsNullOrWhiteSpace(child.Kind) || string.IsNullOrWhiteSpace(child.Name) || child.Name.Length > 512) { throw new ArgumentException("Children need a kind and stable name."); }
        }
        if (children.Distinct().Count() != children.Count) { throw new ArgumentException("Child references must be unique."); }
    }
}
