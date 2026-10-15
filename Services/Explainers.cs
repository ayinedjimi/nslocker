namespace NSLocker.Ui.Services;

/// <summary>Textes pédagogiques de chaque protocole (repris tels quels de l'ancienne interface web).</summary>
/// <param name="RiskyWhenEnabled">
/// Vrai pour mDNS/LLMNR/NBT-NS (l'état "Activé" est le mauvais état, affiché en rouge).
/// Faux pour DNSSEC, où c'est l'inverse : "Activé" est le bon état (affiché en vert).
/// </param>
public sealed record Explainer(string Key, string Title, string Subtitle, string Risk, string SideEffects, string ApplyNote, bool RiskyWhenEnabled = true);

public static class Explainers
{
    public const string DnsRestartNote =
        "Prise d'effet : le service DNS Client (Dnscache) lit ce réglage à son démarrage. " +
        "Redémarrez-le pour une prise en compte immédiate, sinon elle aura lieu au prochain redémarrage du poste. " +
        "Redémarrer ce service vide aussi le cache DNS (sans danger).";

    public const string NbtRestartNote =
        "Prise d'effet : le pilote NetBT lit ce réglage au chargement de l'interface. " +
        "Prise d'effet garantie uniquement après un redémarrage du poste (ou une désactivation/réactivation manuelle de la carte réseau concernée).";

    public const string Overview =
        "mDNS, LLMNR et NBT-NS permettent à un poste de résoudre des noms sans serveur DNS, en diffusant la demande sur le réseau local. " +
        "Un attaquant présent sur ce même réseau peut répondre à la place de la vraie cible et intercepter des identifiants (attaques de type Responder). " +
        "Les désactiver quand ils ne sont pas nécessaires réduit cette surface d'attaque.";

    public static readonly Explainer Mdns = new("mdns", "mDNS",
        "Multicast DNS — résolution de noms .local sans serveur DNS (utilisé par Bonjour/AirPrint et le découvreur réseau Windows).",
        "mDNS diffuse les requêtes de résolution de noms à tout le réseau local, sans authentification. Un attaquant sur le même réseau (outil type Responder) peut répondre à la place de la vraie machine et intercepter des identifiants (hash NTLM) ou rediriger le trafic.",
        "Certains appareils qui se découvrent par nom .local peuvent devenir injoignables par nom : imprimantes AirPrint/Bonjour, appareils Chromecast/IoT, ou la découverte réseau Windows. L'accès par adresse IP directe n'est pas affecté.",
        DnsRestartNote);

    public static readonly Explainer Llmnr = new("llmnr", "LLMNR",
        "Link-Local Multicast Name Resolution — protocole de secours de résolution de noms sur le réseau local, sans serveur DNS.",
        "LLMNR est utilisé quand une requête DNS classique échoue : la machine diffuse alors la demande en clair sur le réseau local. C'est le vecteur principal des attaques Responder/Inveigh pour voler des identifiants Windows (NTLM) sans aucune interaction utilisateur.",
        "Sur un poste avec un serveur DNS fonctionnel (réseau d'entreprise, domaine Active Directory), l'impact est quasiment nul — c'est pour cela que c'est l'un des durcissements les plus recommandés. Sur un petit réseau sans DNS local (ad-hoc, dépannage), la résolution de noms entre machines voisines peut cesser de fonctionner.",
        DnsRestartNote);

    public static readonly Explainer NbtNs = new("nbtns", "NBT-NS",
        "NetBIOS over TCP/IP — réglage par carte réseau. Chaque interface est listée séparément ci-dessous.",
        "NBT-NS est l'ancêtre de LLMNR (hérité de NetBIOS) : même principe de diffusion non authentifiée sur le réseau local, et même exploitable par Responder pour le vol d'identifiants NTLM — souvent utilisé en repli automatique quand LLMNR est désactivé seul, d'où l'intérêt de désactiver les deux.",
        "Peut casser l'accès par nom à des équipements très anciens qui ne connaissent que NetBIOS (vieux NAS, imprimantes/scanners legacy, parc pré-Windows 2000), ainsi que le « Voisinage réseau » historique et les environnements dépendant d'un serveur WINS.",
        NbtRestartNote);

