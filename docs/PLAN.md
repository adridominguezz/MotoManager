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

### Sesión 5.1b — Trazados reales desde coordenadas (preparada, faltan los datos)
- ✅ Hecho: `ShapeConverter` convierte una línea central real (metros o lat/lon) en segmentos de
  simulación (curvatura suavizada → rectas y curvas con radio y ángulo medidos), respetando sentido
  de giro y línea de meta. Campos nuevos en circuitos: `shape`, `shapeIsLatLon`, `shapeStart`,
  `clockwise`, `radiusScale`. La calibración ajusta `radiusScale` sin tocar la geometría medida.
  Test de ida y vuelta con 4 circuitos: mismas curvas, vuelta a ±2,5 % y el dibujo cierra solo.
- ⏳ Pendiente: descargar las líneas centrales de los 27 circuitos (OpenStreetMap / Overpass),
  sustituir los `layout` aproximados y recalibrar. Bloqueado por la red del entorno
  (`overpass-api.de` denegado); alternativa: aportar ficheros GPX/KML/GeoJSON de los trazados.

### Sesión 5.2 — HUD y controles ✅
- Torre de tiempos (posición, gaps, vuelta, neumáticos, estado), feed de eventos, clima/radar con
  previsión probabilística, banderas, velocidad Pausa/1x/2x/4x/8x (simulación determinista
  independiente del render), órdenes al piloto (Push/Balanced/Conserve, Attack/Balanced/Defend,
  entrar a boxes con compuesto).
- Resultado: tarjetas de muro para cada piloto del jugador (posición, distancias, desgaste y
  temperatura de cada neumático, combustible y proyección, ritmo Ahorrar/Normal/Atacar, actitud
  Defender/Normal/Atacar, "a boxes esta vuelta" con compuesto, modo automático); ingeniero de carrera
  en el núcleo (`RaceEngineer`, probado) con avisos de lluvia, pista secándose, neumáticos, combustible,
  ataques y penalizaciones; previsión de lluvia probabilística según el ingeniero de datos; diálogo
  previo con parrilla, previsión y elección de neumático delantero/trasero con recomendación.

### Sesión 5.3 — Replays y rendimiento ✅ (rendimiento en dispositivo pendiente)
- Replay por semilla + log de comandos, cámara libre/TV, perfilado en Android de gama baja.
- Resultado: `RaceReplay` (núcleo, probado) guarda la configuración inicial y el log de órdenes y
  re-simula la carrera idéntica, con saltos de ±1 vuelta; "Ver replay" en los resultados con velocidad
  y cámaras. El perfilado en un Android real queda para cuando se pruebe en dispositivo (Fase 7).

## Fase 6 — Interfaz de gestión (2–3 sesiones) ✅

- Hub (próximo evento, presupuesto, noticias), Equipo (pilotos con atributos percibidos por
  scouting, contratos, personal), Moto (componentes, proyectos de desarrollo), Instalaciones,
  Patrocinadores/Finanzas, Clasificaciones e historial, Calendario, Mercado (ofertas a pilotos),
  Fin de semana (elección de neumáticos, sesiones), Nueva partida (elegir equipo y categoría).
- Resultado: estructura con barra lateral, cabecera (equipo, presupuesto, fecha, próximo evento),
  avisos emergentes y trabajo pesado en segundo plano. Pantallas:
  - **Inicio**: próximo evento (jugar / simular / simular hasta el final), cierre de temporada con
    resumen, tus pilotos en el campeonato, noticias, guardar.
  - **Equipo**: pilotos con sus 18 atributos (rangos según scouting), estilos, historial, contrato,
    renovaciones; personal con contratar (candidatos por rol) y despedir (indemnización).
  - **Moto e I+D**: componentes y si el reglamento permite desarrollarlos, rendimiento derivado,
    proyectos en curso/terminados con resultados reales, iniciar proyectos (coste, días, riesgo,
    compensaciones) e instalaciones mejorables.
  - **Finanzas**: previsión de temporada, patrocinadores con objetivos y satisfacción, firmar nuevos.
  - **Mercado**: pilotos de cualquier categoría o cantera, scouting, ofertas para la temporada
    siguiente (aceptadas o rechazadas por el piloto con su salario pedido).
  - **Clasificaciones**, **Calendario** e **Historial** (palmarés y más victorias).
