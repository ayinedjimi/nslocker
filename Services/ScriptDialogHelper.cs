using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace NSLocker.Ui.Services;

/// <summary>Affiche un script PowerShell généré dans une boîte de dialogue, avec un bouton Copier.</summary>
public static class ScriptDialogHelper
{
    public static async Task ShowAsync(XamlRoot xamlRoot, string title, string script)
    {
        // Normalise les fins de ligne (PowerShellScriptBuilder produit du \n).
        string normalized = script.Replace("\r\n", "\n").Replace("\n", "\r\n");

        // Un TextBox WinUI3 affecté via Text = ... hors arbre visuel n'a, dans certains cas
        // constatés, rendu que sa première ligne tant qu'aucune interaction ne force une
        // nouvelle mesure (le Text contenait pourtant bien tout le script — vérifié via le
        // presse-papiers). Un TextBlock sélectionnable dans un ScrollViewer n'a pas ce problème
        // et c'est de toute façon le même procédé que pour le texte sélectionnable ailleurs
        // dans l'app (IsTextSelectionEnabled).
        var text = new TextBlock
        {
            Text = normalized,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12.5,
        };
        var scroller = new ScrollViewer
        {
            Content = text,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 380,
            Width = 640,
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = scroller,
            PrimaryButtonText = "Copier",
            CloseButtonText = "Fermer",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var package = new DataPackage();
            package.SetText(normalized);
            Clipboard.SetContent(package);
            args.Cancel = true; // garde la boîte ouverte après la copie
        };
        await dialog.ShowAsync();
    }
}
