# Cómo colaborar

¡Gracias por querer mejorar MotoManager! Cualquier ayuda cuenta: código, balance de la simulación, datos,
arte 3D, sonido, traducciones o simplemente avisar de errores.

1. **Ideas y errores:** abre un *Issue* explicando qué pasa (o qué propones), con capturas si puedes.
2. **Código:** haz un *fork*, crea una rama y abre un *Pull Request* con una descripción breve del cambio.
   - El núcleo de simulación (`Assets/MotoManager/Scripts/Core`) es C# puro: añade o ajusta tests en
     `Tests/Core.Tests` y comprueba que pasan con `dotnet test Tests/Core.Tests`.
   - La capa Unity debe compilar: `dotnet build Tests/UnityCheck`.
   - Sigue el estilo del código que tocas (nombres, comentarios, sin dependencias nuevas sin hablarlo).
3. **Datos** (pilotos, equipos, circuitos, calendarios): están en JSON en `Assets/MotoManager/Resources/Data`
   y se pueden editar sin tocar código.
4. **Textos:** el juego está en español e inglés (`Assets/MotoManager/Resources/Localization`).

Al contribuir aceptas que tu aportación se publique bajo la misma licencia del proyecto
([PolyForm Noncommercial 1.0.0](LICENSE)): uso personal y no comercial.

---

**English:** contributions are welcome — open an Issue for bugs/ideas or a Pull Request with your change
(run `dotnet test Tests/Core.Tests` and `dotnet build Tests/UnityCheck` first). By contributing you agree
that your work is released under the project's licence (PolyForm Noncommercial 1.0.0).