- Núcleo nuevo y probado: `ContractService` (ofertas, renovaciones, contratos futuros que el mercado
  aplica al cerrar la temporada), `StaffMarket`, `FinanceService.SeasonForecast`. 28 tests en verde.

## Fase 7 — Pulido y build Android (1–2 sesiones)

- Guardado/carga en `persistentDataPath`, autosave, ajustes, textos (ES/EN), build APK/AAB,
  pruebas en dispositivo, documentación (`README`, `docs/ARCHITECTURE.md`).

## Fase 8 — Requisitos 49–105 (pendiente de aprobación)

La parte final del documento original (secciones 49–105) está analizada en
[`docs/ANALISIS_49-105.md`](ANALISIS_49-105.md): estado de cada sección, contradicciones detectadas,
sesiones propuestas (8.1–8.8) y decisiones que necesitan aprobación antes de programar.

### Sesión 8.1 — Análisis de carrera ✅
- Estadísticas por piloto en el motor: tiempo/velocidad limpios por zona (rectas, curvas lentas,
  medias y rápidas), velocidad punta, mejores sectores, vueltas y posiciones por vuelta, adelantamientos
  hechos/sufridos/abortados, errores y salidas de pista.
- `RaceAnalysis` ("¿Por qué hemos rendido así?"): compara con la parrilla y lo traduce a segundos por
  vuelta, km/h, caída de ritmo, desgaste, adelantamientos, defensa, errores, salida, fiabilidad y
  sanciones; devuelve códigos para mostrarlos en cualquier idioma.
- `TrackProfile`: rasgos del circuito derivados de su geometría (velocidad punta, frenadas, técnico,
  rápido, rebufo, neumáticos, adelantamientos), sin tablas fijas; visible antes y después de la carrera.
- Pantalla post-carrera completa, columna de equipo en la torre, cámara de vista de sector y primer
  sistema de localización (`Loc`, español/inglés) usado por estas pantallas. 30 tests en verde.

### Sesión 8.2 — IA y personalidad de equipos ✅
- `TeamPersonality` en cada equipo: objetivo (título, victorias, podios, mitad alta, puntos, formar
  jóvenes), filosofía (talento joven, experiencia, fiel al fabricante, resultados inmediatos, largo plazo),
  nivel técnico, ritmo de desarrollo, apoyo del fabricante y tolerancia al riesgo. Se genera de forma
  determinista a partir de lo que es el equipo (oficial/satélite/privado, reputación, dinero, categoría) o
  se puede fijar en los JSON.
- Mercado: la filosofía cambia a quién se ficha (jóvenes con margen frente a veteranos con resultados,
  pilotos del mismo fabricante), cuánto se paga (techo salarial según riesgo) y la duración del contrato;
  las renovaciones dependen del objetivo cumplido.
- Desarrollo: las mejoras en temporada dependen del apoyo del fabricante, el nivel técnico y el ritmo de
  desarrollo; la distancia de la moto satélite a la oficial sale del apoyo del fabricante.
- Presupuesto: en invierno cada equipo de la IA invierte parte del excedente en su departamento técnico
  (más los de largo plazo); la capacidad que no se mantiene se pierde poco a poco.
- Carrera: el riesgo del equipo mueve el umbral de ataque, el empuje final, el momento de entrar con lluvia
  y la elección de neumático.
- Evaluación anual: objetivo cumplido o no; los que fallan asumen más riesgo y, tras varias temporadas,
  pueden cambiar de filosofía (con noticia). Las partidas antiguas reciben personalidades al cargarse.
- Pantalla de equipo: objetivo de la dirección y resultado de la temporada anterior (ES/EN). 34 tests en verde;
  10 temporadas estables (campeones de 5 fabricantes en MotoGP).

### Fase E — Economía, contratos y carrera del manager
Auditoría, decisiones y plan en [`docs/ANALISIS_ECONOMIA_CONTRATOS_MANAGER.md`](ANALISIS_ECONOMIA_CONTRATOS_MANAGER.md).
Orden: E1 → E2 → M1 → E3 → E4 → 8.4 → E5 → M2 → E6 → E7 → 8.6–8.8.

