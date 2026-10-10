# Análisis de los requisitos 49–105 frente a lo implementado

Este documento compara la parte del documento original que llegó cortada (secciones 49–105) con lo que
ya existe en la rama, y propone cómo completarlo. Siguiendo las secciones 79 y 103, **no se ha programado
nada nuevo**: se espera aprobación de las decisiones del apartado final.

> Nota honesta sobre el proceso: las secciones 79/103 pedían un análisis previo y esperar aprobación
> antes de programar. Como esa parte no llegó, se trabajó por fases con el plan de `docs/PLAN.md`.
> El orden seguido sí respeta las prioridades de la sección 88 (simulación y carrera visual primero).

Leyenda: ✅ cubierto · 🟡 parcial · ❌ falta

## 1. Estado por sección

| § | Tema | Estado | Qué hay / qué falta |
|---|---|---|---|
| 49 | Patrocinadores | 🟡 | Hay pago por temporada, bonus por carrera/podio/victoria, bonus de campeonato, reputación mínima y pirámide. Falta separar explícitamente `BasePayment` / `PerformanceBonus` / `ChampionshipBonus` y una `CategoryPreference` real (hoy solo GP/SBK). |
| 50 | Contratos | 🟡 | Duración, salario, bonus por victoria/podio, equipo y fabricante. Faltan **cláusulas** (rescisión, opción de renovación, cláusula de rendimiento), **categoría** explícita y **expectativas** (objetivo de posición que afecta a moral y renovación). El mercado ya se resuelve entre temporadas. |
| 51 | Personal | 🟡 | Roles actuales: TeamManager, ChiefEngineer, CrewChief, DataEngineer, TireEngineer, Aerodynamicist, Designer, Scout, Mechanic. Faltan TechnicalDirector, RaceEngineer, DataAnalyst y Physio, y que cada empleado tenga experiencia, reputación y contrato propio (hoy: habilidad y salario). |
| 52 | Instalaciones | 🟡 | Hay 6 (Workshop, DesignOffice, Simulator, DataCenter, Academy, Hospitality) con nivel, coste y mantenimiento. No coinciden con la lista pedida (Factory, Dyno, WindTunnel, ElectronicsLab, Simulator, RiderTrainingCenter, ScoutingCenter) y sus beneficios son genéricos. |
| 53 | Scouting | 🟡 | Atributos visibles/parciales/ocultos, rangos que se estrechan, asignaciones de ojeadores, potencial oculto. Falta **descubrir** pilotos ocultos de las canteras (hoy todos aparecen en el mercado), revelar estilo y personalidad, y un informe del ojeador. |
| 54 | Mercado de pilotos | ✅ | Sistema completo entre temporadas: fin de contratos, renovaciones, ofertas, cascada de vacantes, cambios GP↔SBK, retiradas, nuevos talentos y plantillas completas. Sin lista fija. |
| 55 | IA de equipos | ❌ | Todos los equipos usan la misma fórmula. Faltan objetivos, filosofía, nivel técnico, prioridades, preferencias de pilotos (joven / experimentado / fabricante / rendimiento inmediato / largo plazo) y tolerancia al riesgo. |
| 56 | Diferencias entre equipos | 🟡 | Hay presupuesto, reputación, instalaciones, personal, estado oficial/satélite y fuerza del fabricante. Faltan `TechnicalLevel`, `DevelopmentRate`, `ManufacturerSupport` y `RiskTolerance` como variables propias del equipo. |
| 57 | Diferencias entre circuitos | ✅ | Derivado de la geometría (longitud de rectas, frenadas, radios, rebufo, abrasividad, temperatura); no hay tabla de bonus. Falta **mostrarlo** al jugador ("circuito de velocidad punta"…). |
| 58 | Campeonato | ✅ | Puntos por posición configurables, sprint, Superpole Race, desempate por victorias/segundos/terceros, clasificaciones de pilotos, equipos y fabricantes. Puntos bonus: soportables por datos, ninguna categoría los usa. |
| 59 | Historial del campeonato | 🟡 | Se guarda campeón, equipo, fabricante y puntos de cada temporada y el historial de cada piloto. Faltan subcampeón y récords. |
| 60 | Historia alternativa | ✅ | Tras 2026 todo es simulado; no se sigue la realidad. |
| 61 | WorldHistory | 🟡 | Campeones, campeonatos archivados y noticias. Faltan récords, debutantes, retiradas, cambios de categoría y rivalidades como registro estructurado. |
| 62 | Hall of Fame | ❌ | No es MVP (se propone para el final). |
| 63–64 | Estilo de UI y color | 🟡 | UI oscura, técnica y densa, con tablas y color de equipo solo en identidad. El fondo es `#0E1116`, no `#0B1320`; falta una pasada visual (y aún no se ha visto en Unity). |
| 65 | Pantallas | 🟡 | Hay Menú/Carrera deportiva, Inicio, Calendario, Clasificaciones, Equipo, Moto+I+D, Finanzas, Mercado, Historial, Fin de semana (previo), Carrera y Resultados. Faltan Settings y separar Riders / Staff / Facilities / Contracts / Scouting / Development como pantallas propias. |
| 66 | Dashboard | 🟡 | Muestra próxima carrera, posición, presupuesto, pilotos y noticias. Faltan alertas, estado del desarrollo, contratos que vencen y mini calendario. |
| 67 | Pantalla de carrera | 🟡 | Vista, torre, vuelta, gaps, neumáticos, clima, eventos y estrategia. Hay "Circuito completo" y "Seguir piloto"; falta **Vista de sector**. |
| 68 | Race Tower | 🟡 | Posición, piloto, color de equipo, neumáticos, gap y estado. Falta el nombre del equipo en la fila. |
| 69 | Eventos | ✅ | Adelantamientos, caídas (con tipo), lluvia, pit lane, long lap, averías; solo los importantes en pantalla. |
| 70 | Post-race | ❌ | Solo hay clasificación. Faltan mejor vuelta y sectores, adelantamientos, posiciones ganadas/perdidas, neumáticos, errores, penalizaciones, fiabilidad, puntos y **"¿Por qué hemos rendido así?"**. |
| 71 | Debug build | ❌ | No hay herramientas de depuración en el juego. |
| 72 | Guardado | 🟡 | Autoguardado, guardado manual, versión 1 y guardado atómico. Faltan varios slots y el sistema de migraciones. |
| 73 | Testing | ✅ | 28 tests centrados en simulación, gestión y persistencia (sin obsesión por cantidad). Falta un test de migración cuando exista la v2. |
| 74 | Optimización | ✅ | Simulación matemática + representación ligera; ~0,45 s por evento con 6 categorías en escritorio. Falta medir en Android. |
| 75–77 | Arquitectura | 🟡 | Separación Datos / Dominio-Simulación / Presentación / Persistencia y núcleo sin Unity. Pero la UI llama a servicios y en algún caso modifica el mundo directamente (p. ej. asignar scouting): falta una capa ligera de **comandos de aplicación**. |
| 78–88 | Fases y prioridades | ✅ | Trabajo por fases, cada una ejecutable y probada; orden coherente con §88. |
| 89–91 | Qué no hacer / offline / datos | ✅ | Sin DRS, sin online, sin monetización; todo offline; datos reales separados del código. |
| 92–94 | Motor y renderer | ✅ | Componentes equivalentes (RaceSimulation, TrackModel, TireModel, WeatherModel, RaceAI, RaceEngineer; TrackRenderer, BikeVisual, RaceCamera, RaceHud). La simulación no depende del renderer. |
| 95 | Sistema de eventos | 🟡 | `RaceEvent` cubre adelantamientos, caídas, averías, penalizaciones, boxes, clima y meta. Faltan `LapCompleted`, `SectorCompleted` y `TireStateChange`, necesarios para el análisis post-carrera. |
| 96 | Replay | ✅ | Replay determinista por configuración + órdenes (más de lo pedido para el MVP). |
| 97 | IA de equipos (decisiones) | 🟡 | La IA decide pilotos, desarrollo y estrategia de carrera con servicios internos, pero sin la personalidad de §55. |
| 98–102 | Balance y realismo | 🟡 | Categorías con modelos distintos (prototipo, motor único, monocilíndrica, copa, producción SBK/SSP) y calibradas a ±2 % del ritmo real. El pendiente principal son los **trazados reales** (formas aproximadas; 5 se cruzan). |

