using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class ImageCommandTests
{
    [Fact]
    public void InsertInlineImageCommand_splits_the_run_and_undo_restores_text()
    {
        var document = Document.CreateEmpty();
        var history = new CommandHistory(document);
        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "Hello"));
        var focus = history.Selection.Focus;
        history.SetSelection(new Selection(
            new TextPosition(focus.ParagraphId, focus.RunId, 2),
            new TextPosition(focus.ParagraphId, focus.RunId, 2)));

        history.Execute(new InsertInlineImageCommand(history.Selection, CreateImage()));

        var paragraph = SingleParagraph(history.Current);
        paragraph.PlainText.ShouldBe("He\uFFFCllo");
        paragraph.Runs.Select(run => run.EffectiveImage is not null).ShouldBe([false, true, false]);
        history.Selection.Focus.RunId.ShouldBe(paragraph.Runs[1].Id);
        history.Selection.Focus.Offset.ShouldBe(1);

        history.Undo();

        SingleParagraph(history.Current).PlainText.ShouldBe("Hello");
        SingleParagraph(history.Current).Runs.Length.ShouldBe(1);
    }

    [Fact]
    public void InsertInlineImageCommand_replaces_a_selection_in_one_undo_step()
    {
        var document = Document.CreateEmpty();
        var history = new CommandHistory(document);
        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "Hello"));
        var focus = history.Selection.Focus;
        history.SetSelection(new Selection(
            new TextPosition(focus.ParagraphId, focus.RunId, 1),
            new TextPosition(focus.ParagraphId, focus.RunId, 4)));

        history.Execute(new InsertInlineImageCommand(history.Selection, CreateImage()));

        SingleParagraph(history.Current).PlainText.ShouldBe("H\uFFFCo");

        history.Undo();

        SingleParagraph(history.Current).PlainText.ShouldBe("Hello");
    }

    [Fact]
    public void DeleteSelectionCommand_removes_an_image_and_undo_restores_it()
    {
        var (document, paragraphId, runId) = CreateDocumentWithImage();
        var history = new CommandHistory(document);
        var selection = new Selection(
            new TextPosition(paragraphId, runId, 0),
            new TextPosition(paragraphId, runId, 1));
        history.SetSelection(selection);

        history.Execute(new DeleteSelectionCommand(selection));

        SingleParagraph(history.Current).PlainText.ShouldBe("AB");
        SingleParagraph(history.Current).Runs.Any(run => run.EffectiveImage is not null)
            .ShouldBeFalse();

        history.Undo();

        SingleParagraph(history.Current).Runs.Length.ShouldBe(3);
        SingleParagraph(history.Current).Runs[1].EffectiveImage.ShouldNotBeNull();
    }

    [Fact]
    public void Backspace_after_an_image_deletes_the_image_not_the_neighbouring_text()
    {
        var (document, paragraphId, runId) = CreateDocumentWithImage();
        var history = new CommandHistory(document);
        history.SetSelection(Selection.Caret(
            new TextPosition(paragraphId, runId, 1)));

        history.Execute(new DeleteBackwardCommand(history.Selection));

        SingleParagraph(history.Current).PlainText.ShouldBe("AB");
        SingleParagraph(history.Current).Runs.Length.ShouldBe(2);
    }

    [Fact]
    public void ResizeInlineImageCommand_locks_aspect_ratio_and_is_undoable()
    {
        var (document, paragraphId, runId) = CreateDocumentWithImage();
        var history = new CommandHistory(document);
        var selection = new Selection(
            new TextPosition(paragraphId, runId, 0),
            new TextPosition(paragraphId, runId, 1));
        history.SetSelection(selection);

        history.Execute(new ResizeInlineImageCommand(
            selection,
            paragraphId,
            runId,
            widthPoints: 150,
            heightPoints: 12,
            lockAspectRatio: true));

        var resized = SingleParagraph(history.Current).Runs[1].EffectiveImage;
        resized.ShouldNotBeNull();
        resized!.WidthPoints.ShouldBe(150, 0.0001);
        resized.HeightPoints.ShouldBe(75, 0.0001);
        resized.LockAspectRatio.ShouldBeTrue();

        history.Undo();

        var original = SingleParagraph(history.Current).Runs[1].EffectiveImage;
        original.ShouldNotBeNull();
        original!.WidthPoints.ShouldBe(75, 0.0001);
        original.HeightPoints.ShouldBe(37.5, 0.0001);
    }

    [Fact]
    public void SetInlineImageAltTextCommand_updates_accessible_name_and_undoes()
    {
        var (document, paragraphId, runId) = CreateDocumentWithImage();
        var history = new CommandHistory(document);
        var selection = Selection.Caret(new TextPosition(paragraphId, runId, 0));
        history.SetSelection(selection);

        history.Execute(new SetInlineImageAltTextCommand(
            selection,
            paragraphId,
            runId,
            "  A red rectangle  "));

        var updated = SingleParagraph(history.Current).Runs[1].EffectiveImage;
        updated.ShouldNotBeNull();
        updated!.AltText.ShouldBe("A red rectangle");
        updated.AccessibleName.ShouldBe("A red rectangle");

        history.Undo();

        var original = SingleParagraph(history.Current).Runs[1].EffectiveImage;
        original.ShouldNotBeNull();
        original!.AltText.ShouldBe("Alt text");
    }

    [Fact]
    public void DocumentFragment_capture_and_paste_preserve_an_inline_image()
    {
        var (document, paragraphId, runId) = CreateDocumentWithImage();
        var selection = new Selection(
            new TextPosition(paragraphId, document.FindParagraph(paragraphId)!.Runs[0].Id, 0),
            new TextPosition(paragraphId, document.FindParagraph(paragraphId)!.Runs[2].Id, 1));

        var fragment = DocumentFragment.Capture(document, selection);

        fragment.Paragraphs.Single().Runs.Length.ShouldBe(3);
        fragment.Paragraphs.Single().Runs[1].Image.ShouldNotBeNull();
        fragment.PlainText.ShouldBe("AB");

        var target = Document.CreateEmpty();
        var history = new CommandHistory(target);
        history.Execute(new PasteFragmentCommand(history.Selection, fragment));

        var pasted = SingleParagraph(history.Current);
        pasted.Runs.Length.ShouldBe(3);
        var pastedImage = pasted.Runs[1].EffectiveImage;
        pastedImage.ShouldNotBeNull();
        pastedImage!.Data.ToArray().ShouldBe(CreateImage().Data.ToArray());
        pasted.PlainText.ShouldBe("A\uFFFCB");
        runId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Typing_at_an_image_boundary_does_not_attach_text_to_the_image_run()
    {
        var (document, paragraphId, runId) = CreateDocumentWithImage();
        var history = new CommandHistory(document);
        history.SetSelection(Selection.Caret(
            new TextPosition(paragraphId, runId, 1)));

        history.Execute(new InsertTextAtSelectionCommand(history.Selection, "X"));

        var paragraph = SingleParagraph(history.Current);
        paragraph.PlainText.ShouldBe("A\uFFFCXB");
        paragraph.Runs.Length.ShouldBe(4);
        paragraph.Runs[1].EffectiveImage.ShouldNotBeNull();
        paragraph.Runs[1].Text.Length.ShouldBe(1);
    }

    [Fact]
    public void Formatting_an_image_keeps_the_image_and_updates_its_run_properties()
    {
        var (document, paragraphId, runId) = CreateDocumentWithImage();
        var history = new CommandHistory(document);
        var selection = new Selection(
            new TextPosition(paragraphId, runId, 0),
            new TextPosition(paragraphId, runId, 1));
        history.SetSelection(selection);

        history.Execute(new FormatSelectionCommand(
            selection,
            properties => properties with { Bold = true }));

        var paragraph = SingleParagraph(history.Current);
        paragraph.Runs[1].EffectiveImage.ShouldNotBeNull();
        paragraph.Runs[1].EffectiveProperties.Bold.ShouldBeTrue();
    }

    private static InlineImage CreateImage() =>
        new(
            ImageFormat.Png,
            "red.png",
            ImmutableArray.Create(ImageTestData.Png()),
            100,
            50,
            75,
            37.5,
            "Alt text");

    private static (Document Document, Guid ParagraphId, Guid RunId) CreateDocumentWithImage()
    {
        var imageRun = Run.CreateImage(CreateImage());
        var paragraph = new ParagraphBlock(
            Guid.NewGuid(),
            ImmutableArray.Create(
                Run.Create("A"),
                imageRun,
                Run.Create("B")));
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(paragraph));
        var document = new Document(
            Guid.NewGuid(),
            "Test",
            ImmutableArray.Create(section));

        return (document, paragraph.Id, imageRun.Id);
    }

    private static ParagraphBlock SingleParagraph(Document document) =>
        document.Sections.SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single();
}