### Sesión E1 — Organización y dinero único ✅
- `Organisation`: dueña de los equipos y del dinero (caja, propietario: privado, fabricante, jugador u organizador del campeonato,
  disposición de los propietarios a cubrir pérdidas). Las estructuras con varios equipos (Intact, Ajo, Aspar, Tech3,
  MSi, Team Asia, Marc VDS, Barni, Pramac, Gresini) y las divisiones oficiales (Ducati Corse, HRC, Yamaha, Aprilia,
  KTM, BMW, MV Agusta) se definen en `organisations/organisations.json`, con agrupación aproximada. El resto de
  equipos tiene su propia organización.
- El equipo ya no tiene dinero: tiene `SeasonBudget`, una asignación de la temporada. Gastar por encima de ella
  aparece como exceso de gasto.
- `Accounts.Post` es el único punto que mueve dinero (IA y jugador). Totales por organización, equipo, temporada y
  concepto; detalle línea a línea para la organización del jugador; cada movimiento indica si afecta a la caja y si
  afecta a pérdidas y ganancias; claves para que un evento o una acción nunca se apliquen dos veces; resúmenes
  anuales permanentes y limpieza de los datos antiguos.
- El suelo de dinero oculto de la IA se sustituye por la aportación de los propietarios, anotada como movimiento.
  Una organización del jugador no la recibe.
- Las primas por victoria y podio de los contratos de pilotos ya se pagan (antes no se pagaban). Los contratos
  reales de 2026 reciben primas estándar estimadas.
- Los salarios que acepta la IA tienen tope según el presupuesto del equipo.
- Desaparece `PlayerCash`, que duplicaba el dinero. La pantalla de finanzas muestra la organización, la caja,
  el presupuesto asignado, lo gastado y los movimientos reales por concepto (ES/EN).
- 37 tests en verde.
- Hallazgo de equilibrio que ya existía antes de E1 (se trata en E2/E7): con la mayoría de semillas Ducati gana
  casi todos los títulos de MotoGP en 10 temporadas, y casi todos los equipos de la IA pierden entre 10 y 25 M
  por temporada, que cubren sus propietarios.

### Sesión E2a — Costes por categoría, previsión de caja y viabilidad ✅
- `EconomyRules` en datos, con valores **estimados** y marcados como tales:
  - Cómo se entra en la categoría: compra de plaza en MotoGP, Moto2 y Moto3; inscripción abierta en WorldSBK y
    WorldSSP; la Rookies Cup solo la gestiona el organizador.
  - Precio de plaza o inscripción, equipamiento inicial, compra de motos por piloto y licencias anuales.
  - Escalas de coste de instalaciones y de salarios del personal por categoría (antes un equipo de Moto3 pagaba
    como uno de MotoGP).
  - Reserva de seguridad y caídas esperadas.
- `FinanceService` reescrito alrededor de `CostProfile`: la misma lista de conceptos paga cada evento y alimenta la
  previsión, tanto para un equipo real como para uno planificado.
- `CashflowForecast`: proyección de la caja por fechas del calendario, separando ingresos confirmados y probables;
  consolidada por organización.
- `ViabilityService`: coste de entrada, coste operativo, ingresos confirmados y probables, reserva, caja mínima y
  veredicto Viable / Arriesgada / Inviable / No disponible, con motivos y cantidad que falta. En una expansión cuenta
  los equipos que la organización ya tiene.
- La pantalla de finanzas muestra la previsión de caja hasta final de temporada (ES/EN).
- Los totales contables se guardan de forma compacta (2 temporadas y resúmenes anuales). 40 tests en verde.
- Pendiente para E2b: los ingresos siguen muy por debajo de los costes (unos −1,2 a −2 M por equipo y temporada
  en las categorías inferiores), porque el mercado de patrocinadores es muy pequeño (33 para unos 70 equipos).

