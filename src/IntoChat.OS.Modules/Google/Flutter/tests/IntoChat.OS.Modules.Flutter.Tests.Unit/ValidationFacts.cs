using IntoChat.OS.Modules.Flutter.Button;

namespace IntoChat.OS.Modules.Flutter.Tests.Unit;

public sealed class ValidationFacts
{
    [Fact]
    public void ASetOfKnownUniqueChildrenIsAccepted()
        => CompositionValidation.Children([new("card", "a"), new("button", "b"), new(UIVocabulary.TableType, "t"), new("layout", "l")]);

    [Fact]
    public void NoChildrenIsAccepted()
        => CompositionValidation.Children([]);

    [Fact]
    public void AnUnknownChildKindIsRefused()
        => Assert.Throws<ArgumentException>(() => CompositionValidation.Children([new("hologram", "a")]));

    [Fact]
    public void ABlankKindNameOrAnOverlongNameIsRefused()
    {
        Assert.Throws<ArgumentException>(() => CompositionValidation.Children([new("card", " ")]));
        Assert.Throws<ArgumentException>(() => CompositionValidation.Children([new("card", new string('n', 513))]));
        Assert.Throws<ArgumentException>(() => CompositionValidation.Children([null!]));
    }

    [Fact]
    public void DuplicateChildReferencesAndMoreThan128ChildrenAreRefused()
    {
        Assert.Throws<ArgumentException>(() => CompositionValidation.Children([new("card", "a"), new("card", "a")]));
        var many = Enumerable.Range(0, 129).Select(index => new UiChildRef("card", "c" + index)).ToArray();
        Assert.Throws<ArgumentException>(() => CompositionValidation.Children(many));
        CompositionValidation.Children(many[..128]);
    }

    [Fact]
    public void ANullChildListIsRefused()
        => Assert.Throws<ArgumentNullException>(() => CompositionValidation.Children(null!));

    [Fact]
    public void AControlVisualWithNeutralOrAccentToneAndAnHttpImageIsAccepted()
    {
        ControlVisualValidation.Check(new ControlVisual());
        ControlVisualValidation.Check(new ControlVisual(Icon: "play", ImageUrl: "https://example.test/a.png", Tone: "accent"));
        ControlVisualValidation.Check(new ControlVisual(ImageUrl: "http://example.test/a.png"));
    }

    [Theory]
    [InlineData("file:///c:/secret.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("relative/path.png")]
    public void AControlImageThatIsNotAnAbsoluteHttpUrlIsRefused(string url)
        => Assert.Throws<ArgumentException>(() => ControlVisualValidation.Check(new ControlVisual(ImageUrl: url)));

    [Fact]
    public void AnUnknownToneAndOversizedArtworkAreRefused()
    {
        Assert.Throws<ArgumentException>(() => ControlVisualValidation.Check(new ControlVisual(Tone: "neon")));
        Assert.Throws<ArgumentException>(() => ControlVisualValidation.Check(new ControlVisual(Svg: new string('s', 32_001))));
        Assert.Throws<ArgumentException>(() => ControlVisualValidation.Check(new ControlVisual(Icon: new string('i', 81))));
        Assert.Throws<ArgumentException>(() => ControlVisualValidation.Check(new ControlVisual(ImageUrl: "https://example.test/" + new string('p', 4096))));
        Assert.Throws<ArgumentNullException>(() => ControlVisualValidation.Check(null!));
    }
}
