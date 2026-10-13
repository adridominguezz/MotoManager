# Referencia de navegación: Motorsport Manager 4 (MM4)

> Documento aportado por el usuario (análisis de 64 capturas de MM4 + inspección de una copia local de
> MotoManager). Se guarda aquí como especificación para adaptar la navegación y la interfaz.

## Decisiones del usuario sobre este documento

- **Paleta:** se mantiene la estética MotoGP actual (negro/carbón + rojo de carrera, color de equipo como
  identidad; ver `docs/VISUAL_STYLE.md`). NO se adopta el azul marino/verde lima de MM4. Donde el texto de
  abajo dice "verde lima" para la acción principal, léase "rojo de carrera (Theme primario)"; donde dice
  "cian para editar", léase el estilo secundario/acento existente.
- **Alcance:** se aplica por partes, después del repaso de estilo: (1) cabecera y acción contextual, (2)
  carruseles ‹ › de equipos/categorías/temporadas, (3) flujo del fin de semana con stepper de sesiones.
- **Rail lateral de iconos:** se mantiene (MM4 no aporta evidencia sobre su hub de inicio).
- **Iconos:** no se añaden iconos nuevos (regla del usuario).

## 1. Navegación reconstruida de MM4

### 1.1 No confirmado
Ninguna captura muestra el hub de inicio de MM4: no hay evidencia de barra lateral, barra inferior ni menú
principal. Tampoco se capturaron: plantilla de personal, instalaciones/I+D, tienda, mercado, negociación,
calendario. No se sabe si las píldoras de recursos son pulsables. No inventar estructura para rellenarlo.

### 1.2 Confirmado: cabecera persistente en gestión
- Izquierda: bloque oscuro inclinado (paralelogramo) con chevron `‹` de volver (siempre fuera de inicio).
- Centro: título de pantalla en mayúsculas.
- Derecha (gestión): píldoras de recursos (reputación/nivel en hexágono, caja, coste/delta de la ronda en
  rojo/naranja) + un bloque inclinado con la acción principal "INICIO".
- Derecha (flujo de evento): el mismo bloque dice "CONTINUAR"; en editor de equipo "APLICAR"; antes de un
  evento "CARRERA".
- Conclusión: UNA acción principal, siempre el mismo bloque inclinado arriba a la derecha, etiqueta y función
  según contexto (inicio / continuar / aplicar / iniciar evento).

### 1.3 Confirmado: tutoriales contextuales
Paneles a pantalla completa en secuencias de 2-3 ("01 CARRERAS", "02 ESTRATEGIA", "03 PARADAS EN BOXES"),
número + título, párrafo corto, ilustración, botón "CONTINUAR/ENTENDIDO" arriba a la derecha. Aparecen la
primera vez que una mecánica importa.

### 1.4 Confirmado: overlay de avisos
Modal con categorías ("OTROS"), lista de elementos icono + texto + botón "VER" que navega al contexto; X
roja para cerrar. Equivale a AVISOS/Assistant de MotoManager.

### 1.5 Confirmado: flujo de fin de semana (lineal, avanza con CONTINUAR)
1. Carga de la sede: arte de fondo, miniatura del circuito, probabilidad de lluvia, barra "CARGANDO...".
2. Selección de patrocinador del evento (si aplica): contexto de la ronda (bandera, GP, "RONDA X DE Y",
   tipo de sesión, circuito) + tarjetas de ofertas con "SELECCIONAR".
3. Resumen del fin de semana con stepper horizontal: ENTRENAMIENTO → CLASIFICACIÓN → SPRINT (si aplica) →
   CARRERA, con el día de cada una.
4. Por sesión: (a) planificación específica (prácticas: reparto de tiempo; clasificación: preparación;
   carrera: estrategia/neumáticos con "EDITAR"); (b) sesión en vivo (tabla a la izquierda, vista 3D, paneles
   de pilotos propios a la derecha, barra de controles); (c) resultados de la sesión (Pos/Piloto/Equipo/
   Ventaja/Puntos + objetivo y prima del patrocinador).
   Tras la carrera: (d) podio (3 tarjetas: posición, puntos, piloto, equipo, posición en campeonato);
   (e) clasificaciones actualizadas con toggle EQUIPO/PILOTOS; (f) "SIMULANDO OTROS CAMPEONATOS" con
   progreso; (g) resumen por piloto (mejoras, rasgos); (h) balance financiero del evento (saldo, coste de la
   carrera; desglose por concepto con columnas "esta ronda" / "año en curso", negativos en rojo).
