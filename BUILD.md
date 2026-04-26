# 🎛️ REAPER Plugin Manager — Guía de Construcción y Despliegue

## 📋 Requisitos previos

| Herramienta | Versión mínima | Descarga |
|-------------|----------------|----------|
| .NET SDK | 8.0+ | https://dot.net/download |
| Visual Studio | 2022 (Community+) | https://visualstudio.microsoft.com |
| Windows | 10/11 x64 | — |

> **Nota:** La app solo compila y corre en Windows por su dependencia de WPF, WinVerifyTrust y Windows Defender.

---

## 🚀 Compilación rápida (un solo comando)

```powershell
# Clonar / descomprimir el proyecto
cd ReaperPluginManager

# Restaurar dependencias
dotnet restore ReaperPluginManager.csproj

# Compilar en Release
dotnet build ReaperPluginManager.csproj -c Release

# Publicar como .exe portable único
dotnet publish ReaperPluginManager.csproj -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true `
    -o ./publish
```

El ejecutable final estará en: `./publish/ReaperPluginManager.exe`

---

## 🔧 Compilar el VSTHost (sandbox)

```powershell
cd VSTHost/PluginTestHost
dotnet publish PluginTestHost.csproj -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true -o ../../publish/VSTHost/
```

> El SandboxService busca el host en `./VSTHost/PluginTestHost.exe` relativo al ejecutable principal.

---

## 📦 Estructura del directorio de publicación

```
publish/
├── ReaperPluginManager.exe      ← Ejecutable principal (portable)
└── VSTHost/
    └── PluginTestHost.exe       ← Host VST para sandbox
```

---

## 🗂️ Datos en tiempo de ejecución

La aplicación crea automáticamente estos directorios:

```
%LOCALAPPDATA%\ReaperPluginManager\
├── plugins.db          ← Base de datos LiteDB
├── Downloads\          ← Archivos temporales de descarga
├── Quarantine\         ← Plugins bloqueados cuarentenados
└── Logs\
    └── app-YYYYMMDD.log ← Logs rotativos diarios (14 días)
```

---

## 🛡️ Permisos requeridos

El manifiesto `app.manifest` usa `highestAvailable`:
- **Sin admin**: instala solo en `%APPDATA%` y directorios de usuario
- **Con admin**: puede instalar en `C:\Program Files\VSTPlugins` y `Common Files\VST3`

Para el escaneo con Windows Defender (`MpCmdRun.exe`) no se requieren permisos especiales.

---

## ⚙️ Dependencias NuGet (auto-restauradas)

```
LiteDB                  5.0.21   → Base de datos embebida
Newtonsoft.Json         13.0.3   → Serialización JSON
CommunityToolkit.Mvvm   8.3.2    → MVVM / Source generators
MaterialDesignThemes    5.1.0    → UI Material Design
MaterialDesignColors    3.1.0    → Paleta de colores
Serilog                 4.1.0    → Logging estructurado
Serilog.Sinks.File      6.0.0    → Logs a archivo
SharpCompress           0.37.2   → Extracción ZIP/7Z/RAR
Microsoft.Extensions.*  8.0.x    → DI + HttpClientFactory
```

---

## 🧪 Pruebas unitarias (opcional)

```powershell
# Si se incluye proyecto de tests:
dotnet test ReaperPluginManager.Tests/
```

---

## 🐛 Solución de problemas comunes

| Error | Causa | Solución |
|-------|-------|----------|
| `CS0246: MaterialDesignThemes` no encontrado | NuGet no restaurado | `dotnet restore` |
| `Win32 error 126` al cargar plugin | DLL de 32-bit en sistema 64-bit | Plugin incompatible, usar versión x64 |
| `MpCmdRun.exe` no encontrado | Defender desactivado o path diferente | Actualiza Windows Defender |
| `Access denied` al instalar en Program Files | Falta de permisos UAC | Ejecutar como Administrador |
| `LiteDB: file locked` | Dos instancias abiertas | Cerrar instancia anterior |

---

## 📌 Limitaciones conocidas y mejoras futuras

### Limitaciones actuales
- **VSTHost**: El sandbox actual carga la DLL con `LoadLibraryEx` pero **no** ejecuta el procesador de audio real (requeriría implementar el protocolo VST2/VST3 completo con ASIO/WASAPI). Para testing completo se necesitaría integrar con un host real como [JUCE](https://juce.com).
- **JSFX sandbox**: Solo análisis estático básico. REAPER tiene su propio intérprete de JSFX.
- **Sin base de datos online**: El catálogo online y los ratings comunitarios requieren un backend propio (endpoints en `UpdateService.cs` apuntan a un servidor placeholder).
- **Instaladores NSIS/InnoSetup**: Se ejecutan en modo silencioso pero la detección de archivos instalados es estimada.

### Mejoras futuras recomendadas
1. **Base de datos de plugins online**: API REST con catálogo curado (tipo VST4Free o Plugin Alliance)
2. **Ratings comunitarios**: Backend con sistema de votes y reviews
3. **ETW para sandbox**: Usar Event Tracing for Windows para monitoreo de red/archivos más preciso
4. **JUCE VSTHost**: Compilar un host real con JUCE para prueba auditiva del plugin
5. **Firma de código del ejecutable**: Firmar `ReaperPluginManager.exe` con certificado EV para evitar alertas de SmartScreen
6. **Auto-actualización del manager**: Similar a Squirrel.Windows para actualizaciones del gestor en sí
7. **Soporte AU/LV2**: Extender para macOS (AU) y Linux (LV2)
8. **Integración con REAPER IPC**: REAPER expone una API Named Pipe para comunicación en tiempo real

---

## 📄 Licencia

MIT License — Copyright © 2025 ReaperPluginManager Contributors
