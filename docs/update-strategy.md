# Estrategia de actualizaciones de MarkLocal

Documento operativo. Cubre cómo se publica una versión nueva, cómo se entrega al usuario y cómo el sistema garantiza un upgrade limpio.

---

## Resumen ejecutivo

- El MSI (`scripts/build-msi.ps1`) genera siempre el mismo **UpgradeCode** y un **ProductCode** nuevo por versión. Eso es lo que hace que Windows reconozca cada instalación nueva como upgrade de la anterior.
- En la app hay (opcionalmente) un **comprobador de versión manual** que lee un JSON en `kairis.es` y, si hay versión nueva, ofrece abrir el navegador para descargar el MSI.
- No hay auto-update silencioso. El usuario siempre decide.

---

## 1. Cómo Windows distingue una versión nueva

Cada MSI contiene tres GUID/strings que marcan su identidad:

| Propiedad | Cambia entre versiones | Para qué sirve |
| --- | --- | --- |
| `UpgradeCode` | **No** (fijo) | Identifica el producto a lo largo de toda su vida. Lo que comparte v0.1.0, v0.1.1, v2.0.0… |
| `ProductCode` | **Sí** (cada versión) | Identifica esta versión concreta. WiX lo regenera automáticamente cuando subes `Version`. |
| `ProductVersion` | **Sí** | El número semántico (0.1.0, 0.1.1…) |

Cuando un usuario ejecuta un MSI nuevo:

1. Windows lee su `UpgradeCode`.
2. Busca otros productos con el mismo `UpgradeCode` ya instalados.
3. Si encuentra una versión menor, dispara una *Major Upgrade*: **desinstala** la anterior y **luego** instala la nueva.
4. Si la versión instalada es **mayor**, el MSI nuevo se rechaza con el mensaje declarado en `DowngradeErrorMessage`.

El UpgradeCode actual de MarkLocal está hardcodeado en [`installer/wix/MarkLocal.wxs`](../installer/wix/MarkLocal.wxs):

```
UpgradeCode = 1C9CE61E-67A1-453C-9869-C4BF713037DC
```

**No lo cambies nunca**, ni siquiera al hacer cambios mayores. Si alguna vez se cambia, los usuarios existentes tendrán que desinstalar a mano antes de poder instalar la nueva.

---

## 2. Numeración semántica (semver)

El campo `Version` del [`MarkLocal.csproj`](../MarkLocal/MarkLocal.csproj) sigue **MAYOR.MENOR.PATCH**:

- **PATCH** (`0.1.0` → `0.1.1`): bugfixes que no rompen nada visible.
- **MENOR** (`0.1.x` → `0.2.0`): funcionalidad nueva, cambios visibles pero compatibles. Aprovecha para limpiar settings deprecados.
- **MAYOR** (`0.x` → `1.0.0`): cambios incompatibles. Por ejemplo, cambiar el formato de `settings.json` o la ruta de los borradores.

Windows MSI requiere internamente un formato `M.m.b[.r]`. WiX permite hasta `0.0.0.0`. Para semver puro basta con `M.m.b`.

---

## 3. Flujo paso a paso para publicar una versión nueva

Asumiendo que estás en la rama estable y los cambios están commiteados:

```powershell
# 1. Subir la versión en el csproj. Edita <Version>0.1.0</Version> → <Version>0.1.1</Version>.

# 2. Compilar y verificar tests.
dotnet build .\MarkLocal\MarkLocal.csproj -c Release
dotnet test  .\MarkLocal.Tests\MarkLocal.Tests.csproj

# 3. Generar el portable.
powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-portable.ps1

# 4. Generar el MSI.
powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-msi.ps1

# 5. Resultado en .\dist\:
#    MarkLocal-portable-win-x64-v0.1.1.zip
#    MarkLocal-v0.1.1.msi

# 6. Probar la instalación de upgrade en una máquina con la versión anterior:
msiexec /i .\dist\MarkLocal-v0.1.1.msi /qb

# 7. Verificar:
#    - Get-Package -ProviderName msi | ? Name -like *MarkLocal* → 1 sola entrada con la nueva versión.
#    - El exe en %LocalAppData%\Programs\MarkLocal\MarkLocal.exe tiene la versión correcta.

# 8. Subir los dos artefactos a kairis.es / GitHub Releases / etc.

# 9. Publicar el JSON del feed (ver siguiente sección).

# 10. Etiquetar el commit con la versión y empujar al remoto:
git tag v0.1.1
git push origin v0.1.1
```

---

## 4. El feed de actualizaciones (opcional)

La app puede comprobar si hay una versión nueva contra un JSON publicado en una URL HTTPS. El usuario lo dispara desde **Ayuda → Buscar actualizaciones…**.

### Formato del JSON

```json
{
  "latestVersion": "0.2.0",
  "minSupportedVersion": "0.1.0",
  "releaseDate": "2026-06-15",
  "downloadPageUrl": "https://kairis.es/marklocal/download",
  "msi": {
    "url": "https://kairis.es/marklocal/releases/MarkLocal-v0.2.0.msi",
    "sha256": "AABBCCDD..."
  },
  "portable": {
    "url": "https://kairis.es/marklocal/releases/MarkLocal-portable-win-x64-v0.2.0.zip",
    "sha256": "EEFF00112233..."
  },
  "releaseNotesUrl": "https://kairis.es/marklocal/releases/0.2.0",
  "releaseNotesText": "- Nuevo modo distracción cero.\n- Mejora del scroll sincronizado."
}
```

