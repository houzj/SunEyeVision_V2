using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>
/// Owns the run-page capability that is not represented by ordinary VM fields.
/// The parameter controls themselves are borrowed from the formal parameter tree;
/// this object only validates supported VM selections and retires the interaction
/// lease when the page/session changes.
/// </summary>
internal sealed class BlobRunUiState : IDisposable
{
    private readonly IToolSettingsDraft _draft;
    private readonly DeclaredSettingsView _editor;
    private readonly IReadOnlySet<string> _supportedSort;
    private bool _attached;
    private bool _disposed;

    private bool Current => !_disposed && _attached && ReferenceEquals(_editor.Draft, _draft);

    internal bool IsUnsupportedSelection
    {
        get
        {
            if (!Current)
                return false;

            var values = _draft.CaptureFields();
            var mode = values.Read(BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.SegmentationMode)))
                as ConstantSetting;
            var sort = values.Read(BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.SortFeature)))
                as ConstantSetting;
            if (!BlobThresholdCapabilities.IsSupported(mode?.Value as string))
                return true;
            if (sort?.Value is not string sortKey)
                return true;
            return !_supportedSort.Contains(sortKey);
        }
    }

    // All supported run controls are formal fields. No additional visual state is
    // created here, so there is no second pending-editor set to commit or dispose.
    internal bool HasUiChanges => false;

    internal BlobRunUiState(
        IToolSettingsDraft draft,
        DeclaredSettingsView editor,
        IReadOnlySet<string> supportedSort)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _supportedSort = supportedSort ?? throw new ArgumentNullException(nameof(supportedSort));
    }

    internal void Attach(DeclaredFieldsView fieldHost)
    {
        ArgumentNullException.ThrowIfNull(fieldHost);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _attached = true;
    }

    internal void RetireInteractions()
    {
        // Formal fields are owned by DeclaredFieldsView. There is no second set of
        // controls to retire here; the editor/session lease controls their lifetime.
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _attached = false;
    }
}
