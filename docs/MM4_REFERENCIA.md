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
