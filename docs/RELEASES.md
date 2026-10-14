# Publicar el APK

La gente descarga el juego desde la página **Releases** del repositorio
(`https://github.com/adridominguezz/motomanager/releases/latest`). Hay dos formas de poner un APK ahí.

## A. A mano (lo más rápido)

1. En Unity: **MotoManager → Build Android APK** (sale en `Builds/Android/MotoManager.apk`).
2. En GitHub: **Releases → Draft a new release**.
3. *Choose a tag* → escribe una versión nueva, por ejemplo `v0.1.0`, y *Create new tag*.
4. Título (`MotoManager 0.1.0`) y una nota corta; arrastra el `.apk` a la zona de adjuntos.
5. **Publish release**.

## B. Automático (GitHub Actions)

`.github/workflows/android.yml` compila el APK con Unity en la nube (GameCI) y, al subir una etiqueta `v*`,
crea la release con el APK adjunto. También se puede lanzar a mano desde **Actions → Android APK → Run
workflow** (el APK queda como *artifact* de esa ejecución).

Necesita estos *secrets* (Settings → Secrets and variables → Actions → New repository secret):

| Secret | Qué es |
|---|---|
| `UNITY_EMAIL` / `UNITY_PASSWORD` | La cuenta de Unity con la que tienes la licencia (Personal sirve). |
| `UNITY_LICENSE` | El contenido del fichero de licencia `.ulf` (guía: https://game.ci/docs/github/activation). |
| `ANDROID_KEYSTORE_BASE64` *(opcional)* | Tu keystore en base64 (`base64 -w0 motomanager.keystore`). |
| `ANDROID_KEYSTORE_PASS`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASS` *(opcional)* | Contraseña del keystore, alias y contraseña de la clave. |

Con keystore propio, cada versión nueva se instala encima de la anterior sin perder las partidas. Sin él, el
APK se firma con una clave de depuración distinta en cada compilación y habría que desinstalar antes de
actualizar. Guarda el keystore y sus contraseñas fuera del repositorio: si los pierdes no podrás actualizar
las instalaciones existentes.

Después, cada versión es: `git tag v0.2.0 && git push origin v0.2.0`.
