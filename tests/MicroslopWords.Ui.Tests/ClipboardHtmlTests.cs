using System.Collections.Immutable;
using MicroslopWords.App.Services;
using MicroslopWords.Core.Documents;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class ClipboardHtmlTests
{
    [Fact]
    public void Parse_keeps_allowed_formatting_and_drops_active_content()
    {
        var fragment = ClipboardHtml.Parse(
            "<p>Hello <b>bold</b> <i>italic</i> <u>under</u><br>next "
            + "<a href=\"https://example.invalid\">link</a></p>"
            + "<script>alert('bad')</script>"
            + "<img src=\"https://example.invalid/x.png\" onerror=\"alert(1)\">");

        fragment.Paragraphs.Length.ShouldBe(1);
        fragment.PlainText.ShouldBe("Hello bold italic under\nnext link");
        fragment.Paragraphs[0].Runs.ShouldContain(run =>
            run.Text == "bold" && run.Properties != null && run.Properties.Bold);
        fragment.Paragraphs[0].Runs.ShouldContain(run =>
            run.Text == "italic" && run.Properties != null && run.Properties.Italic);
        fragment.Paragraphs[0].Runs.ShouldContain(run =>
            run.Text == "under" && run.Properties != null && run.Properties.Underline);
        fragment.PlainText.ShouldNotContain("alert");
        fragment.PlainText.ShouldNotContain("example.invalid");
    }

    [Fact]
    public void Parse_maps_paragraphs_breaks_and_lists()
    {
        var fragment = ClipboardHtml.Parse(
            "<p>First</p><p>Second<br>soft</p><ul><li>One</li><li>Two</li></ul>");

        fragment.Paragraphs.Length.ShouldBe(4);
        fragment.Paragraphs[0].PlainText.ShouldBe("First");
        fragment.Paragraphs[1].PlainText.ShouldBe("Second\nsoft");
        fragment.Paragraphs[2].PlainText.ShouldBe("\u2022 One");
        fragment.Paragraphs[3].PlainText.ShouldBe("\u2022 Two");
    }

    [Fact]
    public void ToHtml_and_Parse_round_trip_text_and_character_formatting()
    {
        var fragment = new DocumentFragment(
            ImmutableArray.Create(
                new ParagraphFragment(
                    null,
                    ImmutableArray.Create(
                        new RunFragment("plain ", null),
                        new RunFragment(
                            "bold",
                            new RunProperties { Bold = true }),
                        new RunFragment(
                            " italic",
                            new RunProperties { Italic = true }),
                        new RunFragment(
                            " under",
                            new RunProperties { Underline = true }),
                        new RunFragment("\nnext", null)))));

        var html = ClipboardHtml.ToHtml(fragment);
        var parsed = ClipboardHtml.Parse(html);

        parsed.PlainText.ShouldBe(fragment.PlainText);
        parsed.Paragraphs[0].Runs.ShouldContain(run =>
            run.Text == "bold" && run.Properties != null && run.Properties.Bold);
        parsed.Paragraphs[0].Runs.ShouldContain(run =>
            run.Text == " italic" && run.Properties != null && run.Properties.Italic);
        parsed.Paragraphs[0].Runs.ShouldContain(run =>
            run.Text == " under" && run.Properties != null && run.Properties.Underline);
    }
}
