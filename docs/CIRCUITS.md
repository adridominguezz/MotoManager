# Circuitos: trazados reales y escenario

## Cómo se define un circuito

Cada circuito de `Assets/MotoManager/Resources/Data/circuits/*.json` tiene una de estas dos formas:

- **`layout`** (trazado aproximado escrito a mano): DSL `S<m>` recta, `L|R<grados>@<radio m>` curva, banderas
  `:tl` (límites de pista) y `:ll` (long lap). Las rectas se reescalan para que la vuelta mida `lengthM`.
- **`shape`** (trazado real): línea central `[[lat, lon], ...]` con `"shapeIsLatLon": true`, `"shapeStart"` (punto de la
  línea de meta), `"clockwise"` (sentido de carrera) y `"radiusScale"` (calibración del tiempo de vuelta). Sin `layout`.

Con `shape`, `ShapeConverter` (Core/Race) suaviza la línea (spline Catmull-Rom centrípeta), la remuestrea cada 2 m, la
gira para que empiece en `shapeStart` y en el sentido indicado, y la trocea en rectas y curvas (radio y ángulo medidos)
para la simulación; marca `:tl` en la primera y la última curva. `TrackGeometry` **dibuja la línea real tal cual** y
coloca cada segmento simulado sobre su tramo real, así que las motos se inclinan donde está la curva de verdad.
`radiusScale` se calibra para que el piloto de referencia haga el `referenceLaps` (MotoGP, o WorldSBK si el circuito
no tiene MotoGP): `dotnet run -c Release --project Tests/SimRunner -- calibrate motogp_2026 <id>` (ojo: reescribe el
JSON con sangría; también vale ajustar el número a mano y comprobar con `laps`).

## Circuitos con trazado de F1 (10)

| id | circuito | fuente | puntos | longitud línea / oficial | radiusScale |
|---|---|---|---|---|---|
| catalunya | Circuit de Barcelona-Catalunya | es-1991 | 149 | 4667 / 4657 m | 1.005 |
| redbullring | Red Bull Ring | at-1969 | 80 | 4311 / 4318 m | 0.744 |
| silverstone | Silverstone Circuit | gb-1948 | 134 | 5888 / 5900 m | 1.278 |
| cota | Circuit of the Americas (antihorario) | us-2012 | 170 | 5520 / 5513 m | 1.558 |
| portimao | Autódromo Internacional do Algarve | pt-2008 | 100 | 4649 / 4592 m | 1.361 |
| mugello | Autodromo Internazionale del Mugello | it-1914 | 128 | 5245 / 5245 m | 1.316 |
| sepang | Sepang International Circuit | my-1999 | 103 | 5545 / 5543 m | 1.191 |
| lusail | Losail International Circuit | qa-2004 | 106 | 5415 / 5380 m | 1.256 |
| magnycours | Circuit de Nevers Magny-Cours | fr-1960 | 94 | 4461 / 4411 m | 1.391 |
| estoril | Autódromo do Estoril | pt-1972 | 82 | 4165 / 4182 m | 1.047 |

Las líneas de F1 coinciden con el trazado de motos salvo detalles (Portimão y Estoril difieren ~1 %; la simulación usa
siempre `lengthM`). El test `ShapeTests.RealCentreLinesAreDrawnAsIsAndMatchTheOfficialLength` comprueba longitud,
sentido, banderas y que cada curva simulada cae en una curva real.

### Atribución (licencia MIT)

