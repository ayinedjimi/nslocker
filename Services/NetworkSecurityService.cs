using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace NSLocker.Services;

public enum ProtocolState
{
    Enabled,
    Disabled,
    Indeterminate,
}

public sealed record ProtocolStatus(string Key, string Label, ProtocolState State, string Detail);

public sealed record NbtInterfaceStatus(string AdapterId, string AdapterName, ProtocolState State, string Detail);

/// <summary>
/// Détection et bascule de mDNS, LLMNR et NBT-NS.
///
/// Détection : mêmes clés de registre que CHECKSEC (NetworkSecCollector) — EnableMDNS côté
/// Dnscache, EnableMulticast côté stratégie DNS Client, NetbiosOptions par interface NetBT.
/// Bascule : écrit ces mêmes clés. Nécessite les droits administrateur (cf. app.manifest,
/// requireAdministrator) — chaque méthode Set* lève si l'écriture échoue, plutôt que d'échouer
/// silencieusement.
/// </summary>
public static class NetworkSecurityService
{
    private const string DnsClientPolicyKey = @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient";
    private const string DnscacheParamsKey = @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters";
    private const string NetBtInterfacesKey = @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    // Build à partir duquel Windows embarque un résolveur/répondeur mDNS natif dans le service
    // Dnscache (Windows 10 1703 "Creators Update" ; côté serveur, Windows Server 2019 partage ce
    // même socle — Server 2016 et antérieur n'ont pas cette fonctionnalité). Avant ce build,
    // EnableMDNS n'a aucun effet : il n'y a tout simplement rien à désactiver.
    private const int MdnsNativeMinBuild = 15063;

    // ---------- Détection OS (affichée au lancement) ----------