No determinado: desde dónde se entra ni adónde se vuelve (se infiere inicio).

### 1.6 Confirmado: ficha de equipo navegable por carrusel
Nombre del equipo con flechas `‹ ›` para pasar a otro equipo del campeonato; pestañas HISTORIAL / mánager;
vehículo en vitrina; "EDITAR EQUIPO"; logo del campeonato con su propio `‹ ›`; posición en constructores +
botón "CLASIFICACIONES". A la derecha la lista de pilotos (foto, nombre, edad, valoración, "DETALLES").
HISTORIAL abre un modal por temporada con `‹ ›` entre temporadas.

### 1.7 Confirmado: navegador de campeonatos
Tarjeta del campeonato (logo/trofeo, categoría N, nombre, año, "REGLAS"), flechas `‹ ›` para recorrer TODOS
los campeonatos (también los que el jugador no gestiona); a la derecha tabla Pos/Piloto/Equipo/Victorias/PTS.

### 1.8 Confirmado: perfil del mánager
Tarjeta con bandera, nombre, rasgo de trayectoria y su efecto, nivel con barra de experiencia; debajo
secciones con cabecera clara y filas etiqueta/valor: estadísticas de partida, clasificación, sprint, carrera.

### 1.9 Confirmado: editor de equipo/librea
Vitrina 3D, nombre de equipo, "CAMBIAR LA LIBREA" (personalizados / aleatorio), carrusel `‹ ESTILO N ›`,
muestras de paleta; CTA "APLICAR". (Solo aplicar si MotoManager ya tiene un sistema equivalente; no
inventar funcionalidad nueva sin consultar.)

## 2. Lenguaje visual de MM4 (adaptado)
- Una acción principal por pantalla, bloque inclinado arriba a la derecha (en MotoManager: rojo primario).
- Acciones de editar/configurar diferenciadas de las de confirmar.
- Rojo para cerrar/negativos/alertas de coste.
- Tipografía condensada en negrita; títulos, cabeceras de tabla y botones en mayúsculas; cifras clave
  grandes en negrita.
- Tarjetas con franja de cabecera de dos tonos; tablas densas con zebra striping y cabecera más clara;
  carruseles `‹ ›` para moverse entre instancias de una entidad sin cambiar de pantalla; modales centrados
  sobre fondo oscurecido con un único botón de cierre.

## 3. Correspondencias con MotoManager (verificar contra el código real)
1. Cabecera con CTA contextual único ↔ `ManagementShell` (ya existe; adaptar forma/etiquetas).
2. Rail lateral ↔ sin evidencia en MM4: se mantiene (decisión del usuario).
3. Pestañas a todo el ancho ↔ en MM4 son toggles segmentados dentro de la tarjeta; valorar por página.
4. Flujo del fin de semana ↔ `RaceFlow`, `PreRaceDialog`, `QualifyingPanel`, `RaceHud`, `PostRacePanel`,
   `EventSummaryPanel`, `SeasonReviewPanel`: reorganizar con stepper de sesiones; no inventar mecánicas de
   coches (usar `TireModel`, `WeatherModel`, `RaceSimulation`).
5. Ficha de equipo con carrusel + historial ↔ evaluar en equipo/mercado; usar `HistoryService`.
6. Navegador de campeonatos ↔ Clasificaciones/Calendario con `‹ ›` entre MotoGP/Moto2/Moto3/WorldSBK/
   WorldSSP/Rookies, también las no gestionadas.
7. Editor de librea ↔ solo si existe algo equivalente; si no, consultar.
8. Perfil del mánager ↔ `CareerSummaryPage`/`ManagerPage`: aplicar secciones etiqueta/valor con datos reales.
9. Paleta ↔ se mantiene MotoGP (ver decisiones arriba).
10. Tutoriales y avisos ↔ `Assistant.cs` y AVISOS: alinear presentación con diapositivas numeradas.