### Sesión E2b — Mercado de patrocinadores y objetivos comunes ✅
- `ConditionEvaluator`: un único evaluador de resultados (top N, victorias, podios, poles, vueltas rápidas,
  puntos, posición en el campeonato), usado por los patrocinadores y preparado para las cláusulas de E4 y los
  objetivos del manager en E5.
- `SponsorMarket`:
  - Los patrocinadores hacen ofertas tras los eventos y sobre todo en invierno. Las ofertas caducan y **no son
    dinero** hasta firmarse.
  - La IA decide con las mismas reglas que el jugador.
  - El jugador puede aceptar, rechazar o pedir un +10 %. El patrocinador puede aceptar, mantener la oferta o
    retirarla; la respuesta es determinista.
- Condiciones de los contratos de patrocinio:
  - Prima de firma, que se paga una sola vez.
  - Pagos por ronda.
  - Patrocinio condicionado a una categoría: no paga hasta que se cumple la condición.
  - Exclusividad por sector y número máximo de patrocinadores por equipo.
  - Recorte del pago si no se cumplen los objetivos.
  - Renovaciones según la satisfacción del patrocinador y el valor actual de mercado del equipo.
- Preferencia de categoría con pesos (decisión aprobada). Los objetivos se ajustan a lo que el equipo puede
  lograr: a un equipo modesto no se le exige ganar el título.
- Mercado completado con **marcas inventadas** (nombres de datos ficticios, marcadas `Generated` y «fictional»)
  y renovado cada invierno. Las marcas reales siguen viniendo de los datos.
- Calibración: la curva de salarios que piden los pilotos se suaviza; 10 temporadas sin inflación de ingresos.
  Media por equipo entre −0,1 y −0,8 M en las categorías inferiores (incluidos los oficiales); MotoGP −5 M,
  que cubren los fabricantes.
- Pantalla de patrocinio con los contratos, su estado y las ofertas con sus acciones (ES/EN). 46 tests en verde.

### Sesión E2c — Patrocinadores con varios equipos y ciclos de dominio ✅
- Un patrocinador puede respaldar a varios equipos a la vez, hasta su límite (`MaxTeams`; Red Bull, Monster,
  Liqui Moly, CFMoto y ELF con sus equipos reales), pero nunca dos veces al mismo equipo.
- Concesiones por niveles en MotoGP (A ≥85 %, B ≥60 %, C ≥35 %, D: multiplican el desarrollo y en D se descongela
  el motor), con los niveles iniciales de 2026 (Ducati A, Aprilia y KTM B, Honda C, Yamaha D). Se aplican también a
  los proyectos del jugador.
- Reacción de los fabricantes cada invierno: los que se quedan atrás pueden invertir, fichar ingenieros y subir el
  presupuesto de su equipo oficial; los dominantes pueden perder técnicos; todo tiende con el tiempo hacia la media.
  Limitado por la nueva **capacidad financiera** del grupo (Honda 100, Yamaha 90: los que más motos venden).
- Reglamento de MotoGP 2027 (850 cc, menos aerodinámica, sin dispositivos de altura): fichero `motogp_2027` con
  `RegulationReset` 0,5. Se pierde la mitad de la ventaja acumulada; la física se mantiene como en 2026.
- Mercado: en las categorías reinas los pilotos eligen entre los equipos que los quieren (aceptación diferida) según
  la moto (su nivel real), el equipo, el dinero y sus preferencias. El equipo más prestigioso ya no elige primero
  siempre. La utilidad de una plaza se calcula en un solo sitio (mercado de la IA y ofertas del jugador).
- Resultado en 3 semillas × 10 temporadas: campeones de MotoGP con Ducati, Honda, Aprilia, Yamaha y KTM, y
  rotación también en WorldSBK. Antes, Ducati ganaba 18–20 de cada 20 títulos. 47 tests en verde.

### Sesión M1 — Tienda oficial de merchandising ✅
- Catálogo en datos (`merchandise/products.json`, 16 productos con coste y precio estimados): camisetas, gorras,
  sudaderas, chaquetas, mochilas, llaveros, tazas, pósteres, miniaturas, réplica del mono; productos con la marca
  del fabricante (solo equipos oficiales, con un 15 % de royalty). Los productos de piloto, de victoria y de
  campeonato ya están definidos, pero se activan en M2.
