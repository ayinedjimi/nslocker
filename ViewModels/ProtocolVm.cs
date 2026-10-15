using Microsoft.UI.Xaml.Media;
using NSLocker.Services;
using NSLocker.Ui.Services;

namespace NSLocker.Ui.ViewModels;

/// <summary>Un protocole à état unique (mDNS, LLMNR) ou une carte réseau NBT-NS.</summary>
public sealed class ProtocolVm : ObservableObject
{
    private ProtocolState _state = ProtocolState.Indeterminate;
    private string _detail = "";

    public ProtocolVm(Explainer info, string? name = null, string? id = null)
    {
        Info = info; Name = name ?? info.Title; Id = id ?? info.Key;
    }

    public Explainer Info { get; }
    public string Name { get; }
    /// <summary>Clé du protocole, ou GUID de l'interface pour NBT-NS.</summary>
    public string Id { get; }

    public ProtocolState State
    {
        get => _state;
        set { if (Set(ref _state, value)) { Raise(nameof(Label)); Raise(nameof(Brush)); Raise(nameof(Glyph)); Raise(nameof(IsEnabled)); Raise(nameof(ActionText)); } }
    }

    public string Detail { get => _detail; set => Set(ref _detail, value); }

    public bool IsEnabled => State == ProtocolState.Enabled;
    public string Label => State switch { ProtocolState.Enabled => "Activé", ProtocolState.Disabled => "Désactivé", _ => "Indéterminé" };
    public string ActionText => IsEnabled ? "Désactiver" : "Activer";

    // Vrai quand State représente le bon état côté sécurité, en tenant compte de l'inversion
    // DNSSEC (Info.RiskyWhenEnabled = false) : pour mDNS/LLMNR/NBT-NS, Désactivé est bon ; pour
    // DNSSEC, Activé est bon. Utilisé à la fois pour la couleur et l'icône, pour qu'elles restent
    // cohérentes entre elles (un bug précédent ne faisait l'inversion que sur la couleur, pas
    // l'icône, laissant une coche à côté d'un état pourtant affiché en rouge).
    private bool? IsGoodState => State switch
    {
        ProtocolState.Enabled => !Info.RiskyWhenEnabled,
        ProtocolState.Disabled => Info.RiskyWhenEnabled,
        _ => null,
    };

    /// <summary>Segoe Fluent Icons : coche (bon état) / avertissement (mauvais état) / point d'interrogation (indéterminé).</summary>
    public string Glyph => IsGoodState switch { true => "", false => "", _ => "" };

    public Brush Brush => (Brush)Application.Current.Resources[IsGoodState switch
    {
        true => "StateOkBrush",
        false => "StateCritBrush",
        _ => "StateWarnBrush",
    }];
}
