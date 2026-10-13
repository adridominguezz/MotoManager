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

## Circuitos con trazado real (10)

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

## Cómo conseguir los 17 circuitos que faltan (OpenStreetMap)

Siguen con `layout` aproximado: buriram, goiania, jerez, lemans, balaton, brno, assen, sachsenring, aragon, misano,
motegi, mandalika, phillipisland, valencia, most, donington, cremona.

1. Abre https://overpass-turbo.eu, borra el ejemplo y pega esta consulta (busca las vías `highway=raceway` en un radio
   de 3 km alrededor de cada circuito; cada bloque lleva el id del juego como comentario):

```
[out:json][timeout:180];
(
  way["highway"="raceway"](around:3000,14.9580,103.0850);   // buriram - Chang International Circuit
  way["highway"="raceway"](around:3000,-16.7180,-49.1940);  // goiania - Autódromo Internacional Ayrton Senna
  way["highway"="raceway"](around:3000,36.7086,-6.0340);    // jerez - Circuito de Jerez Ángel Nieto
  way["highway"="raceway"](around:3000,47.9560,0.2080);     // lemans - Circuit Bugatti
  way["highway"="raceway"](around:3000,47.0130,18.2000);    // balaton - Balaton Park Circuit
  way["highway"="raceway"](around:3000,49.2030,16.4450);    // brno - Automotodrom Brno
  way["highway"="raceway"](around:3000,52.9620,6.5240);     // assen - TT Circuit Assen
  way["highway"="raceway"](around:3000,50.7915,12.6885);    // sachsenring
  way["highway"="raceway"](around:3000,41.0780,-0.2030);    // aragon - MotorLand Aragón
  way["highway"="raceway"](around:3000,43.9615,12.6840);    // misano - Misano World Circuit
  way["highway"="raceway"](around:3000,36.5330,140.2270);   // motegi - Mobility Resort Motegi
  way["highway"="raceway"](around:3000,-8.8960,116.3050);   // mandalika - Pertamina Mandalika Circuit
  way["highway"="raceway"](around:3000,-38.5020,145.2340);  // phillipisland - Phillip Island Grand Prix Circuit
  way["highway"="raceway"](around:3000,39.4880,-0.6290);    // valencia - Circuit Ricardo Tormo
  way["highway"="raceway"](around:3000,50.5190,13.6070);    // most - Autodrom Most
  way["highway"="raceway"](around:3000,52.8300,-1.3800);    // donington - Donington Park
  way["highway"="raceway"](around:3000,45.0760,10.3320);    // cremona - Cremona Circuit
);
out body;
>;
out skel qt;
```

2. Pulsa **Ejecutar** y comprueba en el mapa que aparecen los 17 circuitos (si alguno sale vacío, acerca el mapa a ese
   circuito, busca el trazado y ajusta su coordenada en la consulta). Es normal que salgan también pit lanes, karts y
   variantes de trazado: el importador elegirá el anillo cerrado que más se acerque a `lengthM`.
3. **Exportar → GeoJSON** (descargar) y guarda el archivo en el repositorio como
   **`Tools/circuits/osm_raceways.geojson`** (crea la carpeta si no existe).
4. Pide la importación (siguiente pasada): unir los tramos de cada circuito en un anillo, elegir el trazado de MotoGP/SBK,
   fijar `shapeStart` en la línea de meta y `clockwise`, quitar `layout`, calibrar `radiusScale` y revisar las vistas
   previas, igual que con los 10 de arriba.

Los datos de OpenStreetMap son © colaboradores de OpenStreetMap, licencia ODbL: al importarlos hay que añadir aquí esa
atribución ("© OpenStreetMap contributors, ODbL 1.0", https://www.openstreetmap.org/copyright).

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
