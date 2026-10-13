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
