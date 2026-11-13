using MicroslopWords.App.Services;
using MicroslopWords.App.ViewModels;
using MicroslopWords.Core.Documents;
using MicroslopWords.Formats.Docx;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class MainViewModelImageTests
{
    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAGQAAAAyCAIAAAAlV+npAAAAhUlEQVR4nO3SMREAIBADQUAI/qUgCwt//W6d6ib73buYOcMdYjWeFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYi15j5uMQFoB1/W+QAAAABJRU5ErkJggg==";

    [Fact]
    public async Task InsertImageCommand_inserts_a_sized_image_with_alt_text_and_undoes()
    {
        var path = WriteTemporaryImage();
        var picker = new FakeImageFilePicker(path);
        var dialog = new FakeImageInsertDialog
        {
            Result = new ImageDialogResult(60, 30, false, "A red rectangle"),
        };
        using var viewModel = CreateViewModel(picker, dialog);

        try
        {
            await viewModel.InsertImageCommand.ExecuteAsync(null);

            var paragraph = SingleParagraph(viewModel);
            paragraph.PlainText.ShouldBe("\uFFFC");
            var image = paragraph.Runs.Single().EffectiveImage;
            image.ShouldNotBeNull();
            image!.FileName.ShouldBe(Path.GetFileName(path));
            image.WidthPoints.ShouldBe(60, 0.001);
            image.HeightPoints.ShouldBe(30, 0.001);
            image.AltText.ShouldBe("A red rectangle");
            viewModel.HasSelectedImage.ShouldBeTrue();
            viewModel.IsModified.ShouldBeTrue();
            viewModel.CanUndo.ShouldBeTrue();

            viewModel.UndoCommand.Execute(null);

            SingleParagraph(viewModel).Runs.ShouldBeEmpty();
            viewModel.HasSelectedImage.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EditImageCommand_updates_size_alt_text_and_undoes()
    {
        var path = WriteTemporaryImage();
        var picker = new FakeImageFilePicker(path);
        var dialog = new FakeImageInsertDialog
        {
            Result = new ImageDialogResult(60, 30, true, "Initial"),
        };
        using var viewModel = CreateViewModel(picker, dialog);

        try
        {
            await viewModel.InsertImageCommand.ExecuteAsync(null);
            dialog.Result = new ImageDialogResult(120, 10, true, "Updated");

            await viewModel.EditImageCommand.ExecuteAsync(null);

            var image = SingleParagraph(viewModel).Runs.Single().EffectiveImage;
            image.ShouldNotBeNull();
            image!.WidthPoints.ShouldBe(120, 0.001);
            image.HeightPoints.ShouldBe(60, 0.001);
            image.AltText.ShouldBe("Updated");

            viewModel.UndoCommand.Execute(null);

            var restored = SingleParagraph(viewModel).Runs.Single().EffectiveImage;
            restored.ShouldNotBeNull();
            restored!.WidthPoints.ShouldBe(60, 0.001);
            restored.HeightPoints.ShouldBe(30, 0.001);
            restored.AltText.ShouldBe("Initial");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RemoveImageCommand_deletes_the_selected_image_and_undoes()
    {
        var path = WriteTemporaryImage();
        var picker = new FakeImageFilePicker(path);
        var dialog = new FakeImageInsertDialog
        {
            Result = new ImageDialogResult(60, 30, true, string.Empty),
        };
        using var viewModel = CreateViewModel(picker, dialog);

        try
        {
            await viewModel.InsertImageCommand.ExecuteAsync(null);

            viewModel.RemoveImageCommand.Execute(null);

            SingleParagraph(viewModel).Runs.ShouldBeEmpty();
            viewModel.HasSelectedImage.ShouldBeFalse();

            viewModel.UndoCommand.Execute(null);

            SingleParagraph(viewModel).Runs.Single().EffectiveImage.ShouldNotBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Paste_inserts_an_image_from_the_operating_system_clipboard()
    {
        var clipboard = new FakeClipboardService
        {
            Content = new ClipboardContent(
                string.Empty,
                HasImage: true,
                ImagePng: Convert.FromBase64String(PngBase64)),
        };
        using var viewModel = CreateViewModel(
            new FakeImageFilePicker(null),
            new FakeImageInsertDialog(),
            clipboard);

        await viewModel.PasteCommand.ExecuteAsync(null);

        var image = SingleParagraph(viewModel).Runs.Single().EffectiveImage;
        image.ShouldNotBeNull();
        image!.Format.ShouldBe(ImageFormat.Png);
        image.PixelWidth.ShouldBe(100);
        image.PixelHeight.ShouldBe(50);
        viewModel.StatusText.ShouldBe("Pasted image.");
    }

    [Fact]
    public async Task Invalid_image_file_is_rejected_without_changing_the_document()
    {
        var path = WriteTemporaryImage("not an image");
        var picker = new FakeImageFilePicker(path);
        var dialog = new FakeImageInsertDialog
        {
            Result = new ImageDialogResult(60, 30, true, string.Empty),
        };
        using var viewModel = CreateViewModel(picker, dialog);
        var before = viewModel.Document;

        try
        {
            await viewModel.InsertImageCommand.ExecuteAsync(null);

            viewModel.Document.ShouldBeSameAs(before);
            viewModel.ErrorMessage.ShouldNotBeNullOrEmpty();
            viewModel.StatusText.ShouldNotBe("Inserted image.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static MainViewModel CreateViewModel(
        FakeImageFilePicker picker,
        FakeImageInsertDialog dialog,
        FakeClipboardService? clipboard = null) =>
        new(
            new UnavailableDocxFilePicker(),
            new DocxDocumentLoader(),
            new LayoutEngine(),
            new DocxDocumentSaver(),
            new UnavailableSaveConfirmation(),
            null,
            null,
            TimeSpan.FromSeconds(60),
            TimeProvider.System,
            clipboard,
            picker,
            dialog,
            new HeaderOnlyImageDecoder());

    private static ParagraphBlock SingleParagraph(MainViewModel viewModel) =>
        viewModel.Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single();

    private static string WriteTemporaryImage(string? contents = null)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MicroslopWords.Tests");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"image-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(
            path,
            contents is null
                ? Convert.FromBase64String(PngBase64)
                : System.Text.Encoding.UTF8.GetBytes(contents));
        return path;
    }

    private sealed class FakeImageFilePicker(string? path) : IImageFilePicker
    {
        public Task<string?> PickImageFileAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(path);
    }

    private sealed class FakeImageInsertDialog : IImageInsertDialog
    {
        public ImageDialogResult? Result { get; set; }

        public Task<ImageDialogResult?> ShowAsync(
            ImageDialogRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public ClipboardContent? Content { get; set; }

        public Task<ClipboardContent?> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Content);

        public Task<bool> WriteAsync(
            ClipboardContent content,
            CancellationToken cancellationToken = default)
        {
            Content = content;
            return Task.FromResult(true);
        }
    }
}

