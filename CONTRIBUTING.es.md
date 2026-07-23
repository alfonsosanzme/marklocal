# Contribuir a MarkLocal

**[English](CONTRIBUTING.md)** | Español

¡Gracias por el interés! MarkLocal es un proyecto pequeño y pragmático. Reglas de juego:

## Principios del proyecto

Antes de proponer algo grande, ten en cuenta los principios que guían las decisiones (están en [docs/especificacion.md](docs/especificacion.md)):

1. **Local-first** — todo funciona sin internet. Nada de cuentas, nube ni telemetría.
2. **Ligero** — pocas dependencias. Se rechazan librerías pesadas si hay alternativa razonable.
3. **Predecible** — lo que se guarda es Markdown plano, sin formato propietario.
4. **En español** — la UI del proyecto es en castellano (la i18n está en el backlog).

## Cómo contribuir

- **Bugs**: abre un issue con pasos de reproducción, versión (Ayuda → Acerca de) y si usas MSI o portable.
- **Features**: abre un issue antes de picar código; así evitamos trabajo que no encaje con los principios.
- **PRs**: rama desde `main`, descripción clara de qué y por qué. Los tests deben pasar:

```powershell
dotnet test .\MarkLocal.Tests\MarkLocal.Tests.csproj
```

## Entorno de desarrollo

- SDK de .NET 8, Windows 10/11.
- `dotnet run --project .\MarkLocal\MarkLocal.csproj` para lanzar.
- Los scripts de empaquetado están en `MarkLocal\scripts\` (portable y MSI).

## Estilo

- C# idiomático .NET 8, nullable habilitado.
- El código sigue el estilo del archivo en el que estés — comentarios escasos y solo donde el código no puede explicarse solo.
- Mensajes de UI en castellano con tildes correctas.
