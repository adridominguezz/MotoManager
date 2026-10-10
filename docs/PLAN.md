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

### Sesión 1.2 — Creación de partida, controlador de juego y guardado ✅
- `WorldFactory` (snapshot 2026 → `WorldState`: motos por equipo, contratos, relleno de parrillas
  con pilotos generados, cantera inicial, personal, patrocinadores, conocimiento de scouting).
- `GameController`: próximo evento entre ambos calendarios, `BeginEvent` / `CompleteEvent`
  (economía, bonus, lesiones, desarrollo por días, BoP, scouting, noticias), fin de temporada.
- `SaveSystem` (JSON, versionado).
- **Hecho cuando** `SimRunner season` simula 2026 completo de las 6 categorías en modo headless.
- Resultado: `dotnet run -c Release --project Tests/SimRunner -- season <semilla> <años>`.
  Una temporada (34 eventos, 6 campeonatos) tarda ~22 s en escritorio; tres seguidas sin errores.
  Calendarios 2026 de GP (22 rondas, Rookies en 8) y SBK (12 rondas) adelantados de la sesión 2.3.
  Mientras no haya datos reales, `WorldFactory` genera fabricantes, equipos y pilotos de relleno.
  Tests: creación de partida, orden del calendario combinado, formato WSBK de 3 carreras,
  fin de semana del jugador carrera a carrera, determinismo y continuidad tras guardar/cargar.
- Pendiente para la fase 3: rendimiento en móvil (objetivo < 1 s por evento en segundo plano) y
  equilibrio (algún campeón con 17 victorias de 22 con pilotos generados).

## Fase 2 — Datos reales 2026 (2–3 sesiones)

### Sesión 2.1 — Fabricantes, motos y equipos ✅
- `manufacturers/`, `motorcycles/` (componentes por clase; base de producción en SBK/SSP), `teams/`
  (11 equipos MotoGP, Moto2, Moto3, Rookies, WorldSBK, WorldSSP) con estado factory/satélite.
- Resultado: 15 fabricantes, 25 motos base, 66 equipos (MotoGP 11, Moto2 14, Moto3 13, Rookies 1,
  WorldSBK 14 con equipos de 1 piloto, WorldSSP 14). Las motos de producción (SBK/SSP) incluyen la
  "base de serie" de cada modelo. Valoraciones aproximadas, revisables en los JSON.
- Nuevo modo `SimRunner bikes <categoría> [carreras] [vueltas]`: pilotos idénticos para medir solo la moto.
- Corrección del motor de carrera: la diferencia de ritmo (vuelta de referencia por piloto/moto)
  ahora influye en intentar y lograr adelantamientos; antes la moto rápida se quedaba en el tren.

### Sesión 2.2 — Pilotos y contratos ✅
- `riders/` con pilotos reales (MotoGP completo; Moto2/Moto3/WSBK/WSSP lo más completo posible),
  fecha de nacimiento, nacionalidad, atributos estimados, estilos, historial resumido.
- `contracts/` separados de los pilotos. Campo `dataConfidence` para marcar lo que hay que verificar.
- Resultado: 76 pilotos reales (MotoGP 22 completo, WorldSBK 18, Moto2 18, Moto3 12, WorldSSP 6;
  Rookies y el resto de plazas, generados) y 76 contratos. Fuente editable: `Tools/build_riders.py`
  (perfil = nivel + puntos fuertes; si se regenera, sobrescribe los JSON de `riders/` y `contracts/`).
- Equilibrio: forma del fin de semana + afinidad piloto/circuito + moral. Temporada de prueba:
  M. Márquez campeón con 13 victorias de 22 (antes ganaba las 22).

### Sesión 2.3 — Calendarios, nombres, patrocinadores, personal y plantillas de desarrollo ✅
- `calendar/` 2026 GP (22 rondas, Rookies en su subconjunto) y SBK; `names/` por nacionalidad con
  pesos por pirámide; `sponsors/`; `staff/`; `development/` (plantillas por clase de moto);
  `simulation/tuning.json`.
- **Hecho cuando** `GameDatabase.Validate()` devuelve 0 errores.
- Resultado: 22 nacionalidades con nombres y peso de cantera por pirámide (España/Italia en GP,
  Reino Unido/Italia en SBK...), 33 patrocinadores reales con importes y objetivos inventados
  (los patrocinios principales de 2026 se asignan a su equipo), 14 personas reales del paddock
  (resto del personal generado), 75 plantillas de desarrollo (incremental / nueva especificación /
  experimental, con compensaciones) por tipo de moto, y `simulation/tuning.json` editable.
- Ajustes: los pilotos generados empiezan por debajo del nivel de su parrilla y desde las canteras
  no se salta a una categoría reina sin nivel suficiente.

> Nota: los datos reales se basan en conocimiento hasta mediados de 2026. Conviene que revises
> las alineaciones de Moto2/Moto3/WSSP; todo lo dudoso irá marcado con `dataConfidence`.

## Fase 3 — Tests y equilibrio (1–2 sesiones) ✅

