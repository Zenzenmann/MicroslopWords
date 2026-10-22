namespace MicroslopWords.App.Services;

public enum RecoveryPromptChoice
{
    Later,
    OpenRecoveredDraft,
    Discard,
}

public interface IRecoveryPrompt
{
    Task<RecoveryPromptChoice> PromptAsync(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmDiscardAsync(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableRecoveryPrompt : IRecoveryPrompt
{
    public Task<RecoveryPromptChoice> PromptAsync(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RecoveryPromptChoice.Later);

    public Task<bool> ConfirmDiscardAsync(
        RecoverySnapshot snapshot,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
