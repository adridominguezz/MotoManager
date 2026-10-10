# Motorcycle Motorsport Manager — Plan por fases

Plan de desarrollo dividido en sesiones cortas. Cada sesión tiene un objetivo cerrado,
entregables concretos y un criterio de "hecho" verificable. Al final de cada sesión se
hace commit + push en `claude/motorcycle-motorsport-manager-a2iz6p`.

Convenciones que se mantienen en todas las fases:

- **Núcleo puro C#** (`Assets/MotoManager/Scripts/Core`, sin dependencias de Unity, C# 9 /
  netstandard2.1) → se compila y se prueba fuera de Unity con `dotnet` (`Tests/`).
- **Datos fuera del código** (`Assets/MotoManager/Resources/Data/<carpeta>/*.json`).
- **Reglas por categoría** (`CategoryRules`): el motor nunca hace `if (category == MotoGP)`.
- **Determinismo**: toda aleatoriedad sale de `DeterministicRandom` (semilla + comandos).
- **Sin DRS** ni equivalentes.

---

## Estado actual (lo ya hecho)

| Área | Estado |
|---|---|
| Utilidades (`DeterministicRandom`, `MathX`) | ✅ |
| Modelo de datos: categorías, reglas, clases de moto, circuitos, calendario, patrocinadores, nombres, plantillas de desarrollo | ✅ |
| Entidades del mundo: `Rider` (18 atributos, estilos, personalidad, historial), `Team`, `Manufacturer`, `Motorcycle`, `StaffMember`, `WorldState` | ✅ |
| Carga de datos JSON (`DataLoader`, `IDataSource`, `FileDataSource`) + validación de referencias | ✅ |
| Circuito segmentado (DSL de rectas/curvas) + geometría 2D cerrada para el render | ✅ |
| Simulación de carrera (micro): física por fases, neumáticos delantero/trasero (temperatura, desgaste, compuesto), clima gradual, rebufo, adelantamientos/defensa, errores, 6 tipos de caída + remontar, fiabilidad, combustible, pit lane, cambio de moto/neumáticos según reglas, penalizaciones (long lap, ride through, tiempo, posiciones), banderas amarilla/blanca, IA de carrera, comandos del jugador registrados para replay | ✅ |
| Clasificación/práctica (time attack, Q1/Q2, tráfico, rebufo, evolución de pista) | ✅ |
| Fin de semana configurable por reglas (FP, Q, Sprint, Superpole Race, Race 1/2, parrilla de Race 2 WSBK) | ✅ |
| Puntos y clasificaciones (pilotos, equipos, fabricantes) + estadísticas de carrera | ✅ |
| Reglas 2026 de MotoGP, Moto2, Moto3, Rookies Cup, WorldSBK, WorldSSP | ✅ |
| 27 circuitos calibrados (tiempos de vuelta ±2 % del ritmo real) | ✅ |
| Generador de pilotos, evolución por edad, retirada probabilística, scouting | ✅ (sin probar) |
| Personal, patrocinadores, finanzas, desarrollo (proyectos con riesgo y trade-offs, concesiones, BoP) | ✅ (sin probar) |
| Herramientas: `Tests/SimRunner` (laps / race / calibrate), `Tools/check_layouts.py`, `Tools/normalize_turning.py` | ✅ |

---

## Fase 1 — Cerrar el núcleo "macro" (2 sesiones)

### Sesión 1.1 — Mercado de pilotos y fin de temporada ✅
- `TransferMarket`: renovaciones, vacantes en cascada (MotoGP → Moto2 → Moto3 → Rookies → nuevo
  piloto; WorldSBK → WorldSSP → Junior), movimientos entre pirámides por utilidad (rendimiento,
  edad, potencial, reputación, salario, preferencia GP/SBK, fabricante, límites de edad).
- `SeasonEndProcessor`: campeones e historial, patrocinadores, desarrollo de pilotos, retiradas,
  desarrollo invernal de fabricantes, mercado, cantera anual, nueva temporada (calendario
  re-fechado si no hay datos del año siguiente), archivo compacto de campeonatos.
- **Hecho cuando** compila y un test simula un fin de temporada sin asientos vacíos ni pilotos duplicados.
- Resultado: `Tests/Core.Tests` (xUnit) con un mundo sintético. 20 temporadas seguidas mantienen
  parrillas llenas, contratos y edades válidos, dorsales únicos y la jerarquía de niveles
  (MotoGP > Moto2 > Moto3, WorldSBK > WorldSSP). Unos 10 cambios GP↔SBK por temporada;
  las canteras alimentan las parrillas sin necesidad de generar pilotos de relleno.
  Fin de temporada determinista (mismo resultado con la misma semilla).

### Sesión 1.2 — Creación de partida, controlador de juego y guardado
- `WorldFactory` (snapshot 2026 → `WorldState`: motos por equipo, contratos, relleno de parrillas
  con pilotos generados, cantera inicial, personal, patrocinadores, conocimiento de scouting).
- `GameController`: próximo evento entre ambos calendarios, `BeginEvent` / `CompleteEvent`
  (economía, bonus, lesiones, desarrollo por días, BoP, scouting, noticias), fin de temporada.
