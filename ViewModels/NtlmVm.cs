using Microsoft.UI.Xaml.Media;

namespace NSLocker.Ui.ViewModels;

/// <summary>État NTLM actuellement appliqué (distinct de PendingNtlmLevel, la valeur du curseur avant application).</summary>
public sealed class NtlmVm : ObservableObject
{
    private int _level;
    private string _label = "";
    private bool _isHardened;

    public int Level { get => _level; set => Set(ref _level, value); }
    public string Label { get => _label; set => Set(ref _label, value); }
    public bool IsHardened { get => _isHardened; set { if (Set(ref _isHardened, value)) Raise(nameof(Brush)); } }

    public Brush Brush => (Brush)Application.Current.Resources[_isHardened ? "StateOkBrush" : "StateCritBrush"];
}
