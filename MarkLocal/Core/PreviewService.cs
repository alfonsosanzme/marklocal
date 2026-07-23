using System;
using System.IO;
using System.Text;
using MarkLocal.Models;

namespace MarkLocal.Core;

public class PreviewService
{
    private readonly SettingsService _settings;
    private string? _lightCss;
    private string? _darkCss;
    private string? _customCss;
    private string? _customCssCachedPath;
    private DateTime _customCssCachedMtimeUtc;
    private string? _mermaidScript;
    private DateTime _mermaidScriptMtimeUtc;
    private bool _mermaidScriptLoaded;

    public PreviewService(SettingsService settings)
    {
        _settings = settings;
    }

    public string BuildHtml(string bodyHtml, bool useDarkTheme, double fontSizePx)
    {
        string css = LoadCss(useDarkTheme);
        string customCss = LoadCustomCss();
        string mermaid = LoadMermaidScript();
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"es\" data-theme=\"" + (useDarkTheme ? "dark" : "light") + "\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'self' file: data:; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' file: data: https:;\">");
        sb.AppendLine("<style>");
        sb.AppendLine($":root {{ --preview-font-size: {fontSizePx.ToString(System.Globalization.CultureInfo.InvariantCulture)}px; }}");
        sb.AppendLine(css);
        if (!string.IsNullOrWhiteSpace(customCss))
        {
            sb.AppendLine("/* ===== CSS personalizado del usuario ===== */");
            sb.AppendLine(customCss);
        }
        if (!string.IsNullOrEmpty(mermaid))
        {
            sb.AppendLine("/* Bloques mermaid se renderizan como diagramas: ocultamos el bloque de código original */");
            sb.AppendLine("#content pre > code.language-mermaid { display:none; }");
            sb.AppendLine(".mermaid { background: transparent; text-align: center; }");
        }
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<main id=\"content\">");
        sb.Append(bodyHtml);
        sb.AppendLine("</main>");
        sb.AppendLine(BuildScrollScript());
        if (!string.IsNullOrEmpty(mermaid))
        {
            sb.AppendLine("<script>");
            sb.AppendLine(mermaid);
            sb.AppendLine("</script>");
            sb.AppendLine("<script>");
            sb.AppendLine("(function(){");
            sb.AppendLine("  document.querySelectorAll('pre > code.language-mermaid').forEach(function(code){");
            sb.AppendLine("    var div = document.createElement('div');");
            sb.AppendLine("    div.className = 'mermaid';");
            sb.AppendLine("    div.textContent = code.textContent;");
            sb.AppendLine("    var pre = code.parentElement;");
            sb.AppendLine("    pre.parentElement.insertBefore(div, pre);");
            sb.AppendLine("    pre.remove();");
            sb.AppendLine("  });");
            sb.AppendLine("  if (window.mermaid) {");
            sb.AppendLine("    try { mermaid.initialize({ startOnLoad: true, theme: '" + (useDarkTheme ? "dark" : "default") + "', securityLevel: 'strict' }); }");
            sb.AppendLine("    catch (e) { console && console.warn && console.warn(e); }");
            sb.AppendLine("  }");
            sb.AppendLine("})();");
            sb.AppendLine("</script>");
        }
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private string LoadCss(bool dark)
    {
        if (dark)
        {
            return _darkCss ??= ReadCss("preview-dark.css");
        }
        return _lightCss ??= ReadCss("preview.css");
    }

    private static string ReadCss(string name)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string fullPath = Path.Combine(baseDir, "Assets", name);
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }
        }
        catch
        {
        }
        return string.Empty;
    }

    public void Invalidate()
    {
        _lightCss = null;
        _darkCss = null;
        _customCss = null;
        _customCssCachedPath = null;
        _customCssCachedMtimeUtc = default;
        _mermaidScript = null;
        _mermaidScriptLoaded = false;
        _mermaidScriptMtimeUtc = default;
    }

    private string LoadMermaidScript()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string libPath = Path.Combine(baseDir, "Assets", "lib", "mermaid.min.js");
            if (!File.Exists(libPath))
            {
                _mermaidScript = null;
                _mermaidScriptLoaded = true;
                return string.Empty;
            }
            DateTime mtime = File.GetLastWriteTimeUtc(libPath);
            if (_mermaidScriptLoaded && _mermaidScript != null && _mermaidScriptMtimeUtc == mtime)
            {
                return _mermaidScript;
            }
            _mermaidScript = File.ReadAllText(libPath);
            _mermaidScriptMtimeUtc = mtime;
            _mermaidScriptLoaded = true;
            return _mermaidScript;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string LoadCustomCss()
    {
        string? path = _settings.Settings.CustomCssPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _customCss = null;
            _customCssCachedPath = null;
            return string.Empty;
        }

        DateTime mtime;
        try { mtime = File.GetLastWriteTimeUtc(path); }
        catch { return string.Empty; }

        if (_customCss != null
            && string.Equals(_customCssCachedPath, path, StringComparison.OrdinalIgnoreCase)
            && _customCssCachedMtimeUtc == mtime)
        {
            return _customCss;
        }

        try
        {
            _customCss = File.ReadAllText(path);
            _customCssCachedPath = path;
            _customCssCachedMtimeUtc = mtime;
            return _customCss;
        }
        catch
        {
            _customCss = string.Empty;
            _customCssCachedPath = path;
            _customCssCachedMtimeUtc = mtime;
            return _customCss;
        }
    }

    private static string BuildScrollScript() => @"<script>
