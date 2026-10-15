using System.Collections.ObjectModel;
using NSLocker.Services;
using NSLocker.Ui.Services;

namespace NSLocker.Ui.ViewModels;

/// <summary>
/// État et actions de toute l'application. Appelle directement NetworkSecurityService (plus de pont JSON/postMessage) ;
/// les accès registre/WMI tournent hors du thread d'interface (Task.Run), le reste est mis à jour sur le thread UI.
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private bool _busy;
    private string _message = "";
    private InfoBarSeverity _severity = InfoBarSeverity.Informational;

    public ProtocolVm Mdns { get; } = new(Explainers.Mdns);
    public ProtocolVm Llmnr { get; } = new(Explainers.Llmnr);
    public ProtocolVm Dnssec { get; } = new(Explainers.Dnssec);
    public ProtocolVm Ipv6 { get; } = new(Explainers.Ipv6);
    public ObservableCollection<ProtocolVm> Nbt { get; } = new();

    /// <summary>Affichée au lancement (tableau de bord) : nom, édition et build de l'OS détecté.</summary>
    public string OsDescription { get; } = NetworkSecurityService.GetOsDescription();

    public bool IsBusy { get => _busy; private set { if (Set(ref _busy, value)) Raise(nameof(IsIdle)); } }
    public bool IsIdle => !_busy;

    /// <summary>Message affiché dans la barre de notification (vide = masquée).</summary>
    public string Message { get => _message; private set { Set(ref _message, value); Raise(nameof(HasMessage)); } }
    public bool HasMessage => _message.Length > 0;
    public InfoBarSeverity Severity { get => _severity; private set => Set(ref _severity, value); }

    /// <summary>Pire état NBT-NS : activé si au moins une carte l'est.</summary>
    public ProtocolState NbtWorst => Nbt.Count == 0 ? ProtocolState.Indeterminate
        : Nbt.Any(n => n.IsEnabled) ? ProtocolState.Enabled : ProtocolState.Disabled;

    public ProtocolVm NbtSummary { get; } = new(Explainers.NbtNs);

    private DnssecDiagnosticsVm? _dnssecDiag;
    /// <summary>Résultat du dernier diagnostic DNSSEC (null = jamais lancé).</summary>
    public DnssecDiagnosticsVm? DnssecDiag { get => _dnssecDiag; private set => Set(ref _dnssecDiag, value); }

    public NtlmVm Ntlm { get; } = new();

    private double _pendingNtlmLevel;
    /// <summary>
    /// Valeur du curseur NTLM avant application (le registre n'est écrit qu'au clic sur Appliquer).
    /// Type double : Slider.Value l'exige pour le binding bidirectionnel x:Bind.
    /// </summary>
    public double PendingNtlmLevel
    {
        get => _pendingNtlmLevel;
        set { if (Set(ref _pendingNtlmLevel, value)) Raise(nameof(PendingNtlmLabel)); }
    }
    public string PendingNtlmLabel => NetworkSecurityService.NtlmLevelLabels[Math.Clamp((int)_pendingNtlmLevel, 0, 5)];

    public void Dismiss() => Message = "";

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (mdns, llmnr, dnssec, ipv6, nbt, ntlm) = await Task.Run(() =>
                (NetworkSecurityService.GetMdnsStatus(), NetworkSecurityService.GetLlmnrStatus(),
                 NetworkSecurityService.GetDnssecStatus(), NetworkSecurityService.GetIpv6Status(),
                 NetworkSecurityService.GetNbtNsInterfaces(), NetworkSecurityService.GetNtlmStatus()));
            Apply(Mdns, mdns); Apply(Llmnr, llmnr); Apply(Dnssec, dnssec); Apply(Ipv6, ipv6);
            Nbt.Clear();
            foreach (var i in nbt)
                Nbt.Add(new ProtocolVm(Explainers.NbtNs, i.AdapterName, i.AdapterId) { State = i.State, Detail = i.Detail });
            NbtSummary.State = NbtWorst;
            Ntlm.Level = ntlm.Level; Ntlm.Label = ntlm.Label; Ntlm.IsHardened = ntlm.IsHardened;
            PendingNtlmLevel = ntlm.Level;
        }
        catch (Exception ex) { Notify("Lecture impossible : " + ex.Message, InfoBarSeverity.Error); }
        finally { IsBusy = false; }
    }

    public Task ToggleAsync(ProtocolVm p) => RunAsync(() => p.Info.Key switch
    {
        "mdns" => () => NetworkSecurityService.SetMdnsEnabled(!p.IsEnabled),
        "llmnr" => () => NetworkSecurityService.SetLlmnrEnabled(!p.IsEnabled),
        "dnssec" => () => NetworkSecurityService.SetDnssecEnabled(!p.IsEnabled),
        "ipv6" => () => NetworkSecurityService.SetIpv6Enabled(!p.IsEnabled),
        _ => () => NetworkSecurityService.SetNbtNsForInterface(p.Id, !p.IsEnabled),
    }, $"{p.Name} {(p.IsEnabled ? "désactivé" : "activé")}. {p.Info.ApplyNote}");

    public Task RestartDnscacheAsync() => RunAsync(
        () => NetworkSecurityService.RestartDnscacheService,
        "Service DNS Client (Dnscache) redémarré — le nouveau réglage est maintenant actif.");

    /// <summary>
    /// Diagnostic DNSSEC à la demande (NRPT, résolution réelle sur domaine signé/cassé, et sur le
    /// contrôleur de domaine si le poste est joint à un AD). Requêtes réseau : peut prendre
    /// plusieurs secondes, volontairement hors de RefreshAsync pour ne pas ralentir le reste.
    /// </summary>
    public async Task RunDnssecDiagnosticsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        DnssecDiag = null;
        try
        {
            var result = await NetworkSecurityService.RunDnssecDiagnosticsAsync();
            DnssecDiag = new DnssecDiagnosticsVm(result);
        }
        catch (Exception ex) { Notify("Diagnostic DNSSEC impossible : " + ex.Message, InfoBarSeverity.Error); }
        finally { IsBusy = false; }
    }

    public Task ApplyNtlmLevelAsync()
    {
        int level = (int)PendingNtlmLevel;
        return RunAsync(
            () => () => NetworkSecurityService.SetNtlmLevel(level),
            $"Niveau NTLM réglé sur : {NetworkSecurityService.NtlmLevelLabels[level]}. {Explainers.NtlmApplyNote}");
    }

    private async Task RunAsync(Func<Action> action, string success)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await Task.Run(action());
            Notify(success, InfoBarSeverity.Success);
        }
        catch (Exception ex) { Notify("Échec : " + ex.Message, InfoBarSeverity.Error); }
        finally { IsBusy = false; }
        await RefreshAsync();
    }

    private static void Apply(ProtocolVm vm, ProtocolStatus s) { vm.State = s.State; vm.Detail = s.Detail; }

    private void Notify(string text, InfoBarSeverity severity) { Severity = severity; Message = text; }
}