## 4. Reglas de implementación
- Verificar el estado real del proyecto antes de cambiar nada; reutilizar `ScreenBase`, `UiFactory`, `Ui`.
- No eliminar pantallas ni funciones existentes; no sustituir datos reales por maquetas.
- Decisiones ambiguas: tomar la más coherente con la arquitectura y documentarla; consultar al usuario solo
  las de alto impacto e irreversibles.
- Sin dependencias nuevas salvo lo imprescindible; sin operaciones destructivas sin confirmación.
- Dominio y terminología de motociclismo (pilotos, motos, categorías reales).

## 5. Criterios de aceptación (resumen)
- Cabecera: volver + recursos + una acción principal contextual inclinada (inicio/continuar/aplicar/evento).
- Carruseles `‹ ›` entre instancias reales (equipos, categorías, temporadas) donde se implementen.
- Flujo de evento reconocible: planificación → en vivo → resultados por sesión; después podio / resumen /
  balance / clasificaciones, con la simulación real.
- Tablas con zebra, tarjetas de cabecera de dos tonos, CTA inclinado, aplicados de forma consistente.
- Sin regresiones (simular evento, cerrar temporada, guardar, menú, tamaño de texto...).

## Implementación

### Parte 1 — cabecera persistente con una única acción principal contextual (hecho)

Código: `Assets/MotoManager/Scripts/Unity/UI/Screens/ManagementShell.cs` (`BuildTopBar`, `BuildTopBarContent`,
`BuildAction`, `PageHasPrimary`, `BuildPills`). Textos nuevos: `Resources/Localization/ux_{es,en}/mm4.json`.

**Composición de la barra superior (72 de alto, de izquierda a derecha):**
1. **Bloque de volver**: paralelogramo inclinado oscuro (`Theme.PanelLight`, `UiFactory.Slant`) a toda la altura de
   la barra, con el pictograma `UiIcons.Back` (chevron ‹). Visible en todas las páginas salvo Inicio. Mantiene el
   comportamiento de antes (`Back()`: historial de páginas abiertas desde otra; si no hay, Inicio).
2. **Título** de pantalla en mayúsculas (tipografía display) sobre la fecha; en Inicio, el nombre del equipo. La
   etiqueta de progreso sustituye a la fecha mientras se simula (sin cambios).
3. **Bloques de recursos** (texto plano: leyenda sobre valor, separador fino; siguen siendo pulsables):
   caja (→ Finanzas; en rojo si es negativa), **reputación del mánager** (→ Mánager; nuevo) y posición/puntos en el
   campeonato (→ Clasificaciones). Sin equipo solo se muestra la reputación.
4. **AVISOS** (con su contador) y **NOTICIAS**, sin cambios.
5. **Acción principal**: bloque inclinado a toda la altura en el extremo derecho, etiqueta en mayúsculas + chevron.

**Regla de la acción principal (decisión):**
- La función es **siempre la acción de carrera** que ya calculaba el shell: `ContinueShort()` / `Continue()` →
  "CARRERA" (siguiente evento del equipo, simulando antes los de otros campeonatos), "SIMULAR" (sin equipo, evento a
  evento), "TERMINAR TEMP." (simula el resto) o "CERRAR TEMP." (`CloseSeason()`). Es igual en Inicio y en el resto de
  páginas.
- No se usa "INICIO" en las páginas de gestión: Inicio ya es alcanzable con el bloque de volver y con la primera
  celda del rail, y cambiar la función del bloque según la página haría desaparecer el botón de carrera que antes
  estaba en todas las subpáginas (regla: no perder acciones). En MotoManager, la acción que "mueve la partida" es la
  carrera, que es el equivalente del "CONTINUAR"/"CARRERA" de MM4.
- **Una sola acción principal por pantalla**: el bloque es **rojo de carrera** (`Theme.Action`, texto `OnAction`)
  cuando es la única principal; si la página que se acaba de construir muestra su propio botón principal rojo
  (`Ui.PrimaryCTA` o botón grande rojo; lo detecta `PageHasPrimary()` tras construir la página: aceptar
  patrocinador, hacer oferta, proponer, renovar, siguiente/terminar en la configuración del equipo, ver ofertas,
  INICIAR de I+D…), el bloque pasa a **oscuro** (`PanelLight`, texto en color de equipo legible) con la misma
  posición y función. Así la confirmación propia de la página sigue siendo el único botón rojo.
