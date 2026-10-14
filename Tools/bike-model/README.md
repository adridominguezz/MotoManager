# Modelo 3D de la moto

`Assets/MotoManager/Resources/Models/bike_lo*` (carrera, ~9k triángulos, textura 1024) y `bike_hi*` (editor de diseño,
~30k triángulos, textura 2048) salen del modelo del usuario (`New_Project_10_14_2026.glb`, 1,5 M triángulos, una sola
malla con la moto y el piloto). Pasos (Python 3 + numpy, Pillow, scipy, xatlas; `npm i @gltf-transform/cli@4.3.0`):

1. `gltf-transform dequantize in.glb deq.glb` (quita la compresión meshopt).
2. `lod.py`: extrae las texturas, suelda la geometría por posición y escribe `geom.glb` solo con posiciones.
3. `gltf-transform simplify geom.glb geom_lo.glb --ratio 0.006 --error 0.02` y
   `gltf-transform simplify geom.glb geom_hi.glb --ratio 0.02 --error 0.005`.
4. `lo_colors.py` / `hi_colors.py`: emparejan los vértices simplificados con los soldados (`lo.npz`, `hi.npz`).
5. `bake.py lo.npz 1024 lo` y `bake.py hi.npz 2048 hi`: desenvuelven con xatlas y hornean la textura original sobre el
   nuevo atlas (punto más cercano del modelo original con la normal compatible).
6. `export.py`: pasa a espacio de moto de Unity (adelante +Z, pivote en el suelo a mitad de batalla, ×1,75 → 1,4 m de
   batalla), escribe `bike_*.bytes` (formato en `RealBike.cs`), la textura base y la máscara
   (R color principal, G segundo, B acento, A brillo; clasificación por color en `mask.py`).
