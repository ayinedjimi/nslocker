using System;

namespace NSLocker.Services;

/// <summary>
/// Génère le texte d'un script PowerShell équivalent à l'action qu'un bouton de l'application
/// effectuerait — pour relecture, déploiement via GPO/SCCM/Intune, ou simple transparence sur ce
/// que fait réellement l'application. Pure génération de texte : aucune exécution, aucun effet
/// de bord, jamais de risque à appeler ces méthodes.
/// </summary>
public static class PowerShellScriptBuilder
{
    public static string Mdns(bool enable) => Wrap(
        "mDNS (Multicast DNS)",
        RegSetBlock(@"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters", "EnableMDNS", enable ? 1 : 0,
            (enable ? "Active" : "Désactive") + " mDNS.") +
        RestartDnscacheBlock());

    public static string Llmnr(bool enable) => Wrap(
        "LLMNR",
        RegSetBlock(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", "EnableMulticast", enable ? 1 : 0,
            (enable ? "Active" : "Désactive") + " LLMNR.") +
        RestartDnscacheBlock());

    public static string Dnssec(bool enable) => Wrap(
        "DNSSEC (stratégie globale)",
        RegSetBlock(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", "EnableDnssec", enable ? 1 : 0,
            (enable ? "Active" : "Désactive") + " la stratégie globale DNSSEC.") +
        RestartDnscacheBlock() +
        "\n# Rappel : une validation DNSSEC effective par domaine nécessite en plus une règle NRPT, p. ex. :\n" +
        "# Add-DnsClientNrptRule -Namespace '.' -DnssecValidationRequired $true\n" +
        "# En environnement de domaine Active Directory, ces règles sont normalement distribuées par GPO.\n");

    public static string Ntlm(int level)
    {
        if (level is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(level));
        return Wrap(
            $"NTLM — {NetworkSecurityService.NtlmLevelLabels[level]}",
            RegSetBlock(@"SYSTEM\CurrentControlSet\Control\Lsa", "LmCompatibilityLevel", level,
                $"Définit LmCompatibilityLevel = {level} ({NetworkSecurityService.NtlmLevelLabels[level]}).") +
            "\n# LSA lit cette valeur au démarrage : redémarrage du poste recommandé pour une prise en compte garantie.\n");
    }

    public static string Ipv6(bool enable) => Wrap(
        "IPv6",
        RegSetBlock(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", enable ? 0 : 0xFF,
            (enable ? "Réactive" : "Désactive entièrement") + " IPv6.") +
        "\n# Redémarrage du poste requis.\n" +
        "# Microsoft déconseille officiellement de désactiver IPv6 entièrement (KB929852) :\n" +
        "# plusieurs composants Windows en dépendent en interne. Alternative plus prudente,\n" +
        "# désactiver seulement les tunnels Teredo/6to4 en gardant IPv6 natif actif :\n" +
        "# New-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip6\\Parameters' -Name 'DisabledComponents' -Value 0x20 -PropertyType DWord -Force\n");

    public static string NbtNs(string adapterGuid, string adapterName, bool enable) => Wrap(
        $"NBT-NS — {adapterName}",
        RegSetBlock(@"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces\" + adapterGuid, "NetbiosOptions",
            enable ? 1 : 2, (enable ? "Active" : "Désactive") + $" NetBIOS sur l'interface « {adapterName} ».") +
        "\n# Redémarrage du poste recommandé (ou désactivation/réactivation manuelle de cette carte réseau).\n");

    private static string RegSetBlock(string hiveSubKey, string valueName, int value, string comment)
    {
        return
            $"# {comment}\n" +
            $"$path = 'HKLM:\\{hiveSubKey}'\n" +
            "if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }\n" +
            $"New-ItemProperty -Path $path -Name '{valueName}' -Value {value} -PropertyType DWord -Force | Out-Null\n";
    }

    private static string RestartDnscacheBlock() =>
        "\n# Redémarre le service DNS Client pour une prise en compte immédiate (vide aussi le cache DNS, sans danger).\n" +
        "Restart-Service -Name Dnscache -Force\n";

    private static string Wrap(string title, string body) =>
        "#Requires -RunAsAdministrator\n" +
        $"# NSLOCKER — {title}\n" +
        $"# Script généré le {DateTime.Now:yyyy-MM-dd HH:mm} — à relire avant exécution.\n\n" +
        body;
}
