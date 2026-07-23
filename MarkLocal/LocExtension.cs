using System;
using System.Windows.Markup;
using MarkLocal.Core;

namespace MarkLocal;

/// <summary>
/// Markup extension para textos localizados en XAML: Header="{local:Loc main.menu.file}".
/// Resuelve una sola vez al construir la vista; el cambio de idioma requiere reiniciar.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class LocExtension : MarkupExtension
{
    public string Key { get; set; }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