## 2. Contradicciones y ambigüedades detectadas

1. **§51 frente al personal actual**: CrewChief y TireEngineer encajan en *RaceEngineer*; Designer y
   Aerodynamicist, en *TechnicalDirector*; DataEngineer pasa a ser *DataAnalyst*. Cambiarlo rompe los
   guardados actuales y obliga a la primera migración (§72) — de hecho es buena ocasión para estrenarla.
2. **§52**: "Factory" solapa con el fabricante (§19). Propuesta: *Factory* = taller del equipo (velocidad
   de desarrollo y proyectos en paralelo); el fabricante sigue aportando la moto base.
3. **§55/§56**: el equipo y el fabricante comparten variables (nivel técnico, fiabilidad). Propuesta: el
   fabricante define la moto y su evolución; el equipo, cómo la explota (personal, instalaciones,
   filosofía) y `ManufacturerSupport` decide cuánta mejora de fábrica recibe un satélite.
4. **§49 CategoryPreference**: ¿preferencia por una categoría concreta o por la pirámide? Propuesta: lista
   de categorías con peso, de modo que la pirámide sea un caso particular.
5. **Idioma**: los ejemplos del documento están en español ("PILOTO X adelanta a PILOTO Y") y la UI
   actual está en inglés. Hay que decidirlo.