var __lastIncomingScroll = 0;
var __lastOutgoingScroll = 0;

function __headingPositions() {
  var list = [];
  document.querySelectorAll('#content h1[id],#content h2[id],#content h3[id],#content h4[id],#content h5[id],#content h6[id]')
    .forEach(function (h) {
      list.push({ id: h.id, y: h.getBoundingClientRect().top + window.scrollY });
    });
  list.sort(function (a, b) { return a.y - b.y; });
  return list;
}

function __resolveY(id, fallback) {
  if (!id) return fallback;
  var el = document.getElementById(id);
  if (!el) return fallback;
  return el.getBoundingClientRect().top + window.scrollY;
}

window.addEventListener('message', function (e) {
  try {
    var data = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
    if (!data) return;
    if (data.type === 'scroll-to-anchor' && data.anchor) {
      __lastIncomingScroll = Date.now();
      var el = document.getElementById(data.anchor);
      if (el) el.scrollIntoView({behavior:'smooth', block:'start'});
    }
    if (data.type === 'scroll-to-ratio' && typeof data.ratio === 'number') {
      __lastIncomingScroll = Date.now();
      var max = Math.max(document.documentElement.scrollHeight - document.documentElement.clientHeight, 1);
      window.scrollTo({top: max * data.ratio, behavior:'auto'});
    }
    if (data.type === 'sync-scroll') {
      __lastIncomingScroll = Date.now();
      var docH = document.documentElement.scrollHeight;
      var yPrev = __resolveY(data.prev, 0);
      var yNext = __resolveY(data.next, docH);
      if (yNext < yPrev) yNext = docH;
      var frac = typeof data.frac === 'number' ? Math.max(0, Math.min(1, data.frac)) : 0;
      var target = yPrev + frac * (yNext - yPrev);
      window.scrollTo({top: Math.max(0, target), behavior:'auto'});
    }
  } catch (err) {}
});

window.addEventListener('scroll', function () {
  var now = Date.now();
  if (now - __lastIncomingScroll < 250) return;     // viene del editor: no rebotar
  if (now - __lastOutgoingScroll < 80) return;      // throttle
  __lastOutgoingScroll = now;
  var y = window.scrollY;
  var max = Math.max(document.documentElement.scrollHeight - document.documentElement.clientHeight, 1);
  var ratio = Math.max(0, Math.min(1, y / max));
  var hs = __headingPositions();
  var prev = null, next = null, yPrev = 0, yNext = document.documentElement.scrollHeight;
  for (var i = 0; i < hs.length; i++) {
    if (hs[i].y <= y + 1) { prev = hs[i].id; yPrev = hs[i].y; }
    else { next = hs[i].id; yNext = hs[i].y; break; }
  }
  var span = Math.max(yNext - yPrev, 1);
  var frac = Math.max(0, Math.min(1, (y - yPrev) / span));
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage(JSON.stringify({type:'sync-scroll-back', prev: prev, next: next, frac: frac, ratio: ratio}));
  }
}, {passive: true});

document.addEventListener('click', function (e) {
  var a = e.target.closest('a');
  if (!a) return;
  var href = a.getAttribute('href') || '';
  if (href.startsWith('#')) {
    var id = href.substring(1);
    var target = document.getElementById(id) || document.getElementsByName(id)[0];
    if (target) {
      e.preventDefault();
      target.scrollIntoView({behavior:'smooth', block:'start'});
    }
    return;
  }
  if (window.chrome && window.chrome.webview && /^(https?:|mailto:|file:)/.test(href)) {
    e.preventDefault();
    window.chrome.webview.postMessage(JSON.stringify({type:'open-external', href: href}));
  }
});

// Edición básica desde la preview: checkboxes de listas de tareas clicables.
(function(){
  var boxes = document.querySelectorAll('#content input[type=checkbox]');
  boxes.forEach(function(cb, i){
    cb.disabled = false;
    cb.style.cursor = 'pointer';
    cb.addEventListener('change', function(){
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({type:'toggle-task', index: i, checked: cb.checked}));
      }
    });
  });
})();

// Doble clic en cualquier parte del contenido: pedir al host abrir el editor en esa sección.
document.addEventListener('dblclick', function (e) {
  if (e.target.closest('a') || e.target.closest('input')) return;
  var el = e.target.closest('h1,h2,h3,h4,h5,h6,p,li,pre,blockquote,table');
  var anchor = '';
  var node = el;
  while (node) {
    if (/^H[1-6]$/.test(node.tagName) && node.id) { anchor = node.id; break; }
    var prev = node.previousElementSibling;
    while (prev) {
      if (/^H[1-6]$/.test(prev.tagName) && prev.id) { anchor = prev.id; break; }
      prev = prev.previousElementSibling;
    }
    if (anchor) break;
    node = node.parentElement;
  }
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage(JSON.stringify({type:'edit-here', anchor: anchor}));
  }
});
</script>";
}