- Demanda por producto y evento, con límites:
  - Audiencia de la categoría.
  - Tamaño de la afición, con rendimiento decreciente (como mucho el doble de la media).
  - Interés temporal tras victorias, podios y títulos, que se disipa.
  - Atractivo del producto.
  - Precio, con una sensibilidad distinta para cada tipo de comprador. Un precio muy bajo da como mucho 2,5 veces
    la demanda normal.
- Contabilidad coherente con el libro:
  - Comprar stock convierte caja en inventario (no es gasto).
  - Una venta anota el ingreso y el coste de lo vendido (sin movimiento de caja).
  - Gastos de tienda, envío y royalties.
  - El stock sin vender conserva su valor al cambiar de temporada; liquidarlo a un mayorista tiene pérdida.
  - Ventas perdidas por falta de stock.
- La IA gestiona su tienda con las mismas reglas: productos básicos y reposición según la demanda prevista.
- Afición y el interés por los resultados guardados por equipo. El merchandising cuenta como ingreso probable en la
  previsión de caja, nunca como confirmado.
- Pantalla «Tienda» (ES/EN):
  - Resumen de la temporada y productos ordenados por beneficio.
  - Por producto: precio con su previsión a varios precios, ventas por evento y pedidos con coste, cobertura,
    stock que probablemente sobrará y caja después, antes de confirmar.
  - Catálogo para añadir productos.
- Los presupuestos de los equipos oficiales quedan limitados por la capacidad financiera del fabricante. 52 tests
  en verde.

### Pilotos que aportan dinero (adelanto de E4) ✅
- `Rider.PersonalBacking`: dinero de patrocinadores personales o de la familia (solo en pilotos inventados: los
  acuerdos privados de los pilotos reales no son públicos). Cambia con los años.
- El mercado lo valora según el presupuesto y la necesidad del equipo: decisivo para un equipo modesto de Moto3,
  Moto2, WorldSSP o un WorldSBK pequeño, casi irrelevante en un equipo oficial de MotoGP. En las copas no se paga
  por la plaza.
- `Contract.RiderFunding`: el dinero entra por ronda en la contabilidad del equipo (`RiderFunding`), tanto con la
  IA como con el jugador; la columna «Aporta» aparece en el mercado.
- Un piloto solo paga cuando su nivel no basta para la plaza: por debajo del nivel que el equipo espera (media de
  la categoría ajustada por la reputación del equipo). Se vuelve a evaluar en cada contrato y renovación: si mejora,
  deja de pagar aunque siga teniendo patrocinadores. El mercado solo valora el dinero en ese caso.
- En 6 temporadas: 4–9 pilotos de pago por parrilla en las categorías pequeñas, entre 4 y 13 puntos más lentos que
  el resto; prácticamente ninguno en MotoGP. 55 tests en verde.

### Sesión E3 — Perfil del manager e inicio de carrera ✅
- `ManagerProfile` (reputación, negociación, gestión deportiva, finanzas, contactos, comercial, dinero propio,
  historial) separado de equipos y organizaciones. Hay 5 trayectorias iniciales en datos, con valores de diseño:
  personal de equipo, expiloto, ingeniero, directivo y empresario.
- Todos los equipos tienen un manager de la IA (personas inventadas).
- **Empleado:** solo ofrecen trabajo los equipos con la plaza abierta (alrededor de un tercio), con un máximo de 6
  ofertas y según tu reputación, gestión y contactos frente al tamaño del puesto. Un recién llegado encuentra
  trabajo en las categorías pequeñas. Aceptar no transfiere la propiedad: la organización paga tu sueldo, que va a
  tu dinero propio.
- **Fundador:**
  - Inviertes tu dinero (solo el tuyo, nunca el de la organización), pides un préstamo (importe y tipo según tus
    finanzas y tu reputación) y recibes patrocinadores interesados, cada uno condicionado a una categoría.
  - Viabilidad de cada categoría, incluidas las cuotas del préstamo. Solo se pueden elegir las viables o
    arriesgadas, y las arriesgadas con aviso.
  - Eliges el proveedor de motos. En las parrillas cerradas se compra la plaza del equipo privado más débil, que se
    retira y deja libres a sus pilotos y su personal; en las abiertas se paga la inscripción.
  - Todo queda en la contabilidad: inversión, préstamo, plaza, equipamiento y motos. Los pilotos se fichan entre los
    libres que aceptan.
