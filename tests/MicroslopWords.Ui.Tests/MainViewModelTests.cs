using System.Collections.Immutable;
using MicroslopWords.App.Controls;
using MicroslopWords.App.Services;
using MicroslopWords.App.ViewModels;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using MicroslopWords.Formats.Docx;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void New_document_is_editable_with_one_page()
    {
        var viewModel = CreateViewModel();

        viewModel.DocumentTitle.ShouldBe("Untitled");
        viewModel.WindowTitle.ShouldBe("Untitled - MicroslopWords");
        viewModel.FilePath.ShouldBeEmpty();
        viewModel.PageCount.ShouldBe(1);
        viewModel.PageLabel.ShouldBe("Page 1 of 1");
        viewModel.IsModified.ShouldBeFalse();
        viewModel.IsReadOnly.ShouldBeFalse();
        viewModel.IsEditingEnabled.ShouldBeTrue();
        viewModel.SaveStateText.ShouldBe("Not saved");
    }

    [Fact]
    public void InsertText_updates_document_layout_selection_and_undo_redo()
    {
        var viewModel = CreateViewModel();

        viewModel.InsertText("Hello");

        viewModel.Document.PlainText.ShouldBe("Hello");
        viewModel.Selection.Focus.Offset.ShouldBe(5);
        viewModel.IsModified.ShouldBeTrue();
        viewModel.PageCount.ShouldBe(1);
        viewModel.CanUndo.ShouldBeTrue();

        viewModel.UndoCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBeEmpty();
        viewModel.Selection.Focus.Offset.ShouldBe(0);

        viewModel.RedoCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBe("Hello");
        viewModel.Selection.Focus.Offset.ShouldBe(5);
    }

    [Fact]
    public void Typed_selection_replacement_is_one_undo_step()
    {
        var viewModel = CreateViewModel();
        viewModel.InsertText("Hello world");
        var runId = viewModel.Selection.Focus.RunId;
        var paragraphId = viewModel.Selection.Focus.ParagraphId;

        viewModel.Selection = new Selection(
            new TextPosition(paragraphId, runId, 6),
            new TextPosition(paragraphId, runId, 11));
        viewModel.InsertText("there");

        viewModel.Document.PlainText.ShouldBe("Hello there");

        viewModel.UndoCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBe("Hello world");
    }

    [Fact]
    public void Enter_splits_and_backspace_at_start_merges_paragraphs()
    {
        var viewModel = CreateViewModel();
        viewModel.InsertText("Hello");

        viewModel.SplitParagraph();

        viewModel.Document.PlainText
            .Split(Environment.NewLine)
            .ShouldBe(["Hello", string.Empty]);
        viewModel.Selection.Focus.Offset.ShouldBe(0);

        viewModel.Backspace();

        viewModel.Document.PlainText.ShouldBe("Hello");
        viewModel.Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Count()
            .ShouldBe(1);
    }

    [Fact]
    public void Shift_enter_inserts_a_soft_break_without_a_new_paragraph()
    {
        var viewModel = CreateViewModel();
        viewModel.InsertText("Hello");

        viewModel.InsertSoftBreak();
        viewModel.InsertText("world");

        viewModel.Document.PlainText.ShouldBe("Hello\nworld");
        viewModel.Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Count()
            .ShouldBe(1);
    }

    [Fact]
    public void Delete_selection_removes_exact_text_and_undo_restores_it()
    {
        var viewModel = CreateViewModel();
        viewModel.InsertText("Hello brave world");
        var position = viewModel.Selection.Focus;

        viewModel.Selection = new Selection(
            new TextPosition(position.ParagraphId, position.RunId, 6),
            new TextPosition(position.ParagraphId, position.RunId, 12));
        viewModel.Backspace();

        viewModel.Document.PlainText.ShouldBe("Hello world");

        viewModel.UndoCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBe("Hello brave world");
    }

    [Fact]
    public async Task Unsupported_documents_remain_read_only()
    {
        var picker = new FakeFilePicker(TestPath("unsupported.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(
                CreateDocument("Unsupported"),
                [new Diagnostic(
                    DocxDiagnosticCodes.UnsupportedTable,
                    DiagnosticSeverity.Warning,
                    "Tables are not shown.")]));
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);

        viewModel.IsReadOnly.ShouldBeTrue();
        viewModel.IsEditingEnabled.ShouldBeFalse();
        viewModel.CanCut.ShouldBeFalse();
        viewModel.CanPaste.ShouldBeFalse();

        viewModel.SelectAllCommand.Execute(null);

        viewModel.CanCopy.ShouldBeTrue();

        viewModel.InsertText("x");

        viewModel.Document.PlainText.ShouldBe("Hello");
        viewModel.IsModified.ShouldBeFalse();
    }

    [Fact]
    public void Zoom_commands_clamp_and_update_the_status_text()
    {
        var viewModel = CreateViewModel();

        for (var index = 0; index < 40; index++)
        {
            viewModel.ZoomInCommand.Execute(null);
        }

        viewModel.Zoom.ShouldBe(4);
        viewModel.ZoomPercentText.ShouldBe("400%");

        for (var index = 0; index < 60; index++)
        {
            viewModel.ZoomOutCommand.Execute(null);
        }

        viewModel.Zoom.ShouldBe(0.25);
        viewModel.ZoomPercentText.ShouldBe("25%");

        viewModel.ResetZoomCommand.Execute(null);

        viewModel.Zoom.ShouldBe(1);
        viewModel.ZoomMode.ShouldBe(ZoomFitMode.Custom);
    }

    [Fact]
    public void Fit_width_recomputes_when_the_viewport_changes()
    {
        var viewModel = CreateViewModel();
        viewModel.SetViewport(864, 1104);

        viewModel.FitWidthCommand.Execute(null);

        viewModel.ZoomMode.ShouldBe(ZoomFitMode.FitWidth);
        viewModel.Zoom.ShouldBe(1, 0.0001);

        viewModel.SetViewport(432, 1104);

        viewModel.Zoom.ShouldBe(0.4706, 0.0001);
    }

    [Fact]
    public void Direct_zoom_changes_leave_fit_mode()
    {
        var viewModel = CreateViewModel();
        viewModel.SetViewport(864, 1104);
        viewModel.FitWidthCommand.Execute(null);

        viewModel.Zoom = 1.5;

        viewModel.ZoomMode.ShouldBe(ZoomFitMode.Custom);
    }

    [Fact]
    public void Fit_page_uses_the_smaller_axis()
    {
        var viewModel = CreateViewModel();
        viewModel.SetViewport(864, 1104);

        viewModel.FitPageCommand.Execute(null);

        viewModel.ZoomMode.ShouldBe(ZoomFitMode.FitPage);
        viewModel.Zoom.ShouldBe(1, 0.0001);
    }

    [Fact]
    public void Zoom_does_not_change_the_layout()
    {
        var viewModel = CreateViewModel();
        var layout = viewModel.Layout;

        viewModel.SetViewport(864, 1104);
        viewModel.FitPageCommand.Execute(null);
        viewModel.ZoomInCommand.Execute(null);
        viewModel.ZoomOutCommand.Execute(null);
        viewModel.ResetZoomCommand.Execute(null);

        viewModel.Layout.ShouldBeSameAs(layout);
        viewModel.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Open_updates_page_count_and_page_label_for_multi_page_documents()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 2000));
        var picker = new FakeFilePicker(TestPath("long.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Long", text)));
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);

        viewModel.PageCount.ShouldBe(3);
        viewModel.PageLabel.ShouldBe("Page 1 of 3");
        viewModel.CurrentPage.ShouldBe(1);
    }

    [Fact]
    public void Page_label_uses_the_current_page_and_total()
    {
        var viewModel = CreateViewModel();

        viewModel.CurrentPage = 3;

        viewModel.PageLabel.ShouldBe("Page 1 of 1");
    }

    [Fact]
    public async Task Open_success_updates_document_state_and_diagnostics()
    {
        var picker = new FakeFilePicker(TestPath("opened.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(
                CreateDocument("Opened"),
                [new Diagnostic("MSW2001", DiagnosticSeverity.Warning, "Tables are not shown.")]));
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);

        loader.LastPath.ShouldBe(TestPath("opened.docx"));
        viewModel.DocumentTitle.ShouldBe("Opened");
        viewModel.WindowTitle.ShouldBe("Opened - MicroslopWords");
        viewModel.FilePath.ShouldBe(TestPath("opened.docx"));
        viewModel.PageCount.ShouldBe(1);
        viewModel.HasDiagnostics.ShouldBeTrue();
        viewModel.HasError.ShouldBeFalse();
        viewModel.StatusText.ShouldContain("unsupported");
    }

    [Fact]
    public async Task Open_failure_keeps_previous_document_and_reports_file()
    {
        var picker = new FakeFilePicker(TestPath("broken.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Failure<Document>(
                new Diagnostic(
                    "MSW1001",
                    DiagnosticSeverity.Error,
                    "The selected file is not a readable DOCX package.")));
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());
        var previousDocument = viewModel.History.Current;

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);

        viewModel.History.Current.ShouldBeSameAs(previousDocument);
        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldContain("broken.docx");
        viewModel.ErrorMessage.ShouldContain("not a readable DOCX");
        viewModel.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Open_shows_progress_after_one_second_and_keeps_previous_document_visible()
    {
        var release = new ManualResetEventSlim();
        var picker = new FakeFilePicker(TestPath("slow.docx"));
        var loader = new FakeDocumentLoader((_, cancellationToken) =>
        {
            release.Wait(TimeSpan.FromSeconds(10), cancellationToken);
            return OperationResult.Success(CreateDocument("Slow"));
        });
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());
        var previousDocument = viewModel.History.Current;

        var openTask = viewModel.OpenDocumentCommand.ExecuteAsync(null);

        await Task.Delay(
            TimeSpan.FromMilliseconds(1200),
            TestContext.Current.CancellationToken);

        viewModel.IsProgressVisible.ShouldBeTrue();
        viewModel.IsLoading.ShouldBeTrue();
        viewModel.CanStartAction.ShouldBeFalse();
        viewModel.History.Current.ShouldBeSameAs(previousDocument);

        release.Set();
        await openTask;

        viewModel.IsProgressVisible.ShouldBeFalse();
        viewModel.IsLoading.ShouldBeFalse();
        viewModel.DocumentTitle.ShouldBe("Slow");
    }

    [Fact]
    public async Task Canceling_open_discards_pending_result()
    {
        var started = new ManualResetEventSlim();
        var picker = new FakeFilePicker(TestPath("slow.docx"));
        var loader = new FakeDocumentLoader((_, cancellationToken) =>
        {
            started.Set();
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancellationToken.ThrowIfCancellationRequested();
            return OperationResult.Success(CreateDocument("Slow"));
        });
        var viewModel = new MainViewModel(picker, loader, new LayoutEngine());
        var previousDocument = viewModel.History.Current;

        var openTask = viewModel.OpenDocumentCommand.ExecuteAsync(null);
        started
            .Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
            .ShouldBeTrue();

        viewModel.OpenDocumentCancelCommand.Execute(null);
        await openTask;

        viewModel.History.Current.ShouldBeSameAs(previousDocument);
        viewModel.DocumentTitle.ShouldBe("Untitled");
        viewModel.IsLoading.ShouldBeFalse();
        viewModel.IsProgressVisible.ShouldBeFalse();
        viewModel.StatusText.ShouldBe("Open canceled.");
    }

    [Fact]
    public async Task Save_with_existing_path_writes_to_current_path_and_clears_dirty_state()
    {
        var picker = new FakeFilePicker(TestPath("source.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Opened")));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);
        var history = viewModel.History;

        viewModel.IsModified.ShouldBeTrue();
        viewModel.WindowTitle.ShouldBe("Opened* - MicroslopWords");
        viewModel.SaveStateText.ShouldBe("Unsaved changes");

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        saver.SaveCount.ShouldBe(1);
        saver.LastPath.ShouldBe(TestPath("source.docx"));
        picker.SavePickerCalls.ShouldBe(0);
        viewModel.FilePath.ShouldBe(TestPath("source.docx"));
        viewModel.IsModified.ShouldBeFalse();
        viewModel.WindowTitle.ShouldBe("Opened - MicroslopWords");
        viewModel.SaveStateText.ShouldBe("Saved");
        viewModel.History.ShouldBeSameAs(history);
        viewModel.History.CanUndo.ShouldBeTrue();
        viewModel.StatusText.ShouldBe("Saved source.docx.");
    }

    [Fact]
    public async Task Save_without_path_invokes_save_as_and_changes_path_after_success()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: TestPath("new.docx"));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        AddEdit(viewModel);

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        picker.LastSuggestedSaveName.ShouldBe("Untitled.docx");
        saver.LastPath.ShouldBe(TestPath("new.docx"));
        viewModel.FilePath.ShouldBe(TestPath("new.docx"));
        viewModel.DocumentTitle.ShouldBe("new");
        viewModel.WindowTitle.ShouldBe("new - MicroslopWords");
        viewModel.IsModified.ShouldBeFalse();
        viewModel.SaveStateText.ShouldBe("Saved");
    }

    [Fact]
    public async Task Canceled_save_as_keeps_path_content_dirty_state_and_history()
    {
        var picker = new FakeFilePicker(
            openPath: TestPath("source.docx"),
            savePath: null);
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Opened")));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);
        var history = viewModel.History;

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        saver.SaveCount.ShouldBe(0);
        viewModel.FilePath.ShouldBe(TestPath("source.docx"));
        viewModel.DocumentTitle.ShouldBe("Opened");
        viewModel.IsModified.ShouldBeTrue();
        viewModel.History.ShouldBeSameAs(history);
        viewModel.StatusText.ShouldBe("Save canceled.");
    }

    [Fact]
    public async Task Failed_save_as_keeps_previous_path_and_file_state()
    {
        var picker = new FakeFilePicker(
            openPath: TestPath("source.docx"),
            savePath: TestPath("unwritable.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Opened")));
        var saver = new FakeDocumentSaver((_, _) =>
            OperationResult.Failure<string>(
                new Diagnostic(
                    DocxDiagnosticCodes.SaveFailed,
                    DiagnosticSeverity.Error,
                    "The document could not be saved.")));
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);
        var history = viewModel.History;

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        viewModel.FilePath.ShouldBe(TestPath("source.docx"));
        viewModel.IsModified.ShouldBeTrue();
        viewModel.History.ShouldBeSameAs(history);
        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldContain("unwritable.docx");
        viewModel.StatusText.ShouldContain("still unsaved");
    }

    [Fact]
    public async Task Canceled_save_keeps_document_dirty_and_path_unchanged()
    {
        var picker = new FakeFilePicker(TestPath("source.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(CreateDocument("Opened")));
        var saver = new CancellableDocumentSaver();
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);
        var history = viewModel.History;

        var saveTask = viewModel.SaveDocumentCommand.ExecuteAsync(null);
        saver.Started
            .Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
            .ShouldBeTrue();

        viewModel.IsSaving.ShouldBeTrue();
        viewModel.CancelSaveCommand.Execute(null);
        await saveTask;

        viewModel.IsSaving.ShouldBeFalse();
        viewModel.FilePath.ShouldBe(TestPath("source.docx"));
        viewModel.IsModified.ShouldBeTrue();
        viewModel.History.ShouldBeSameAs(history);
        viewModel.StatusText.ShouldBe("Save canceled.");
    }

    [Fact]
    public async Task Unsupported_content_save_defaults_to_cancel_without_writing()
    {
        var picker = new FakeFilePicker(TestPath("source.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(
                CreateDocument("Opened"),
                [new Diagnostic(
                    DocxDiagnosticCodes.UnsupportedTable,
                    DiagnosticSeverity.Warning,
                    "Tables are not shown.")]));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var confirmation = new FakeSaveConfirmation(canSaveUnsupported: false);
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            confirmation);

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        confirmation.UnsupportedPromptCount.ShouldBe(1);
        saver.SaveCount.ShouldBe(0);
        viewModel.FilePath.ShouldBe(TestPath("source.docx"));
        viewModel.IsModified.ShouldBeTrue();
        viewModel.HasDiagnostics.ShouldBeTrue();
        viewModel.StatusText.ShouldContain("not acknowledged");
    }

    [Fact]
    public async Task Acknowledged_unsupported_content_save_writes_and_clears_notice()
    {
        var picker = new FakeFilePicker(TestPath("source.docx"));
        var loader = new FakeDocumentLoader((_, _) =>
            OperationResult.Success(
                CreateDocument("Opened"),
                [new Diagnostic(
                    DocxDiagnosticCodes.UnsupportedTable,
                    DiagnosticSeverity.Warning,
                    "Tables are not shown.")]));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var confirmation = new FakeSaveConfirmation(canSaveUnsupported: true);
        var viewModel = new MainViewModel(
            picker,
            loader,
            new LayoutEngine(),
            saver,
            confirmation);

        await viewModel.OpenDocumentCommand.ExecuteAsync(null);
        AddEdit(viewModel);

        await viewModel.SaveDocumentCommand.ExecuteAsync(null);

        confirmation.UnsupportedPromptCount.ShouldBe(1);
        saver.SaveCount.ShouldBe(1);
        viewModel.IsModified.ShouldBeFalse();
        viewModel.HasDiagnostics.ShouldBeFalse();
        viewModel.StatusText.ShouldContain("acknowledged loss");
    }

    [Fact]
    public async Task Save_as_without_extension_adds_docx_after_confirmation()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: TestPath("new"));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var confirmation = new FakeSaveConfirmation(canAddExtension: true);
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            confirmation);

        AddEdit(viewModel);

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        confirmation.ExtensionPromptCount.ShouldBe(1);
        saver.LastPath.ShouldBe(TestPath("new.docx"));
        viewModel.FilePath.ShouldBe(TestPath("new.docx"));
        viewModel.DocumentTitle.ShouldBe("new");
    }

    [Fact]
    public async Task Declining_docx_extension_keeps_document_unsaved()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: TestPath("new"));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var confirmation = new FakeSaveConfirmation(canAddExtension: false);
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            confirmation);

        AddEdit(viewModel);

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        confirmation.ExtensionPromptCount.ShouldBe(1);
        saver.SaveCount.ShouldBe(0);
        viewModel.FilePath.ShouldBeEmpty();
        viewModel.IsModified.ShouldBeTrue();
        viewModel.StatusText.ShouldBe("Save canceled.");
    }

    [Fact]
    public async Task Save_as_with_non_docx_extension_reports_error_without_writing()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: TestPath("new.txt"));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        AddEdit(viewModel);

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        saver.SaveCount.ShouldBe(0);
        viewModel.FilePath.ShouldBeEmpty();
        viewModel.IsModified.ShouldBeTrue();
        viewModel.ErrorMessage.ShouldContain("Only .docx");
    }

    [Fact]
    public async Task Copy_writes_the_selection_without_changing_the_document()
    {
        var clipboard = new FakeClipboardService();
        var viewModel = CreateViewModel(clipboard);
        viewModel.InsertText("Hello world");
        var position = viewModel.Selection.Focus;
        var before = viewModel.Document;

        viewModel.Selection = new Selection(
            new TextPosition(position.ParagraphId, position.RunId, 6),
            new TextPosition(position.ParagraphId, position.RunId, 11));

        await viewModel.CopyCommand.ExecuteAsync(null);

        clipboard.Written.ShouldNotBeNull();
        clipboard.Written.Text.ShouldBe("world");
        clipboard.Written.Html.ShouldNotBeNullOrEmpty();
        clipboard.Written.Fragment.ShouldNotBeNull();
        viewModel.Document.ShouldBeSameAs(before);
        viewModel.CanUndo.ShouldBeTrue();
    }

    [Fact]
    public async Task Cut_removes_the_selection_as_one_undo_step()
    {
        var clipboard = new FakeClipboardService();
        var viewModel = CreateViewModel(clipboard);
        viewModel.InsertText("Hello world");
        var position = viewModel.Selection.Focus;

        viewModel.Selection = new Selection(
            new TextPosition(position.ParagraphId, position.RunId, 6),
            new TextPosition(position.ParagraphId, position.RunId, 11));

        await viewModel.CutCommand.ExecuteAsync(null);

        viewModel.Document.PlainText.ShouldBe("Hello ");
        clipboard.Written!.Text.ShouldBe("world");

        viewModel.UndoCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBe("Hello world");
    }

    [Fact]
    public async Task Paste_internal_fragment_round_trips_the_copied_text()
    {
        var clipboard = new FakeClipboardService();
        var viewModel = CreateViewModel(clipboard);
        viewModel.InsertText("Hello world");
        var position = viewModel.Selection.Focus;

        viewModel.Selection = new Selection(
            new TextPosition(position.ParagraphId, position.RunId, 6),
            new TextPosition(position.ParagraphId, position.RunId, 11));
        await viewModel.CopyCommand.ExecuteAsync(null);

        viewModel.Selection = Selection.Caret(position);
        await viewModel.PasteCommand.ExecuteAsync(null);

        viewModel.Document.PlainText.ShouldBe("Hello worldworld");
    }

    [Fact]
    public async Task Paste_html_maps_formatting_and_ignores_scripts()
    {
        var clipboard = new FakeClipboardService
        {
            Content = new ClipboardContent(
                string.Empty,
                "<p><b>Bold</b> <script>alert(1)</script>text</p>"),
        };
        var viewModel = CreateViewModel(clipboard);

        await viewModel.PasteCommand.ExecuteAsync(null);

        viewModel.Document.PlainText.ShouldBe("Bold text");

        var paragraph = viewModel.Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single();

        paragraph.Runs.ShouldContain(run =>
            run.Text == "Bold" && run.EffectiveProperties.Bold);
        viewModel.Document.PlainText.ShouldNotContain("alert");
    }

    [Fact]
    public async Task Paste_plain_text_maps_lines_to_paragraphs()
    {
        var clipboard = new FakeClipboardService
        {
            Content = new ClipboardContent("first\r\nsecond"),
        };
        var viewModel = CreateViewModel(clipboard);

        await viewModel.PasteCommand.ExecuteAsync(null);

        var paragraphs = viewModel.Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .ToArray();

        paragraphs.Length.ShouldBe(2);
        paragraphs[0].PlainText.ShouldBe("first");
        paragraphs[1].PlainText.ShouldBe("second");
    }

    [Fact]
    public async Task Empty_or_image_only_clipboard_is_a_safe_no_op()
    {
        var clipboard = new FakeClipboardService
        {
            Content = new ClipboardContent(string.Empty, HasImage: true),
        };
        var viewModel = CreateViewModel(clipboard);
        var before = viewModel.Document;

        await viewModel.PasteCommand.ExecuteAsync(null);

        viewModel.Document.ShouldBeSameAs(before);
        viewModel.StatusText.ShouldContain("Image paste");

        clipboard.Content = new ClipboardContent(string.Empty);

        await viewModel.PasteCommand.ExecuteAsync(null);

        viewModel.Document.ShouldBeSameAs(before);
        viewModel.StatusText.ShouldBe("Clipboard is empty.");
    }

    [Fact]
    public void Select_all_selects_the_whole_document()
    {
        var viewModel = CreateViewModel();
        viewModel.InsertText("One");
        viewModel.SplitParagraph();
        viewModel.InsertText("Two");

        viewModel.SelectAllCommand.Execute(null);

        var start = viewModel.Selection.GetStart(viewModel.Document);
        var end = viewModel.Selection.GetEnd(viewModel.Document);

        start.Offset.ShouldBe(0);
        end.Offset.ShouldBe(3);
        viewModel.Document.PlainText.ShouldBe($"One{Environment.NewLine}Two");
    }

    [Fact]
    public void Rapid_typing_is_coalesced_into_one_undo_step()
    {
        var viewModel = CreateViewModel();

        viewModel.InsertText("a");
        viewModel.InsertText("b");
        viewModel.InsertText("c");

        viewModel.UndoCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBeEmpty();
        viewModel.CanUndo.ShouldBeFalse();
    }
    [Fact]
    public void Creating_a_new_document_clears_history()
    {
        var viewModel = CreateViewModel();
        viewModel.InsertText("Hello");

        viewModel.NewDocumentCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBeEmpty();
        viewModel.CanUndo.ShouldBeFalse();
        viewModel.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public async Task Saving_does_not_clear_the_undo_history()
    {
        var picker = new FakeFilePicker(openPath: null, savePath: TestPath("saved.docx"));
        var saver = new FakeDocumentSaver((_, path) => OperationResult.Success(path));
        var viewModel = new MainViewModel(
            picker,
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            saver,
            new FakeSaveConfirmation());

        viewModel.InsertText("Hello");

        await viewModel.SaveDocumentAsCommand.ExecuteAsync(null);

        viewModel.CanUndo.ShouldBeTrue();

        viewModel.UndoCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBeEmpty();
    }
    private static MainViewModel CreateViewModel(IClipboardService clipboard) =>
        new(
            new FakeFilePicker(null),
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine(),
            new FakeDocumentSaver((_, path) => OperationResult.Success(path)),
            new FakeSaveConfirmation(),
            recoveryStore: null,
            recoveryPrompt: null,
            TimeSpan.FromSeconds(60),
            TimeProvider.System,
            clipboard);
    private static MainViewModel CreateViewModel() =>
        new(
            new FakeFilePicker(null),
            new FakeDocumentLoader((_, _) =>
                OperationResult.Failure<Document>("TEST", "Not used.")),
            new LayoutEngine());

    private static string TestPath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "MicroslopWords.Tests", fileName);

    private static void AddEdit(MainViewModel viewModel)
    {
        var paragraph = viewModel.History.Current.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .First();

        viewModel.ApplyEdit(new InsertTextCommand(paragraph.Id, Run.Create(" edited")));
    }

    private static Document CreateDocument(string title, string text = "Hello")
    {
        var paragraph = ParagraphBlock.Create(text);
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(paragraph));
        return new Document(Guid.NewGuid(), title, ImmutableArray.Create(section));
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public ClipboardContent? Content { get; set; }

        public ClipboardContent? Written { get; private set; }

        public Task<ClipboardContent?> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Content);

        public Task<bool> WriteAsync(
            ClipboardContent content,
            CancellationToken cancellationToken = default)
        {
            Written = content;
            Content = content;
            return Task.FromResult(true);
        }
    }
    private sealed class FakeFilePicker(string? openPath, string? savePath = null) : IDocxFilePicker
    {
        public int SavePickerCalls { get; private set; }

        public string? LastSuggestedSaveName { get; private set; }

        public Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(openPath);

        public Task<string?> PickSaveFileAsync(
            string suggestedFileName,
            CancellationToken cancellationToken = default)
        {
            SavePickerCalls++;
            LastSuggestedSaveName = suggestedFileName;
            return Task.FromResult(savePath);
        }
    }

    private sealed class FakeDocumentLoader(
        Func<string, CancellationToken, OperationResult<Document>> handler) : IDocxDocumentLoader
    {
        public string? LastPath { get; private set; }

        public OperationResult<Document> Load(
            string path,
            CancellationToken cancellationToken = default)
        {
            LastPath = path;
            return handler(path, cancellationToken);
        }
    }

    private sealed class FakeDocumentSaver(
        Func<Document, string, OperationResult<string>> handler) : IDocxDocumentSaver
    {
        public int SaveCount { get; private set; }

        public string? LastPath { get; private set; }

        public OperationResult<string> Save(
            Document document,
            string path,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            LastPath = path;
            return handler(document, path);
        }
    }

    private sealed class FakeSaveConfirmation(
        bool canSaveUnsupported = false,
        bool canAddExtension = false) : ISaveConfirmation
    {
        public int UnsupportedPromptCount { get; private set; }

        public int ExtensionPromptCount { get; private set; }

        public Task<bool> ConfirmSaveDespiteUnsupportedContentAsync(
            IReadOnlyList<Diagnostic> diagnostics,
            CancellationToken cancellationToken = default)
        {
            UnsupportedPromptCount++;
            return Task.FromResult(canSaveUnsupported);
        }

        public Task<bool> ConfirmAddDocxExtensionAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            ExtensionPromptCount++;
            return Task.FromResult(canAddExtension);
        }
    }

    private sealed class CancellableDocumentSaver : IDocxDocumentSaver
    {
        public ManualResetEventSlim Started { get; } = new();

        public OperationResult<string> Save(
            Document document,
            string path,
            CancellationToken cancellationToken = default)
        {
            Started.Set();
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancellationToken.ThrowIfCancellationRequested();
            return OperationResult.Success(path);
        }
    }
}