- `SaveSystem` (JSON, versionado).
- **Hecho cuando** `SimRunner season` simula 2026 completo de las 6 categorías en modo headless.

## Fase 2 — Datos reales 2026 (2–3 sesiones)

### Sesión 2.1 — Fabricantes, motos y equipos
- `manufacturers/`, `motorcycles/` (componentes por clase; base de producción en SBK/SSP), `teams/`
  (11 equipos MotoGP, Moto2, Moto3, Rookies, WorldSBK, WorldSSP) con estado factory/satélite.

### Sesión 2.2 — Pilotos y contratos
- `riders/` con pilotos reales (MotoGP completo; Moto2/Moto3/WSBK/WSSP lo más completo posible),
  fecha de nacimiento, nacionalidad, atributos estimados, estilos, historial resumido.
- `contracts/` separados de los pilotos. Campo `dataConfidence` para marcar lo que hay que verificar.

### Sesión 2.3 — Calendarios, nombres, patrocinadores, personal y plantillas de desarrollo
- `calendar/` 2026 GP (22 rondas, Rookies en su subconjunto) y SBK; `names/` por nacionalidad con
  pesos por pirámide; `sponsors/`; `staff/`; `development/` (plantillas por clase de moto);
  `simulation/tuning.json`.
- **Hecho cuando** `GameDatabase.Validate()` devuelve 0 errores.

> Nota: los datos reales se basan en conocimiento hasta mediados de 2026. Conviene que revises
> las alineaciones de Moto2/Moto3/WSSP; todo lo dudoso irá marcado con `dataConfidence`.

## Fase 3 — Tests y equilibrio (1–2 sesiones)

- Proyecto xUnit `Tests/Core.Tests`: determinismo (misma semilla + comandos = mismo resultado),
  replays, cierre de geometría de todos los circuitos, tiempos de vuelta vs referencia, tasas de
  caídas/averías por carrera, desgaste de neumáticos, combustible, clima gradual, sin DRS,
  reglas (cambio de moto solo si `allowsBikeSwap`), puntos, mercado y retiradas en 20 temporadas.
- Ajuste de `tuning.json` con `SimRunner` (estadísticas de muchas carreras).
- **Hecho cuando** `dotnet test` en verde y 20 temporadas simuladas sin errores ni degeneraciones
  (parrillas llenas, distribución de talento estable, varios fabricantes ganadores).

## Fase 4 — Proyecto Unity base (1 sesión)

- `Packages/manifest.json` (Unity 2022.3 LTS, uGUI, Newtonsoft oficial), `ProjectSettings`
  mínimos, `.asmdef` (`MotoManager.Core` sin referencias a motor, `MotoManager.Unity`,
  `MotoManager.Editor`), Built-in RP (URP solo si aporta).
- `ResourcesDataSource`, `GameBootstrap`, escena generada por script de editor, ajustes Android
  (IL2CPP, ARM64, 60/30 FPS objetivo).
- Proyecto de comprobación de compilación contra ensamblados de referencia de Unity.

## Fase 5 — Carrera visual (2–3 sesiones) ★ requisito principal

### Sesión 5.1 — Render del circuito y motos
- Malla del circuito desde `TrackGeometry` (asfalto, pianos, grava, pit lane), cámara
  cenital/isométrica con zoom y seguimiento, motos low-poly con colores de equipo, inclinación,
  frenada y caídas animadas; interpolación entre ticks.

### Sesión 5.2 — HUD y controles
- Torre de tiempos (posición, gaps, vuelta, neumáticos, estado), feed de eventos, clima/radar con
  previsión probabilística, banderas, velocidad Pausa/1x/2x/4x/8x (simulación determinista
  independiente del render), órdenes al piloto (Push/Balanced/Conserve, Attack/Balanced/Defend,
  entrar a boxes con compuesto).

### Sesión 5.3 — Replays y rendimiento
- Replay por semilla + log de comandos, cámara libre/TV, perfilado en Android de gama baja.

## Fase 6 — Interfaz de gestión (2–3 sesiones)

- Hub (próximo evento, presupuesto, noticias), Equipo (pilotos con atributos percibidos por
  scouting, contratos, personal), Moto (componentes, proyectos de desarrollo), Instalaciones,
  Patrocinadores/Finanzas, Clasificaciones e historial, Calendario, Mercado (ofertas a pilotos),
  Fin de semana (elección de neumáticos, sesiones), Nueva partida (elegir equipo y categoría).

## Fase 7 — Pulido y build Android (1–2 sesiones)

- Guardado/carga en `persistentDataPath`, autosave, ajustes, textos (ES/EN), build APK/AAB,
  pruebas en dispositivo, documentación (`README`, `docs/ARCHITECTURE.md`).

## Pendiente de especificación

El documento original se corta en el apartado **49. Patrocinadores** ("Cada patrocinador puede
tener: …"). Lo implementado usa: pago por temporada, nivel/tier, reputación mínima, pirámides,
nacionalidades preferidas, objetivos por carrera y temporada, duración y satisfacción.
Cuando envíes el resto (secciones 49+), se incorporará en la fase que corresponda.