Las líneas centrales de esos 10 circuitos proceden de **bacinger/f1-circuits** (https://github.com/bacinger/f1-circuits),
simplificadas a 6 decimales:

```
Copyright (c) 2019-2025 Tomislav Bacinger

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Circuitos con trazado de OpenStreetMap (17)

Los 27 circuitos usan ya una línea real; ya no queda ningún `layout` escrito a mano en los datos (el DSL sigue
funcionando para circuitos nuevos). Los 17 de abajo salen de OpenStreetMap: vías `highway=raceway` exportadas con
overpass-turbo a `Tools/circuits/*.geojson` (datos en bruto, sin tocar). Vista de los 27 con la meta y el sentido de
carrera en rojo: `docs/circuits_current.png`.

| id | circuito | puntos | longitud línea / oficial | sentido | radiusScale |
|---|---|---|---|---|---|
| buriram | Chang International Circuit | 98 | 4570 / 4554 m | horario | 1.466 |
| goiania | Autódromo Internacional Ayrton Senna | 94 | 3830 / 3835 m | horario | 1.059 |
| jerez | Circuito de Jerez - Ángel Nieto | 133 | 4427 / 4423 m | horario | 1.135 |
| lemans | Circuit Bugatti | 127 | 4166 / 4185 m | horario | 1.236 |
| balaton | Balaton Park Circuit | 115 | 4078 / 4075 m | antihorario | 1.390 |
| brno | Automotodrom Brno | 159 | 5404 / 5403 m | horario | 0.998 |
| assen | TT Circuit Assen | 121 | 4538 / 4542 m | horario | 1.533 |
| sachsenring | Sachsenring | 127 | 3650 / 3671 m | antihorario | 1.133 |
| aragon | MotorLand Aragón | 140 | 5071 / 5077 m | antihorario | 1.201 |
| misano | Misano World Circuit Marco Simoncelli | 110 | 4220 / 4226 m | horario | 1.535 |
| motegi | Mobility Resort Motegi | 114 | 4814 / 4801 m | horario | 1.359 |
| mandalika | Pertamina Mandalika Circuit | 106 | 4313 / 4301 m | horario | 1.403 |
| phillipisland | Phillip Island Grand Prix Circuit | 151 | 4456 / 4448 m | antihorario | 0.814 |
| valencia | Circuit Ricardo Tormo | 107 | 4001 / 4005 m | antihorario | 1.434 |
| most | Autodrom Most | 127 | 4210 / 4212 m | horario | 1.281 |
| donington | Donington Park (GP, con Melbourne Loop) | 110 | 3997 / 4023 m | horario | 1.334 |
| cremona | Cremona Circuit | 105 | 3702 / 3754 m | antihorario | 1.385 |

Cómo se importaron:

- **Anillo:** se unen las vías por sus nodos compartidos (descartando pit lanes, karting, motocross y tierra) y se
  elige el ciclo cerrado cuya longitud se acerca más a `lengthM`; se revisó cada uno a ojo frente a las variantes.
- **Sentido:** el de las vías `oneway=yes` de OSM (van en sentido de carrera). Brno, Motegi y Valencia no lo tienen y
  usan el del `layout` antiguo. En Goiânia, Balaton y Cremona OSM contradice al `layout` antiguo y manda OSM.
- **Meta (`shapeStart`):** en la recta de boxes, a media pit lane cuando OSM la tiene; si no, por la secuencia de curvas
  del `layout` antiguo o a mano en la recta principal. Las de Balaton, Most y Sachsenring son las menos seguras.
- **Simplificación:** Douglas-Peucker a 0,6 m (94-159 puntos); `radiusScale` calibrado con `SimRunner calibrate`.

Para añadir o rehacer un circuito: abre https://overpass-turbo.eu (servidor `https://overpass.kumi.systems/api/` si el
de por defecto está ocupado), encuadra solo ese circuito y ejecuta:

```
[out:json][timeout:60];
(
  way["highway"="raceway"]({{bbox}});
  way["area:highway"="raceway"]({{bbox}});
  way["leisure"="track"]["sport"~"motor|motorcycle"]({{bbox}});
  relation["type"="route"]["route"="raceway"]({{bbox}});
  relation["type"="circuit"]({{bbox}});
);
out body;
>;
out skel qt;
```

Exporta a GeoJSON en `Tools/circuits/` y repite los pasos de arriba.

### Atribución (OpenStreetMap, ODbL)

Trazados de buriram, goiania, jerez, lemans, balaton, brno, assen, sachsenring, aragon, misano, motegi, mandalika,
phillipisland, valencia, most, donington y cremona: © OpenStreetMap contributors, disponibles bajo la Open Database
License (ODbL) 1.0, https://www.openstreetmap.org/copyright. Los créditos del juego deben incluir esta línea.

## Escenario de carrera

`TrackPlan` (Unity/Race, sin dependencias de Unity) planifica todo lo que rodea la pista y `TrackRenderer` lo convierte
en cuatro mallas con colores por vértice y el mismo shader (`VertexColorLit`, con desplazamiento de profundidad para las
capas planas): suelo, superficies, pintura y objetos.

- Asfalto en paneles con ligera variación de tono y la trazada de goma más oscura; líneas blancas de borde.
- Pianos rojos y blancos en el vértice (interior) y a la entrada y salida (exterior) de cada curva de la simulación.
- Escapatorias en el exterior: franja pintada (azul o verde según el circuito) y después grava en las frenadas fuertes
  y curvas lentas o asfalto claro en las rápidas, con anchura según la velocidad de paso.
- Césped segado a franjas hasta las barreras: guardarraíl con publicidad en las rectas y muros de neumáticos detrás de
  las escapatorias. Todo se recorta donde otro tramo del circuito queda más cerca, así nada cruza la pista.
- Línea de meta a cuadros, pórtico de semáforos y cajones de parrilla; pit lane con muro, boxes con los colores de los
  equipos, edificio con planta acristalada, torre de control, banderas y paddock con camiones.
- Tribuna principal frente a boxes y tribunas con público en las frenadas más fuertes; lagos, bosques y casas lejos de
  la pista. Todo se genera a partir del id del circuito, así que cada visita se ve igual.