- Proyecto xUnit `Tests/Core.Tests`: determinismo (misma semilla + comandos = mismo resultado),
  replays, cierre de geometría de todos los circuitos, tiempos de vuelta vs referencia, tasas de
  caídas/averías por carrera, desgaste de neumáticos, combustible, clima gradual, sin DRS,
  reglas (cambio de moto solo si `allowsBikeSwap`), puntos, mercado y retiradas en 20 temporadas.
- Ajuste de `tuning.json` con `SimRunner` (estadísticas de muchas carreras).
- **Hecho cuando** `dotnet test` en verde y 20 temporadas simuladas sin errores ni degeneraciones
  (parrillas llenas, distribución de talento estable, varios fabricantes ganadores).
- Resultado (sesión 3.1): 19 tests en verde (determinismo, replay desde el log de órdenes,
  independencia del paso de tiempo, cierre y calibración de los 27 circuitos, tasas de caídas
  ~3.7/carrera y averías, desgaste y combustible, clima gradual y previsiones, cambio de moto solo
  si las reglas lo permiten, flag-to-flag con lluvia, salidas en falso penalizadas, tablas de puntos).
- Rendimiento: entrenamientos/clasificación derivados de una vuelta limpia simulada por piloto;
  frenada corregida para que la vuelta no dependa del paso de tiempo (0,05 s visual = 0,2 s fondo).
  Coste estable ~0,45 s por evento en escritorio (antes ~0,72 s). En el juego, las demás categorías
  se simularán en un hilo de fondo mientras el jugador ve su carrera (Fase 5).
- Equilibrio en 10 temporadas reales: declive por edad más marcado, cantera para jóvenes en
  categorías de formación, el presupuesto del grupo no infla los programas inferiores y límite de
  desarrollo invernal en categorías con tope. Resultado: relevo generacional (Márquez → Acosta y
  nuevos talentos), varios fabricantes campeones en cada categoría, niveles y edades estables.

## Fase 4 — Proyecto Unity base (1 sesión) ✅

- `Packages/manifest.json` (Unity 2022.3 LTS, uGUI, Newtonsoft oficial), `ProjectSettings`
  mínimos, `.asmdef` (`MotoManager.Core` sin referencias a motor, `MotoManager.Unity`,
  `MotoManager.Editor`), Built-in RP (URP solo si aporta).
- `ResourcesDataSource`, `GameBootstrap`, escena generada por script de editor, ajustes Android
  (IL2CPP, ARM64, 60/30 FPS objetivo).
- Proyecto de comprobación de compilación contra ensamblados de referencia de Unity.
- Resultado: `Packages/manifest.json` (uGUI + Newtonsoft oficial), Unity 2022.3 LTS, asmdefs
  (`MotoManager.Core` sin motor, `MotoManager.App`, `MotoManager.Editor`), `link.xml` y tipos AOT para
  IL2CPP. Arranque sin escena cableada (`GameBootstrap`), datos desde `Resources` parseados en un hilo,
  `GameSession` con trabajo pesado en segundo plano, guardado atómico con autoguardado al pausar,
  60/30 FPS adaptativo, kit de UI por código y una pantalla de prueba (nueva carrera deportiva,
  continuar, simular evento/temporada, clasificación, noticias). Editor: configuración Android
  automática, escena de arranque y builds APK/AAB (menú y línea de comandos).
- **Pendiente de verificar en tu máquina**: primera apertura en Unity y build real en un dispositivo
  (aquí solo se ha comprobado la compilación contra ensamblados de referencia).

## Fase 5 — Carrera visual (2–3 sesiones) ★ requisito principal

### Sesión 5.1 — Render del circuito y motos ✅
- Malla del circuito desde `TrackGeometry` (asfalto, pianos, grava, pit lane), cámara
  cenital/isométrica con zoom y seguimiento, motos low-poly con colores de equipo, inclinación,
  frenada y caídas animadas; interpolación entre ticks.
- Resultado: shader propio barato (color por vértice), circuito generado desde la geometría del núcleo
  (asfalto, pianos, grava, meta, parrilla, pit lane, garajes, tribunas), motos low-poly con colores del
  equipo (inclinación, cabeceo, caídas), cámara Seguir/Circuito/Libre con gestos táctiles, vista de
  carrera a velocidad 0/1/2/4/8x con paso fijo (determinista) e interpolación, HUD básico (torre de
  tiempos, vuelta, clima, banderas, eventos, etiquetas, resultados) y flujo completo del evento
  (entrenos y clasificación en segundo plano → carreras visibles → resto del evento en segundo plano).
- Vista previa fuera de Unity: `SimRunner preview|circuits` genera SVG del mismo dibujo
  (`docs/race_preview_mugello.png`, `docs/circuits_current.png`).
- **Problema detectado**: los trazados escritos a mano solo se parecen vagamente a los reales y 5 se
  cruzan sobre sí mismos (Jerez, Sepang, Sachsenring, Phillip Island, Silverstone). Solución prevista
  (sesión 5.1b): derivar los segmentos de la simulación a partir de coordenadas reales del trazado
  (OpenStreetMap). Requiere permitir `overpass-api.de` en la red del entorno o aportar las coordenadas.

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
