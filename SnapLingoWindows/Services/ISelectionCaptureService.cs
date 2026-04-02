using SnapLingoWindows.Models;

namespace SnapLingoWindows.Services;

public interface ISelectionCaptureService
{
    Task<SelectionCaptureOutcome> CaptureSelectionAsync(CancellationToken cancellationToken);
    Task<SelectionSnapshot?> TryCaptureSelectionSnapshotAsync(CancellationToken cancellationToken);
    Task<string?> TryCaptureSelectionTextAsync(CancellationToken cancellationToken);
    Task<string?> WaitForClipboardChangeAsync(uint afterChangeCount, CancellationToken cancellationToken);
    Task<string?> ReadClipboardTextAsync();
}