- **Inicio**: la ficha "Próxima carrera" ya no tiene su propio `PrimaryCTA` (CARRERA / CERRAR TEMP.), que duplicaba
  la acción de la cabecera; conserva SIMULAR EVENTO y SIMULAR TEMPORADA como secundarios (`HomeScreen.cs`,
  `RaceTile`; cambio mínimo). Con temporada terminada la ficha queda sin botones y la cabecera dice CERRAR TEMP.
  Inicio sin equipo mantiene "VER OFERTAS" como principal, así que allí la acción de la cabecera (SIMULAR) se ve
  oscura.

**MM4 §2 aplicado en el shell:** una acción principal por pantalla arriba a la derecha en bloque inclinado; rojo
para negativos (caja); títulos, botones y etiquetas de bloques en mayúsculas, cifras en negrita. Tablas/zebra,
tarjetas de dos tonos y modales quedan fuera de esta parte.

**Desviación consciente de `VISUAL_STYLE.md`:** allí "inclinado = solo etiquetas/marcas" y "cápsula = solo el
principal". Por decisión del usuario (adoptar la cabecera de MM4), los dos bloques de la cabecera (volver y acción
principal) son inclinados; el `PrimaryCTA` dentro de las páginas sigue siendo cápsula roja.

**Sin cambios:** rail de iconos, pestañas, AVISOS/NOTICIAS, menú, bloqueo durante simulaciones (los bloques nuevos
son `Button` y se bloquean igual), historial y volver, y todas las acciones (simular evento/temporada, cerrar
temporada, guardar…). Sin iconos nuevos; texto mínimo 18 (las etiquetas se ajustan con `Ui.Fit`).

### Parte 2 — carruseles `‹ ›` de campeonatos, equipos y temporadas (hecho)

Código: `Screens/InfoScreens.cs` (`StandingsScreen`, `CalendarScreen`, `HistoryScreen`, `ChampNav`), `Screens/TeamSheet.cs`
(`TeamSheet`, `Carousel`) y una línea en `Screens/MarketScreen.cs` (entrada a la ficha). Textos nuevos:
`Resources/Localization/ux_{es,en}/mm4b.json`. Sin iconos nuevos: las flechas son el pictograma `UiIcons.Back` dentro
de `Ui.IconButton` (56×56 de toque), girado 180° para `›`; una flecha sin destino se ve atenuada y no responde.

**Navegador de campeonatos (MM4 §1.7).** Recorre **todas las categorías simuladas** de la base de datos (orden
pirámide → nivel: MotoGP, Moto2, Moto3, Rookies, WorldSBK, WorldSSP…), las gestione el jugador o no; las flechas dan la
vuelta.
- **Revisión tras feedback del usuario** ("veo duplicada esta información"): se quitó la tarjeta lateral del
  campeonato (temporada, ronda, líder, tu equipo) y la lista "Todos los campeonatos", que repetían lo que ya dicen la
  banda de la tabla, las flechas y la fila propia fijada. El navegador pasó primero a la banda de la tabla (luego a una fila propia, ver abajo)
  (`ChampNav.Navigate` sobre la banda de `Wp4Ui.Table`): "‹ MOTOGP · PILOTOS ›" a la izquierda y la ronda ("Ronda 2/22")
  a la derecha; tocar el nombre abre una hoja pequeña con todos los campeonatos para saltar directamente (sustituye a
  la lista). La banda usa el **color propio del campeonato** (`categories.json`: MotoGP rojo, Moto2 azul…) en lugar del
  acento del equipo, con tinta elegida por contraste (`Theme.OnColor`); el subrayado de la pestaña activa usa el mismo
  color (aclarado hasta 3:1 sobre el fondo). La tabla ocupa todo el ancho. No se muestra "No gestionas esta
  categoría" (era duplicación). La fila del jugador sigue resaltada y fijada con el color de su equipo (identidad).
- **Segunda revisión** ("el selector de campeonato debería estar arriba"): la banda navegadora "‹ MOTOGP ›  Ronda
  2/22" es ahora la **primera fila** de la página, encima de las pestañas; la tabla ya no repite el nombre: su banda de
  título se reduce a una franja fina del color del campeonato (`ChampNav.Slim`) y empieza por las cabeceras de columna.