    public static readonly Explainer Dnssec = new("dnssec", "DNSSEC",
        "Validation cryptographique des réponses DNS (signatures) — vérifie qu'une réponse DNS n'a pas été falsifiée en chemin.",
        "Sans validation DNSSEC, rien ne garantit qu'une réponse DNS provient bien du vrai serveur faisant autorité : un attaquant en position d'interception (réseau local, Wi-Fi public, FAI compromis) peut forger de fausses réponses DNS (DNS spoofing/cache poisoning) pour rediriger le trafic sans que l'utilisateur ne s'en aperçoive.\n\n" +
        "Important : ce réglage active une stratégie globale, mais n'impose pas à lui seul une validation par domaine — celle-ci passe par des règles NRPT (Name Resolution Policy Table). Sur un poste isolé, une règle NRPT peut être ajoutée localement sans GPO (Add-DnsClientNrptRule -DnssecValidationRequired). En environnement de domaine Active Directory, les règles NRPT sont généralement distribuées par Stratégie de groupe (GPO) : dans ce cas, activer ce seul réglage ici ne suffit pas, il faut que l'administrateur de domaine configure la NRPT via GPO.",
        "",
        DnsRestartNote, RiskyWhenEnabled: false);

    public static readonly Explainer Ipv6 = new("ipv6", "IPv6",
        "Pile réseau IPv6 — activée par défaut sur Windows, souvent inutilisée en pratique sur un réseau purement IPv4.",
        "Contrairement aux autres modules de cette application, ce n'est pas un correctif de vulnérabilité : IPv6 actif mais inutilisé n'est pas en soi exploitable comme le sont LLMNR/NBT-NS/mDNS. " +
        "C'est surtout une réduction de surface « par principe » (moins de piles réseau actives à maintenir/surveiller) — utile si rien sur le réseau n'utilise IPv6, sans urgence de sécurité particulière.",
        "Microsoft déconseille explicitement de désactiver IPv6 entièrement (KB929852) : plusieurs composants Windows en dépendent en interne (DirectAccess, certains services de découverte), même sur un réseau sans IPv6 natif. " +
        "Une alternative plus prudente, non proposée ici en un clic, consiste à ne désactiver que les tunnels Teredo/6to4 (DisabledComponents = 0x20) tout en gardant IPv6 natif — voir le script PowerShell généré, qui rappelle cette option en commentaire.",
        "Prise d'effet : nécessite un redémarrage du poste (composant chargé au démarrage du système).");

    public const string NtlmRisk =
        "Le niveau d'authentification réseau détermine quels protocoles ce poste accepte d'envoyer/recevoir : LM et NTLMv1 sont cryptographiquement faibles " +
        "(craquables hors ligne, vulnérables au relais). Un niveau bas permet à un attaquant en position d'interception ou de relais (Responder, ntlmrelayx) " +
        "d'exploiter des identifiants interceptés bien plus facilement qu'avec NTLMv2 exigé.";

    public const string NtlmSideEffects =
        "Monter le niveau (surtout 4-5, qui refusent LM/NTLM) peut casser l'authentification vers de très anciens serveurs/périphériques qui ne parlent que " +
        "LM ou NTLMv1 (NAS anciens, certains automates industriels, systèmes pré-Windows 2000/XP SP2). Sur un parc homogène récent, l'impact est généralement nul.";

    public const string NtlmApplyNote =
        "Prise d'effet : LmCompatibilityLevel est lu par LSA au démarrage du système. Un redémarrage du poste est recommandé pour une prise en compte garantie " +
        "(certaines sessions/connexions déjà établies peuvent continuer d'utiliser l'ancien niveau jusqu'au redémarrage).";

    public static Explainer ByKey(string key) => key switch
    {
        "mdns" => Mdns,
        "llmnr" => Llmnr,
        "dnssec" => Dnssec,
        _ => NbtNs,
    };
}