    public static string GetOsDescription()
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? key = baseKey.OpenSubKey(CurrentVersionKey);
            string productName = key?.GetValue("ProductName") as string ?? "Windows";
            string installationType = key?.GetValue("InstallationType") as string ?? "Client";
            _ = int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out int build);
            int ubr = (key?.GetValue("UBR") as int?) ?? 0;
            string display = (key?.GetValue("DisplayVersion") as string) ?? (key?.GetValue("ReleaseId") as string) ?? "";

            bool isServer = installationType.Contains("Server", StringComparison.OrdinalIgnoreCase);
            // ProductName dit parfois encore "Windows 10 ..." sur des builds Windows 11 (bug Microsoft
            // connu, selon le canal de mise à jour) : corrigé via le seuil de build officiel (22000).
            string label = (!isServer && build >= 22000 && productName.Contains("Windows 10"))
                ? productName.Replace("Windows 10", "Windows 11")
                : productName;

            string version = display.Length > 0 ? " " + display : "";
            string buildStr = ubr > 0 ? $"{build}.{ubr}" : build.ToString();
            return $"{label}{version} (build {buildStr})";
        }
        catch
        {
            return "Windows (version indéterminée)";
        }
    }

    private static bool IsMdnsNativelySupported()
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? key = baseKey.OpenSubKey(CurrentVersionKey);
            _ = int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out int build);
            return build >= MdnsNativeMinBuild;
        }
        catch
        {
            // Au doute, on ne bloque pas la fonctionnalité : on se contente de ne pas prétendre
            // "activé par défaut" à tort si la lecture de la clé EnableMDNS échoue aussi.
            return true;
        }
    }

    // ---------- mDNS ----------

    public static ProtocolStatus GetMdnsStatus()
    {
        if (!IsMdnsNativelySupported())
        {
            return new ProtocolStatus("mdns", "mDNS (Multicast DNS)", ProtocolState.Indeterminate,
                "Non applicable sur cette version de Windows — le résolveur mDNS natif a été introduit avec " +
                "Windows 10 1703 / Windows Server 2019. Rien à désactiver ici sur ce poste.");
        }
        object? value = ReadHklm(DnscacheParamsKey, "EnableMDNS");
        if (value is int i)
        {
            return new ProtocolStatus("mdns", "mDNS (Multicast DNS)",
                i != 0 ? ProtocolState.Enabled : ProtocolState.Disabled,
                $"EnableMDNS = {i}");
        }
        return new ProtocolStatus("mdns", "mDNS (Multicast DNS)", ProtocolState.Enabled,
            "Non configuré — activé par défaut");
    }

    public static void SetMdnsEnabled(bool enabled)
    {
        if (!IsMdnsNativelySupported())
        {
            throw new InvalidOperationException(
                "mDNS natif n'existe pas sur cette version de Windows (introduit avec Windows 10 1703 / Windows Server 2019) — rien à activer ou désactiver.");
        }
        WriteHklm(DnscacheParamsKey, "EnableMDNS", enabled ? 1 : 0);
    }

    // ---------- LLMNR ----------

    public static ProtocolStatus GetLlmnrStatus()
    {
        object? value = ReadHklm(DnsClientPolicyKey, "EnableMulticast");
        if (value is int i)
        {
            return new ProtocolStatus("llmnr", "LLMNR (Link-Local Multicast Name Resolution)",
                i != 0 ? ProtocolState.Enabled : ProtocolState.Disabled,
                $"EnableMulticast = {i}");
        }
        return new ProtocolStatus("llmnr", "LLMNR (Link-Local Multicast Name Resolution)", ProtocolState.Enabled,
            "Non configuré — activé par défaut");
    }

    public static void SetLlmnrEnabled(bool enabled)
    {
        // EnableMulticast = 0 désactive LLMNR ; 1 (ou absence de la clé) le laisse activé.
        WriteHklm(DnsClientPolicyKey, "EnableMulticast", enabled ? 1 : 0);
    }

    // ---------- DNSSEC ----------

    // Contrairement à mDNS/LLMNR/NBT-NS, l'état "souhaité" est inversé : DNSSEC activé est le bon
    // état (authenticité des réponses DNS), désactivé/non configuré est l'état à risque. Stable
    // depuis Windows 7 / Server 2008 R2 (support DNSSEC côté client) — aucune restriction de build.
    //
    // Portée volontairement limitée au strict nécessaire : lecture/bascule de la clé de stratégie
    // globale EnableDnssec. Cette clé seule n'impose pas forcément une validation effective par
    // domaine — celle-ci passe normalement par des règles NRPT (Name Resolution Policy Table),
    // configurables localement (Add-DnsClientNrptRule -DnssecValidationRequired, pas besoin de
    // GPO sur un poste isolé) ou, en environnement de domaine Active Directory, en général
    // distribuées par Stratégie de groupe (GPO) pour s'appliquer à tout le parc. Ce module ne
    // touche pas aux règles NRPT : c'est signalé explicitement dans le texte affiché à
    // l'utilisateur plutôt que de laisser croire que la bascule seule suffit.
    public static ProtocolStatus GetDnssecStatus()
    {
        object? value = ReadHklm(DnsClientPolicyKey, "EnableDnssec");
        bool isEnabled = value is int i && i == 1;
        return new ProtocolStatus("dnssec", "DNSSEC (validation des signatures DNS)",
            isEnabled ? ProtocolState.Enabled : ProtocolState.Disabled,
            value == null ? "Non configuré — désactivé par défaut" : $"EnableDnssec = {value}");
    }

    public static void SetDnssecEnabled(bool enabled)
    {
        WriteHklm(DnsClientPolicyKey, "EnableDnssec", enabled ? 1 : 0);
    }

    // ---------- NTLM (niveau d'authentification réseau) ----------

    // LmCompatibilityLevel existe sur TOUT Windows (poste, serveur, contrôleur de domaine) depuis
    // NT4 — ce n'est pas réservé aux serveurs. C'est le même terrain que LLMNR/NBT-NS : LM et
    // NTLMv1 sont vulnérables au craquage de hachage et au relais ; niveau 5 (NTLMv2 uniquement,
    // refus LM + NTLM) est la configuration recommandée (CIS 2.3.11.7 / ANSSI R67). Lu par LSA au
    // démarrage : un redémarrage du poste est recommandé pour une prise en compte garantie.
    private const string LsaKey = @"SYSTEM\CurrentControlSet\Control\Lsa";

    // Libellés fidèles au texte officiel Microsoft ("Network security: LAN Manager authentication
    // level"). Pour les niveaux 3 à 5, "refuse" décrit le comportement d'acceptation côté
    // serveur/contrôleur de domaine (ce que ce poste accepte en authentification entrante), pas
    // uniquement ce qu'il envoie en sortant — nuance importante, à ne pas paraphraser à la légère.
    public static readonly string[] NtlmLevelLabels =
    {
        "Niveau 0 — Envoie LM + NTLM, n'utilise jamais NTLMv2 (sécurité très faible, compatibilité maximale avec les très vieux systèmes)",
        "Niveau 1 — Envoie LM + NTLM, utilise NTLMv2 si négocié (sécurité faible, compatibilité élevée)",
        "Niveau 2 — Envoie NTLM uniquement, utilise NTLMv2 si négocié (sécurité moyenne, bonne compatibilité)",
        "Niveau 3 — Envoie NTLMv2 uniquement (sécurité bonne, compatibilité correcte)",
        "Niveau 4 — Envoie NTLMv2 uniquement, refuse LM en entrant (sécurité bonne, compatibilité correcte)",
        "Niveau 5 — Envoie NTLMv2 uniquement, refuse LM et NTLM en entrant (sécurité recommandée — peut casser de très vieux systèmes)",
    };

    public sealed record NtlmStatus(int Level, string Label, bool IsConfigured, bool IsHardened);

    public static NtlmStatus GetNtlmStatus()
    {
        object? value = ReadHklm(LsaKey, "LmCompatibilityLevel");
        int level = value is int i ? Math.Clamp(i, 0, 5) : 0;
        return new NtlmStatus(level, NtlmLevelLabels[level], value != null, level >= 5);
    }

    public static void SetNtlmLevel(int level)
    {
        if (level is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(level), "Niveau NTLM invalide (0 à 5 attendu).");
        WriteHklm(LsaKey, "LmCompatibilityLevel", level);
    }

    // ---------- DNSSEC : diagnostic complet (à la demande, pas dans RefreshAsync général) ----------
    //
    // Complète la clé de stratégie par deux vérifications concrètes :
    //   1. les règles NRPT exigeant la validation DNSSEC (Get-DnsClientNrptPolicy) ;
    //   2. une requête réelle vers un domaine signé valide (la signature doit être présente) ;
    //   3. une requête réelle vers dnssec-failed.org, zone volontairement cassée maintenue
    //      publiquement pour les tests — si la résolution échoue (SERVFAIL), une validation
    //      DNSSEC bloque effectivement les réponses falsifiées quelque part dans la chaîne (ce
    //      poste ou le résolveur configuré) ; si elle réussit, aucune validation effective ne
    //      protège ce poste, quel que soit l'état de la stratégie locale.
    // Get-DnsClientNrptPolicy peut échouer (classe WMI absente/non enregistrée selon le poste,
    // observé en pratique) : traité comme "indéterminé", jamais comme un crash ni un faux "non".
    // Les tests réseau dépendent de la connectivité : un résultat "indéterminé" est toujours
    // préféré à une affirmation non vérifiée.

    public sealed record DnssecDiagnostics(
        ProtocolStatus Policy,
        bool? NrptRequiresValidation, string NrptDetail,
        bool? SignedDomainOk, string SignedDomainDetail,
        bool? ForgedDomainBlocked, string ForgedDomainDetail,
        bool IsDomainJoined, string? DomainControllerName,
        bool? DcForgedDomainBlocked, string DcDetail);

    private const string DnssecSignedTestDomain = "dnssec-tools.org";
    private const string DnssecForgedTestDomain = "dnssec-failed.org";

    public static async Task<DnssecDiagnostics> RunDnssecDiagnosticsAsync(CancellationToken ct = default)
    {
        ProtocolStatus policy = GetDnssecStatus();

        var nrptTask = RunPowerShellJsonAsync(
            "try { " +
            "$r = @(Get-DnsClientNrptPolicy -ErrorAction Stop | Where-Object { $_.DnsSecValidationRequired }); " +
            "[PSCustomObject]@{ ok = $true; count = $r.Count } | ConvertTo-Json -Compress " +
            "} catch { [PSCustomObject]@{ ok = $false; error = $_.Exception.Message } | ConvertTo-Json -Compress }",
            ct);

        var signedTask = RunPowerShellJsonAsync(
            $"try {{ " +
            $"$r = @(Resolve-DnsName -Name '{DnssecSignedTestDomain}' -Type DNSKEY -DnssecOk -ErrorAction Stop); " +
            $"$sig = @($r | Where-Object Type -eq 'RRSIG').Count -gt 0; " +
            $"[PSCustomObject]@{{ ok = $true; signed = $sig }} | ConvertTo-Json -Compress " +
            $"}} catch {{ [PSCustomObject]@{{ ok = $false; error = $_.Exception.Message }} | ConvertTo-Json -Compress }}",
            ct);

        var forgedTask = RunPowerShellJsonAsync(
            $"try {{ " +
            $"Resolve-DnsName -Name '{DnssecForgedTestDomain}' -Type A -ErrorAction Stop | Out-Null; " +
            $"[PSCustomObject]@{{ ok = $true; resolved = $true }} | ConvertTo-Json -Compress " +
            $"}} catch {{ [PSCustomObject]@{{ ok = $true; resolved = $false; error = $_.Exception.Message }} | ConvertTo-Json -Compress }}",
            ct);

        // Détection du contrôleur de domaine via l'API .NET AD, chargée dynamiquement dans le
        // process PowerShell (pas de référence NuGet supplémentaire côté projet C#).
        var dcInfoTask = RunPowerShellJsonAsync(
            "try { " +
            "Add-Type -AssemblyName System.DirectoryServices -ErrorAction Stop; " +
            "$d = [System.DirectoryServices.ActiveDirectory.Domain]::GetComputerDomain(); " +
            "$dc = $d.FindDomainController(); " +
            "[PSCustomObject]@{ ok = $true; joined = $true; dc = $dc.Name } | ConvertTo-Json -Compress " +
            "} catch [System.DirectoryServices.ActiveDirectory.ActiveDirectoryObjectNotFoundException] { " +
            "[PSCustomObject]@{ ok = $true; joined = $false } | ConvertTo-Json -Compress " +
            "} catch { [PSCustomObject]@{ ok = $false; error = $_.Exception.Message } | ConvertTo-Json -Compress }",
            ct, timeoutMs: 6000);

        await Task.WhenAll(nrptTask, signedTask, forgedTask, dcInfoTask);

        (bool? nrptOk, string nrptDetail) = ParseNrptResult(await nrptTask);
        (bool? signedOk, string signedDetail) = ParseSignedResult(await signedTask);
        (bool? forgedBlocked, string forgedDetail) = ParseForgedResult(await forgedTask);
        (bool joined, string? dcName, string dcLookupDetail) = ParseDcInfoResult(await dcInfoTask);

        bool? dcForgedBlocked = null;
        string dcDetail = dcLookupDetail;
        if (joined && dcName != null)
        {
            JsonElement? dcForgedJson = await RunPowerShellJsonAsync(
                $"try {{ " +
                $"Resolve-DnsName -Name '{DnssecForgedTestDomain}' -Type A -Server '{dcName}' -ErrorAction Stop | Out-Null; " +
                $"[PSCustomObject]@{{ ok = $true; resolved = $true }} | ConvertTo-Json -Compress " +
                $"}} catch {{ [PSCustomObject]@{{ ok = $true; resolved = $false; error = $_.Exception.Message }} | ConvertTo-Json -Compress }}",
                ct);
            (bool? blocked, string blockedDetail) = ParseForgedResult(dcForgedJson, $"via le contrôleur de domaine {dcName}");
            dcForgedBlocked = blocked;
            dcDetail = $"Contrôleur de domaine détecté : {dcName}. " + blockedDetail;
        }

        return new DnssecDiagnostics(policy, nrptOk, nrptDetail, signedOk, signedDetail, forgedBlocked, forgedDetail,
            joined, dcName, dcForgedBlocked, dcDetail);
    }

    private static (bool joined, string? dcName, string detail) ParseDcInfoResult(JsonElement? json)
    {
        if (json is not { } j) return (false, null, "Appartenance à un domaine AD indéterminée (PowerShell injoignable ou délai dépassé).");
        if (!(j.TryGetProperty("ok", out var okProp) && okProp.GetBoolean()))
        {
            string err = j.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
            return (false, null, "Détection du domaine AD impossible : " + err);
        }
        bool joined = j.TryGetProperty("joined", out var jd) && jd.GetBoolean();
        if (!joined) return (false, null, "Poste non joint à un domaine Active Directory — pas de contrôleur de domaine à tester.");
        string? dcName = j.TryGetProperty("dc", out var d) ? d.GetString() : null;
        // Un nom de DC ne doit contenir que des caractères de nom d'hôte valides avant d'être
        // réinjecté dans un script PowerShell : garde-fou défensif même si la source (API AD) est fiable.
        if (dcName == null || !System.Text.RegularExpressions.Regex.IsMatch(dcName, @"^[A-Za-z0-9.\-]+$"))
            return (true, null, "Domaine AD détecté, mais nom de contrôleur de domaine invalide ou injoignable.");
        return (true, dcName, "");
    }

    private static (bool? ok, string detail) ParseNrptResult(JsonElement? json)
    {
        if (json is not { } j) return (null, "Test indéterminé (PowerShell injoignable ou délai dépassé).");
        if (j.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
        {
            int count = j.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
            return count > 0
                ? (true, $"{count} règle(s) NRPT exigent la validation DNSSEC (Get-DnsClientNrptPolicy).")
                : (false, "Aucune règle NRPT n'exige la validation DNSSEC — la stratégie globale seule ne suffit généralement pas en pratique.");
        }
        string err = j.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
        return (null, "Lecture NRPT impossible : " + err);
    }

    private static (bool? ok, string detail) ParseSignedResult(JsonElement? json)
    {
        if (json is not { } j) return (null, "Test indéterminé (pas de réponse réseau ou délai dépassé).");
        if (j.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
        {
            bool signed = j.TryGetProperty("signed", out var s) && s.GetBoolean();
            return signed
                ? (true, $"Signature RRSIG reçue pour {DnssecSignedTestDomain} — la chaîne de confiance DNSSEC répond correctement.")
                : (false, $"Aucune signature RRSIG reçue pour {DnssecSignedTestDomain} — résolution possible mais sans validation DNSSEC visible.");
        }
        string err = j.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
        return (null, $"Requête vers {DnssecSignedTestDomain} impossible (pas de réseau ?) : " + err);
    }

    private static (bool? ok, string detail) ParseForgedResult(JsonElement? json, string? via = null)
    {
        string suffix = via == null ? "" : " (" + via + ")";
        if (json is not { } j) return (null, "Test indéterminé (pas de réponse réseau ou délai dépassé)" + suffix + ".");
        bool resolved = j.TryGetProperty("resolved", out var r) && r.GetBoolean();
        return resolved
            ? (false, $"{DnssecForgedTestDomain} (zone volontairement invalide) a été résolu SANS erreur{suffix} — aucune validation DNSSEC effective ne protège ce poste par cette voie.")
            : (true, $"{DnssecForgedTestDomain} (zone volontairement invalide) a échoué comme attendu (SERVFAIL){suffix} — une validation DNSSEC bloque bien les réponses falsifiées.");
    }

    private static async Task<JsonElement?> RunPowerShellJsonAsync(string script, CancellationToken ct, int timeoutMs = 8000)
    {
        string output = await RunPowerShellAsync(script, ct, timeoutMs);
        if (string.IsNullOrWhiteSpace(output)) return null;
        try
        {
            return JsonDocument.Parse(output).RootElement;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> RunPowerShellAsync(string script, CancellationToken ct, int timeoutMs)
    {
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using Process? proc = Process.Start(psi);
        if (proc == null) return "";

        Task<string> stdoutTask = proc.StandardOutput.ReadToEndAsync();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return "";
        }
        return await stdoutTask;
    }

    // ---------- IPv6 ----------

    // DisabledComponents : 0 = IPv6 entièrement activé (par défaut), 0xFF (255) = entièrement
    // désactivé, 0x20 = tunnels Teredo/6to4 désactivés seulement (IPv6 natif conservé).
    // Important, à la différence de mDNS/LLMNR/NBT-NS : ce n'est PAS une recommandation de
    // durcissement officielle. Microsoft déconseille explicitement de désactiver IPv6
    // entièrement (KB929852) — plusieurs composants Windows en dépendent en interne même sur un
    // réseau purement IPv4. C'est une pratique répandue ("IPv6 ne sert à rien ici") mais pas un
    // correctif de vulnérabilité au même titre que les autres modules : le texte affiché à
    // l'utilisateur le dit explicitement plutôt que de le présenter comme un risque avéré.
    private const string Tcpip6ParamsKey = @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters";

    public static ProtocolStatus GetIpv6Status()
    {
        object? value = ReadHklm(Tcpip6ParamsKey, "DisabledComponents");
        int disabled = value is int i ? i : 0;
        ProtocolState state = disabled == 0 ? ProtocolState.Enabled : ProtocolState.Disabled;
        string detail = value == null
            ? "Non configuré — IPv6 entièrement activé par défaut"
            : disabled switch
            {
                0xFF => "DisabledComponents = 0xFF (255) — IPv6 entièrement désactivé",
                0x20 => "DisabledComponents = 0x20 (32) — tunnels Teredo/6to4 désactivés uniquement, IPv6 natif actif",
                _ => $"DisabledComponents = {disabled} (0x{disabled:X2})",
            };
        return new ProtocolStatus("ipv6", "IPv6", state, detail);
    }

    // enable=false désactive entièrement IPv6 (0xFF) — pas l'option intermédiaire 0x20, pour
    // rester cohérent avec la bascule simple Activer/Désactiver des autres modules. Le script
    // PowerShell généré rappelle l'alternative plus prudente en commentaire.
    public static void SetIpv6Enabled(bool enable)
    {
        WriteHklm(Tcpip6ParamsKey, "DisabledComponents", enable ? 0 : 0xFF);
    }

    // ---------- NBT-NS (par interface) ----------

    // NetbiosOptions : 0 = par défaut (suit le DHCP, NBT-NS actif en pratique), 1 = activé, 2 = désactivé.
    public static List<NbtInterfaceStatus> GetNbtNsInterfaces()
    {
        var names = GetAdapterFriendlyNames();
        var result = new List<NbtInterfaceStatus>();

        foreach (string guid in EnumerateSubKeys(NetBtInterfacesKey))
        {
            object? raw = ReadHklm(NetBtInterfacesKey + "\\" + guid, "NetbiosOptions");
            int option = raw is int i ? i : 0;
            ProtocolState state = option switch
            {
                2 => ProtocolState.Disabled,
                1 => ProtocolState.Enabled,
                _ => ProtocolState.Enabled, // 0 = par défaut, actif en pratique sauf réseau sans WINS/DHCP
            };
            string detail = option switch
            {
                2 => "NetbiosOptions = 2 (désactivé)",
                1 => "NetbiosOptions = 1 (activé)",
                _ => "NetbiosOptions = 0 (par défaut — suit le DHCP, actif en pratique)",
            };
            string name = names.TryGetValue(guid, out var n) ? n : guid;
            result.Add(new NbtInterfaceStatus(guid, name, state, detail));
        }
        return result;
    }

    public static void SetNbtNsForInterface(string adapterGuid, bool enabled)
    {
        // 1 = activé explicitement, 2 = désactivé. On n'utilise jamais 0 (par défaut) ici : une
        // bascule explicite doit rester explicite, pas retomber sur un comportement ambigu.
        WriteHklm(NetBtInterfacesKey + "\\" + adapterGuid, "NetbiosOptions", enabled ? 1 : 2);
    }

    // ---------- Prise d'effet ----------

    // Le service Dnscache lit EnableMDNS/EnableMulticast au démarrage : un redémarrage du
    // service (pas forcément du poste) suffit à appliquer mDNS/LLMNR immédiatement. Redémarrer
    // Dnscache vide aussi le cache DNS — effet secondaire mineur et sans danger.
    public static void RestartDnscacheService()
    {
        RestartService("Dnscache");
    }

    private static void RestartService(string serviceName)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_Service WHERE Name='" + serviceName + "'");
        using ManagementObjectCollection results = searcher.Get();
        foreach (ManagementObject service in results)
        {
            using (service)
            {
                service.InvokeMethod("StopService", null);
                System.Threading.Thread.Sleep(800);
                service.InvokeMethod("StartService", null);
                return;
            }
        }
        throw new InvalidOperationException($"Service {serviceName} introuvable.");
    }

    private static Dictionary<string, string> GetAdapterFriendlyNames()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SettingID, Description FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    string? settingId = mo["SettingID"]?.ToString();
                    string? description = mo["Description"]?.ToString();
                    if (!string.IsNullOrEmpty(settingId) && !string.IsNullOrEmpty(description))
                    {
                        map["Tcpip_" + settingId] = description;
                    }
                }
            }
        }
        catch
        {
            // Si WMI échoue, on retombe sur l'affichage du GUID brut (gestion dans l'appelant).
        }
        return map;
    }

    private static object? ReadHklm(string subKey, string valueName)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? key = baseKey.OpenSubKey(subKey, writable: false);
            return key?.GetValue(valueName);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateSubKeys(string subKey)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? key = baseKey.OpenSubKey(subKey, writable: false);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void WriteHklm(string subKey, string valueName, int value)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey key = baseKey.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Impossible d'ouvrir/créer {subKey} en écriture (droits administrateur requis).");
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }
}
