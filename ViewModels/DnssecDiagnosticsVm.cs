using NSLocker.Services;

namespace NSLocker.Ui.ViewModels;

/// <summary>Présentation en lignes (icône + texte) du résultat de NetworkSecurityService.RunDnssecDiagnosticsAsync.</summary>
public sealed class DnssecDiagnosticsVm
{
    public DnssecDiagnosticsVm(NetworkSecurityService.DnssecDiagnostics d)
    {
        PolicyLine = $"{Icon(d.Policy.State == ProtocolState.Enabled)} Stratégie globale : {d.Policy.Detail}";
        NrptLine = $"{Icon(d.NrptRequiresValidation)} {d.NrptDetail}";
        SignedLine = $"{Icon(d.SignedDomainOk)} {d.SignedDomainDetail}";
        ForgedLine = $"{Icon(d.ForgedDomainBlocked)} {d.ForgedDomainDetail}";
        DcLine = d.IsDomainJoined
            ? $"{Icon(d.DcForgedDomainBlocked)} {d.DcDetail}"
            : $"ℹ️ {d.DcDetail}";
    }

    public string PolicyLine { get; }
    public string NrptLine { get; }
    public string SignedLine { get; }
    public string ForgedLine { get; }
    public string DcLine { get; }

    private static string Icon(bool? ok) => ok switch { true => "✅", false => "❌", null => "❔" };
}