- Las cuotas del préstamo se pagan por evento. La organización del jugador no recibe ayuda de propietarios externos.
- Interfaz: la lista libre de equipos se sustituye por el flujo de perfil → camino → empleo o fundación, con su
  comparación de categorías (ES/EN), y la pantalla de equipo muestra tu situación de manager. 59 tests en verde.

### Sesión E4a — Contratos avanzados: reglas, cláusulas, traspasos ✅
- Contrato con identificador:
  - Prima de firma.
  - Primas por carrera en tramos (solo se paga el mejor tramo: una victoria no cobra además el podio).
  - Prima de pole, primas por posición final y por punto.
  - Porcentaje de indemnización.
- Cláusulas con efecto real (`Contracts`):
  - Cláusula de rescisión.
  - Extensión por rendimiento (posición en el campeonato).
  - Opción del equipo: la IA decide; el jugador la ejerce durante la temporada.
  - Opción del piloto.
  - Salida libre hacia una categoría superior.
  - Salida si el equipo no cumple su objetivo.
- Orden en el cierre de temporada: resultados → primas de temporada (una sola vez por piloto) → extensión por
  rendimiento → opción del equipo → opción del piloto → cláusulas de salida → mercado. Una cláusula usada
  desaparece.
- Rescisión unilateral: lo que queda de la temporada en curso más el porcentaje pactado de las siguientes.
- Fichar a un piloto con contrato exige pagar: su cláusula de rescisión o su valor negociado (salario pendiente ×
  su importancia). Los pilotos del jugador solo salen por su cláusula. El mercado de la IA paga y cobra traspasos
  con las mismas reglas (desaparecen los «buyouts» gratis y la regla de +10 de prestigio).
- Renovar crea un contrato nuevo desde la temporada siguiente; ya no se reescribe el contrato en curso.
- Historial de contratos de cada piloto: equipo, fechas, salario, motivo de salida y cantidad pagada.
- Los pilotos de una copa suben normalmente a la categoría que alimenta (Rookies → Moto3); saltar un escalón
  es posible pero poco frecuente.
- 64 tests en verde; los traspasos cuadran (lo que paga el comprador lo cobra el vendedor).

### Sesión E4b — Negociación, traspasos entre equipos y fichajes inmediatos ✅
- Propuesta completa:
  - salario, años e incorporación inmediata o la temporada siguiente;
  - prima de firma y primas por victoria y podio;
  - cláusula de rescisión;
  - opción del equipo, renovación automática por resultados y salida a una categoría superior.
- Antes de proponer se ve el coste: salario garantizado, primas previstas, prima de firma, traspaso y caja que
  queda.
- El piloto valora la oferta según:
  - salario y primas que espera ganar (más si es ambicioso);
  - años (los jóvenes prefieren seguridad, las estrellas libertad);
  - cláusulas (la libertad le suma, la opción del equipo le resta);
  - la plaza comparada con la que tiene.

  Si no le basta, hace una contraoferta. Tras 3 rechazos deja de negociar esa temporada.
- Pilotos con contrato: se acuerda un traspaso con su equipo (la IA acepta, pide su precio o rechaza) o se paga su
  cláusula. Se pueden incorporar ya, sin ventana de traspasos, y el equipo que lo pierde cubre la plaza al momento.
- La IA puja por tus mejores pilotos: el traspaso se cobra ya y el piloto se va al final de la temporada.
  Puedes aceptar o rechazar.
- Pantallas (ES/EN):
  - **Mercado:** panel de negociación y ofertas recibidas.
  - **Equipo:** condiciones de cada contrato, negociar renovación, ejercer la opción y rescindir mostrando el
    coste (con doble confirmación).
- 70 tests en verde.

## Pendiente de especificación

Recibidas las secciones 49–105; ver la Fase 8.
