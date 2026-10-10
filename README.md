# Motorcycle Motorsport Manager

Juego de gestión de motociclismo para Android (Unity). El jugador dirige un equipo — pilotos, motos,
desarrollo, estrategia, economía — en un universo persistente que arranca con el estado real de 2026 y
evoluciona durante décadas: MotoGP, Moto2, Moto3, Red Bull Rookies Cup, WorldSBK y WorldSSP.

El plan por fases está en [`docs/PLAN.md`](docs/PLAN.md).

## Estructura

```
Assets/MotoManager/
  Scripts/Core/      Núcleo de simulación en C# puro (sin Unity): carrera, fin de semana, temporada,
                     carrera deportiva, economía. Determinista y probado fuera de Unity.
  Scripts/Unity/     Capa Unity (asmdef MotoManager.App): arranque, carga de datos, guardado, interfaz.
  Scripts/Editor/    Configuración del proyecto para Android y comandos de build.
  Resources/Data/    Datos del juego en JSON (categorías, reglas, circuitos, calendarios, fabricantes,
                     motos, equipos, pilotos, contratos, personal, patrocinadores, nombres, desarrollo,
                     ajustes de simulación). Se pueden editar sin tocar código.
Tests/
  CoreLib/           Compila el núcleo como netstandard2.1 / C# 9 (las mismas restricciones que Unity).
  Core.Tests/        Tests xUnit del núcleo.
  SimRunner/         Herramienta de consola: carreras, temporadas, calibración y perfiles.
  UnityCheck/        Comprueba que la capa Unity compila contra ensamblados de referencia de Unity.
Tools/               Scripts auxiliares (generación de pilotos, comprobación de trazados).
```

## Abrir en Unity

1. Instala **Unity 2022.3 LTS** con el módulo **Android Build Support** (SDK, NDK y OpenJDK).
2. Abre la carpeta del repositorio desde Unity Hub. La primera vez Unity importará los paquetes
   (uGUI y Newtonsoft JSON) y generará los ficheros `.meta`; conviene hacer commit de ellos después.
3. Al terminar la importación se ejecuta automáticamente **MotoManager → Configure project**:
   crea la escena `Assets/MotoManager/Scenes/Main.unity` y configura Android (IL2CPP, ARM64,
   horizontal). Se puede volver a lanzar desde ese menú.
4. Abre la escena `Main` y pulsa Play. Todo lo demás se crea por código al arrancar.

## Generar el APK

- Menú **MotoManager → Build Android APK** (o *App Bundle* para Google Play). El resultado queda en `Builds/Android/`.
- Línea de comandos:
  `Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod MotoManager.EditorTools.BuildScript.BuildAndroid`

## Tests y herramientas (sin Unity)

Requiere .NET 8 SDK.

```bash
dotnet test Tests/Core.Tests                                       # tests del núcleo
dotnet build Tests/UnityCheck                                      # la capa Unity compila
dotnet run -c Release --project Tests/SimRunner -- race motogp_2026 mugello 3     # una carrera
dotnet run -c Release --project Tests/SimRunner -- season 2026 10                 # 10 temporadas
dotnet run -c Release --project Tests/SimRunner -- bikes worldsbk 12              # equilibrio de motos
dotnet run -c Release --project Tests/SimRunner -- laps motogp_2026               # tiempos por circuito
```

## Datos reales

El snapshot inicial usa pilotos, equipos, fabricantes, circuitos y calendarios reales de 2026. Los
atributos de pilotos y motos son estimaciones para el juego. Cada fichero o registro lleva un campo
`dataConfidence` que indica qué está verificado y qué es aproximado.
