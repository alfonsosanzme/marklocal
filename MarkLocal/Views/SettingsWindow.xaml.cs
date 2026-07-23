using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using MarkLocal.Core;
using MarkLocal.Infrastructure;
using MarkLocal.Models;

namespace MarkLocal.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;

    public string? RequestOpenInEditorPath { get; private set; }

    public SettingsWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        LoadFromSettings();
        RefreshAssociationStatus();
        ConfigPathHint.Text = "Estos ajustes se guardan en: " + _settings.ConfigFilePath;
    }

    private void RefreshAssociationStatus()
    {
        bool associated = WindowsIntegration.IsAssociated();
        AssociationStatus.Text = associated
            ? "Estado actual: los archivos .md/.markdown abren con MarkLocal."
            : "Estado actual: los archivos .md/.markdown no abren con MarkLocal.";
        AssociateButton.IsEnabled = !associated;
        DisassociateButton.IsEnabled = associated;

        bool shellNew = WindowsIntegration.IsShellNewEnabled();
        ShellNewStatus.Text = shellNew
            ? "Estado actual: el menú \"Nuevo\" del Explorador incluye \"Nuevo Markdown\"."
            : "Estado actual: el menú \"Nuevo\" del Explorador NO incluye \"Nuevo Markdown\".";
        EnableShellNewButton.IsEnabled = !shellNew;
        DisableShellNewButton.IsEnabled = shellNew;
    }

    private void OnAssociateClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            WindowsIntegration.Associate();
            MessageBox.Show(this, "Asociación creada. Puede que necesites cerrar y abrir el explorador para verlo.", "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo asociar:\n" + ex.Message, "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshAssociationStatus();
    }

    private void OnDisassociateClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            WindowsIntegration.Disassociate();
            MessageBox.Show(this, "Asociación retirada para tu usuario.", "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo retirar la asociación:\n" + ex.Message, "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshAssociationStatus();
    }

    private void OnEnableShellNewClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            string templatesDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MarkLocal", "templates");
            System.IO.Directory.CreateDirectory(templatesDir);
            string templatePath = System.IO.Path.Combine(templatesDir, "shellnew.md");
            if (!System.IO.File.Exists(templatePath))
            {
                System.IO.File.WriteAllText(templatePath, "# Nuevo documento\n\n", new UTF8Encoding(false));
            }
            WindowsIntegration.EnableShellNew(templatePath);
            MessageBox.Show(this,
                "Listo. En el menú contextual del Explorador → \"Nuevo\" aparecerá ahora \"Documento Markdown\".\n\n" +
                "Si no lo ves de inmediato, cierra y abre una nueva ventana del Explorador.",
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo activar:\n" + ex.Message,
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshAssociationStatus();
    }

    private void OnDisableShellNewClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            WindowsIntegration.DisableShellNew();
            MessageBox.Show(this, "\"Nuevo Markdown\" retirado del menú contextual del Explorador.",
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo desactivar:\n" + ex.Message,
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshAssociationStatus();
    }

    private void LoadFromSettings()
    {
        var s = _settings.Settings;
        ThemeCombo.SelectedIndex = s.Theme switch
        {
            ThemePreference.Light => 1,
            ThemePreference.Dark => 2,
            _ => 0
        };
        FontFamilyBox.Text = s.FontFamily;
        FontSizeBox.Text = s.FontSize.ToString(CultureInfo.InvariantCulture);
        PreviewFontSizeBox.Text = s.PreviewFontSize.ToString(CultureInfo.InvariantCulture);
        WordWrapCheck.IsChecked = s.WordWrap;
        LineNumbersCheck.IsChecked = s.ShowLineNumbers;
        AutoPairCheck.IsChecked = s.AutoPairBrackets;
        OutlineCheck.IsChecked = s.ShowOutline;
        LineEndingCombo.SelectedIndex = s.DefaultLineEnding == LineEnding.LF ? 1 : 0;
        AssetsFolderBox.Text = s.AssetsFolderName;
        CopyImagesCheck.IsChecked = s.CopyImagesToAssets;
        AllowHtmlCheck.IsChecked = s.AllowInlineHtml;
        DebounceBox.Text = s.PreviewDebounceMs.ToString(CultureInfo.InvariantCulture);
        CustomCssBox.Text = s.CustomCssPath ?? string.Empty;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        var s = _settings.Settings;
        s.Theme = ThemeCombo.SelectedIndex switch
        {
            1 => ThemePreference.Light,
            2 => ThemePreference.Dark,
            _ => ThemePreference.System
        };
        s.FontFamily = string.IsNullOrWhiteSpace(FontFamilyBox.Text) ? "Consolas" : FontFamilyBox.Text.Trim();
        if (double.TryParse(FontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fs)) s.FontSize = fs;
        if (double.TryParse(PreviewFontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pfs)) s.PreviewFontSize = pfs;
        s.WordWrap = WordWrapCheck.IsChecked == true;
        s.ShowLineNumbers = LineNumbersCheck.IsChecked == true;
        s.AutoPairBrackets = AutoPairCheck.IsChecked == true;
        s.ShowOutline = OutlineCheck.IsChecked == true;
        s.DefaultLineEnding = LineEndingCombo.SelectedIndex == 1 ? LineEnding.LF : LineEnding.CRLF;
        s.AssetsFolderName = string.IsNullOrWhiteSpace(AssetsFolderBox.Text) ? "assets" : AssetsFolderBox.Text.Trim();
        s.CopyImagesToAssets = CopyImagesCheck.IsChecked == true;
        s.AllowInlineHtml = AllowHtmlCheck.IsChecked == true;
        if (int.TryParse(DebounceBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deb))
        {
            s.PreviewDebounceMs = System.Math.Clamp(deb, 50, 2000);
        }
        s.CustomCssPath = string.IsNullOrWhiteSpace(CustomCssBox.Text) ? null : CustomCssBox.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void OnBrowseCustomCss(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Hojas de estilo CSS (*.css)|*.css|Todos los archivos (*.*)|*.*",
            CheckFileExists = true
        };
        if (!string.IsNullOrWhiteSpace(CustomCssBox.Text))
        {
            try { dlg.InitialDirectory = Path.GetDirectoryName(CustomCssBox.Text); } catch { }
        }
        if (dlg.ShowDialog(this) == true)
        {
            CustomCssBox.Text = dlg.FileName;
        }
    }

    private void OnRemoveCustomCss(object sender, RoutedEventArgs e)
    {
        CustomCssBox.Text = string.Empty;
    }

    private void OnOpenCustomCss(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CustomCssBox.Text) || !File.Exists(CustomCssBox.Text))
        {
            MessageBox.Show(this, "Primero indica un archivo CSS existente.", "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        RequestOpenInEditorPath = CustomCssBox.Text;
        _settings.Settings.CustomCssPath = CustomCssBox.Text;
        DialogResult = true;
        Close();
    }

    private void OnCreateCssTemplate(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Hojas de estilo CSS (*.css)|*.css",
            DefaultExt = ".css",
            FileName = "marklocal-custom.css"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, BuildCssTemplate(), new UTF8Encoding(false));
            CustomCssBox.Text = dlg.FileName;
            MessageBox.Show(this,
                "Plantilla creada. Pulsa \"Guardar\" para aplicarla, o \"Abrir en el editor\" para editarla aquí mismo.",
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo crear la plantilla:\n" + ex.Message,
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string BuildCssTemplate() =>
@"/* MarkLocal — CSS personalizado del preview.
   Se aplica después del CSS del tema, así que basta con redefinir lo que quieras.
   Inspecciona los selectores reales abriendo DevTools desde la app... ah, no, aún no.
   De momento, esto es lo que más se suele tocar:
*/

/* Anchura del contenido y márgenes */
#content {
  /* max-width: 920px; */
  /* padding: 0 36px; */
}

/* Tipografía base */
html, body {
  /* font-family: 'Source Sans 3', 'Segoe UI', sans-serif; */
  /* line-height: 1.7; */
}

/* Encabezados */
#content h1 { /* color: #1a73e8; */ }
#content h2 { /* border-bottom-color: #ccc; */ }

/* Citas */
#content blockquote {
  /* border-left-color: #9c27b0; */
  /* background: transparent; */
}

/* Bloques de código */
#content pre {
  /* background: #0d1117; */
  /* color: #e6edf3; */
}

/* Enlaces */
#content a {
  /* color: #c2185b; */
  /* text-decoration: underline; */
}

/* Print: si exportas a PDF puedes forzar otros estilos aquí */
@media print {
  #content {
    /* max-width: 100%; */
  }
}
";

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