- **Clasificaciones**: banda navegadora, debajo el segmentado PILOTOS / EQUIPOS / FABRICANTES / EVOLUCIÓN y la tabla
  (o la gráfica de EVOLUCIÓN).
- **Calendario**: la misma banda con `‹ ›` arriba (lleva la "i" del calendario) y la tabla de rondas debajo. Cada entrada es una categoría con calendario: se listan solo las rondas que
  corre esa categoría y el ganador es el de **su** carrera principal (antes: siempre la categoría reina del
  calendario). Un calendario que no use ninguna categoría simulada se añade al final como entrada propia (banda gris).
  El texto "i" se actualiza (`ux.mm4b.calendarInfo`).
- **Historial**: el selector de categoría se sustituye por un carrusel compacto `‹ MotoGP ›` en la misma celda,
  relleno con el color del campeonato; tocar el nombre abre la lista completa en una hoja. Además, la fila de un
  campeón abre la ficha de su equipo si el equipo sigue existiendo.

**Ficha de equipo con carrusel (MM4 §1.6).** Hoja modal (`Ui.Sheet`, 86% de ancho), así se abre sin salir de la
pantalla y sin tocar `ManagementShell`. Entradas: fila de la tabla EQUIPOS de Clasificaciones (antes abría la hoja de
cifras, que ahora están dentro de la ficha), botón VER EQUIPO en la hoja de un piloto de la clasificación, VER EQUIPO
en la situación de un piloto del mercado y la fila de un campeón en Historial.
- Cabecera: nombre del equipo entre `‹ ›` (equipo anterior / siguiente de **la misma categoría**, en orden de la
  clasificación de equipos y luego los no clasificados; da la vuelta), con "categoría · n / N (· Tu equipo)" y una
  marca con el color del equipo. Pestañas de texto EQUIPO / HISTORIAL (se recuerdan al pasar de equipo).
- EQUIPO: campeonato (temporada, posición P/N, puntos, victorias, podios, poles); moto (fabricante, modelo y año,
  estructura oficial/satélite/independiente, **nivel de la moto** = media de los valores de rendimiento como en la
  página Moto, país); pilotos (dorsal, nombre + nacionalidad, edad, nivel, posición en el campeonato).
- HISTORIAL: temporadas terminadas del equipo, una a una con su propio `‹ ›` (‹ = más antigua, › = más reciente; sin
  vuelta): posición P/N, puntos, victorias, podios, poles, título de equipos si lo ganó, campeón de pilotos de esa
  temporada, y los pilotos que corrió ese año (posición, puntos, victorias); debajo la lista de todas las temporadas
  para saltar a una.

**Datos (solo reales) y límites documentados:**
- Historial del equipo = `WorldState.ArchivedChampionships` (clasificación de equipos que se archiva al cerrar cada
  temporada en `SeasonEndProcessor.RollOver`) + `world.Champions` (título de equipos) + `world.History`
  (`SeasonHistory`, campeón) + `Rider.CareerHistory` (`SeasonRecord` con `TeamId`, también de pilotos retirados). La
  temporada en curso no aparece en HISTORIAL (está en EQUIPO). En una partida nueva el historial está vacío y se
  explica que se llena al cerrar temporadas.
- `HistoryService` no guarda nada por equipo (solo campeones, récords, rivalidades y salón de la fama de pilotos); por
  eso se usa el archivo de clasificaciones.
- No existen en el mundo: la moto o el fabricante de un equipo en temporadas pasadas (el archivo no lo guarda), fotos
  de pilotos, mánager/personal rival con historial, ni presupuesto de rivales: no se muestran. El "nivel" de los
  pilotos es la estimación de habilidad de carrera que percibe tu equipo (`ScoutingService.Perceive`, igual que en el
  mercado; exacto con tus pilotos), para no revelar atributos ocultos de rivales; se aclara bajo la tabla.
- La ficha de manager/"EDITAR EQUIPO"/"REGLAS" de MM4 no se añaden (no hay sistema equivalente para rivales).

**Sin cambios:** tablas de clasificación, fila propia fijada, EVOLUCIÓN, hojas de cifras de pilotos/fabricantes,
vistas de Historial, cabecera y acción principal de la parte 1. Texto mínimo 18 (etiquetas con `Ui.Fit`).