Campos:

| Campo | Obligatorio | Uso |
| --- | --- | --- |
| `latestVersion` | sí | Compararla con la versión local. |
| `minSupportedVersion` | no | Si la versión local es menor, mostrar "Tu versión ya no recibe actualizaciones, actualiza obligatoriamente". |
| `releaseDate` | no | Solo informativo. |
| `downloadPageUrl` | sí | URL que abrirá la app si el usuario acepta. |
| `msi.url` / `portable.url` | no | Permite descarga directa desde la app en el futuro. |
| `*.sha256` | no | Para verificar integridad si se automatiza la descarga. |
| `releaseNotes*` | no | Se muestran en el diálogo de actualización. |

### Configuración en el cliente

`AppSettings.UpdateFeedUrl` controla qué URL se consulta. Por defecto está vacío y la comprobación está **desactivada**. Cuando se quiera activar oficialmente:

```json
{
  "updateFeedUrl": "https://kairis.es/marklocal/updates.json"
}
```

El usuario puede sobrescribirlo en su `settings.json` si quiere usar un feed alternativo (mirrors, instalaciones internas de la empresa, etc.).

---

## 5. Estrategias de update consideradas

| Estrategia | Esfuerzo | Decisión |
| --- | --- | --- |
| **Manual con MSI** (lo que hay hoy) | Bajo | ✔ Implementado. El MSI con `MajorUpgrade` hace todo el trabajo limpio. |
| **Comprobación manual** desde la app (botón "Buscar actualizaciones") | Medio | ✔ Implementado. JSON en HTTPS. |
| **Comprobación automática al arranque** | Medio | Posible pero apagada por defecto (rompería el principio "local-first sin internet obligatorio"). |
| **Auto-descarga + auto-install silencioso** (estilo Chrome/Edge) | Alto | Descartado por ahora. Requiere certificado de firma, servidor con disponibilidad y diseño de fallbacks. |
| **Velopack / Squirrel / Sparkle** | Alto | Descartado por ahora; sustituye el modelo MSI. Si en el futuro se quiere auto-update real, es el siguiente paso. |
| **MSIX con Store / sideload** | Muy alto | Descartado. Firma obligatoria, AppContainer entra en conflicto con la ruta personalizada de WebView2. |

---

## 6. Casos de borde

### El usuario tiene el portable y luego instala el MSI

Conviven sin pisarse. El portable lee/escribe en `<exeDir>\Data`; el MSI lee/escribe en `%AppData%\MarkLocal` y `%LocalAppData%\MarkLocal`. La configuración no se comparte.

Si se quiere migrar la config del portable al MSI: copiar manualmente `<portable>\Data\config\settings.json` a `%AppData%\MarkLocal\settings.json`.

### El usuario tiene `install.ps1` (per-user, vía robocopy) y ahora le llega el MSI

Son dos mecanismos distintos. El instalador `install.ps1` no aparece en la base de datos de MSI, por lo que **el MSI no lo desinstalará automáticamente**. Hay que desinstalar primero con el `uninstall.ps1` o desde *Configuración → Aplicaciones → MarkLocal*.

Cuando publiquemos oficialmente, conviene anunciar el cambio y dejar el `install.ps1` solo como herramienta de desarrolladores.

### Downgrade (instalar v0.1.0 sobre v0.2.0)

`MajorUpgrade` lo bloquea con un MsgBox: "Ya tienes instalada una versión más reciente de MarkLocal." El usuario tendría que desinstalar a mano si quisiera bajar.

### Migración de configuración entre versiones

Mientras `AppSettings` mantenga retrocompatibilidad (campos opcionales, valores por defecto razonables al deserializar), no hay problema. Si en algún momento hay cambio incompatible:

1. Cambiar `MAYOR` en la versión.
2. Añadir migración en `SettingsService.Load()` que detecte el formato antiguo y lo transforme.
3. Documentar en el `releaseNotesText` del feed.

### Borradores y plantillas

Viven en `%LocalAppData%\MarkLocal\drafts` y `%AppData%\MarkLocal\templates`. **El MSI no los toca al desinstalar ni al actualizar.** Eso preserva el trabajo del usuario entre versiones.

---

## 7. Checklist para publicar v0.x.y

- [ ] Cambios commiteados y tests verdes.
- [ ] `csproj.Version` actualizado.
- [ ] `build-portable.ps1` ejecutado → ZIP en `dist\`.
- [ ] `build-msi.ps1` ejecutado → MSI en `dist\`.
- [ ] MSI probado en máquina con versión anterior (upgrade limpio: 1 sola entrada en *Apps instaladas*).
- [ ] MSI probado en máquina sin versión anterior (instalación fresca).
- [ ] Configuración del usuario preservada tras el upgrade.
- [ ] Subir artefactos al servidor de releases.
- [ ] Actualizar `updates.json` del feed con la nueva versión.
- [ ] Etiquetar el commit (`git tag vX.Y.Z`) y empujar.
- [ ] Anuncio (releases page / blog / lo que toque).

---

*Documento operativo creado por Alfonso Sanz López (Kairis) con apoyo de Claude Opus 4.7.*