6. **§79/§103 vs. estado real**: el proyecto ya está en la fase de gestión. Propongo no rehacer nada y
   cerrar los huecos de este documento en el orden de prioridad de §88.

## 3. Propuesta de sesiones (por prioridad §88/§105)

| Sesión | Contenido | Secciones |
|---|---|---|
| **8.1 Análisis de carrera** | Eventos de vuelta/sector/estado de neumático; post-carrera completo (tiempo, gap, mejor vuelta, mejores sectores, adelantamientos, posiciones ganadas/perdidas, neumáticos, errores, penalizaciones, fiabilidad, puntos) y **"¿Por qué hemos rendido así?"**: fortalezas y debilidades calculadas comparando velocidad por tipo de tramo (rectas, frenadas, curvas lentas/rápidas), desgaste, adelantamientos intentados/logrados y errores frente a los rivales. Perfil del circuito visible ("favorece velocidad punta / paso por curva / gestión de neumático"). Torre con equipo; cámara de **vista de sector**. | 57, 67, 68, 70, 95, 100 |
| **8.2 IA y personalidad de equipos** | Perfil por equipo en datos: objetivos, filosofía (joven / experimentado / fabricante / inmediato / largo plazo), nivel técnico, ritmo de desarrollo, apoyo del fabricante, tolerancia al riesgo. Lo usan el mercado, el desarrollo, el presupuesto y la estrategia de carrera de la IA. | 55, 56, 97 |
| **8.3 Contratos completos** | Cláusulas (rescisión con coste, opción de renovación, cláusula de rendimiento), categoría, expectativas por temporada que afectan a moral, renovación y reputación; negociación con contraoferta. | 50, 54 |
| **8.4 Personal e instalaciones según especificación** | Roles de §51 con experiencia, reputación y contrato; instalaciones de §52 con beneficios concretos (Dyno → motor y fiabilidad, WindTunnel → aerodinámica, ElectronicsLab → electrónica y tracción, Simulator → puesta a punto, RiderTrainingCenter → forma física y recuperación de lesiones, ScoutingCenter → scouting). **Primera migración de guardado (v1 → v2).** | 51, 52, 72 |
| **8.5 Scouting y patrocinadores** | Pilotos de cantera ocultos hasta descubrirlos, informe del ojeador (potencial, estilo, personalidad, nacionalidad, rendimiento), Scouting Center; patrocinadores con BasePayment / PerformanceBonus / ChampionshipBonus / ReputationRequirement / CategoryPreference. | 49, 53 |
| **8.6 WorldHistory** | Subcampeón, récords (victorias, poles, podios, títulos, más joven/mayor ganador, rachas), debutantes, retiradas, cambios de categoría, rivalidades (pares que se disputan victorias/títulos), resumen por temporada; almacenamiento compacto para décadas. | 59, 61 |
| **8.7 Guardado, ajustes y debug** | Varios slots, guardado manual, migraciones encadenadas y test de migración; pantalla Settings (idioma, velocidad por defecto, FPS, autoguardado); panel de depuración solo en builds de desarrollo (ForceRain/ForceDry/ForceCrash/ForceMechanicalFailure/SetSeed/AddMoney/CompleteDevelopment/SimulateRace/SetTireWear/SetWeather), excluido de producción por compilación condicional. | 65, 71, 72 |
| **8.8 Pasada de UI** | Base `#0B1320`, Dashboard de §66 (alertas, desarrollo, contratos, calendario), pantallas de §65 separadas, gráficos (evolución de gaps, puntos por ronda), idioma acordado. Capa ligera de comandos de aplicación entre UI y dominio (§76). | 63–66, 76 |
| **Paralelo** | Trazados reales desde coordenadas (requiere acceso a `overpass-api.de` o ficheros GPX/KML) y prueba en Unity/Android (requiere tu máquina). | 98–102 |
| **Final** | Hall of Fame. | 62 |

## 4. Decisiones que necesitan aprobación

1. **Orden de las sesiones** de la tabla anterior (propuesta: 8.1 → 8.2 → 8.3 → 8.4 → 8.5 → 8.6 → 8.7 → 8.8).
2. **Personal**: sustituir los roles actuales por los 8 de §51 con la correspondencia del punto 2.1 (requiere migración de guardado).
3. **Instalaciones**: sustituir las 6 actuales por las 7 de §52 con los beneficios propuestos.
4. **Idioma de la interfaz**: español, inglés, o ambos con selector en Settings.
5. **CategoryPreference** de patrocinadores como lista de categorías con peso.
6. **Capa de comandos de aplicación**: añadirla de forma ligera (un único punto de entrada para las acciones del jugador), sin interfaces ni patrones adicionales (§77).
