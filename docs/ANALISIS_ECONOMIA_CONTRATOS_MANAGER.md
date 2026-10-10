# Economía, contratos y carrera del manager — auditoría y plan

Análisis del documento «Evolución del juego: economía, contratos y carrera profesional del manager»
contra el estado real del repositorio (rama `claude/motorcycle-motorsport-manager-a2iz6p`, tras la sesión 8.2).
**No se ha modificado código.** Las secciones 1–3 son hechos observados; las secciones 4 y siguientes son propuestas.

---

## 1. Hechos observados: qué existe hoy

Arquitectura: el núcleo de C# está en `Assets/MotoManager/Scripts/Core` y no depende de Unity
(netstandard2.1, C# 9, Newtonsoft). La interfaz de Unity está en `Scripts/Unity` y se construye por código.
Los datos están en JSON, en `Resources/Data`, y hay pruebas xUnit en `Tests/Core.Tests` (34 en verde).
El estado de la partida se guarda en un único objeto serializable, `WorldState` (`World/WorldState.cs`).
Toda la aleatoriedad pasa por `DeterministicRandom`.

### 1.1 Entidades del dominio

| Concepto del documento | Qué hay en el código | Archivo |
|---|---|---|
| Manager | **No existe.** El jugador es el valor `WorldState.PlayerTeamId` más `Team.IsPlayer`. | `World/WorldState.cs`, `World/Organisations.cs` |
| Organización | **No existe.** Cada `Team` es independiente y tiene su propio dinero. | — |
| Equipo | `Team`: categoría, estado (Factory/Satellite/Independent), moto, pilotos, `Budget`, `Reputation`, instalaciones, personal, patrocinios y `Personality` (8.2) | `World/Organisations.cs` |
| Fabricante | `Manufacturer`: `Programs` (categoría → moto base), `DevelopmentBudget`, `EngineeringStrength`, concesiones | `World/Organisations.cs` |
| Patrocinador | `SponsorTemplate` en los datos (33) y `SponsorDeal` firmado dentro de `Team.Sponsors` | `Data/WorldDefinitions.cs`, `Organisations.cs` |
| Piloto | `Rider`: atributos, personalidad (ambición, lealtad, preferencia GP/SBK), reputación, moral, `Contract`, `FutureContract`, `CareerHistory` por temporada | `World/Rider.cs` |
| Personal | `StaffMember`: rol, habilidad, salario y equipo. **Sin contrato** (no tiene duración) | `Organisations.cs` |
| Contrato de piloto | `Contract {TeamId, ManufacturerId, StartSeason, EndSeason, SalaryPerSeason, WinBonus, PodiumBonus, FactoryBacked}` | `World/Rider.cs` |
| Contrato de patrocinio | `SponsorDeal {PaymentPerSeason, Start/EndSeason, Satisfaction, Objectives, BonusEarnedThisSeason}` | `Organisations.cs` |
| Contrato laboral, ofertas de empleo | **No existen** | — |
| Oferta de fichaje entre equipos | **No existe.** Solo hay «buyouts» automáticos de la IA, sin compensación | `Career/TransferMarket.cs` |
| Categoría / campeonato | `ChampionshipCategory` y `CategoryRules`, que incluye `EconomyRules`; `ChampionshipState` | `Data/CategoryDefinitions.cs`, `WorldState.cs` |
| Presupuesto / libro financiero | Un solo número por equipo (`Team.Budget`). **No hay libro de movimientos** | `Economy/TeamServices.cs` |
| Objetivos del equipo | `TeamPersonality.Objective` y su evaluación anual (8.2): el germen de los objetivos de la dirección | `World/TeamPersonality.cs`, `Career/TeamAI.cs` |

### 1.2 Economía (`Economy/TeamServices.cs`, `Season/GameController.cs`)

- `FinanceService.ApplyRound` aplica a cada equipo, tras cada evento, una parte proporcional (1/rondas) de lo siguiente:
  - Ingresos: patrocinios y `ParticipationIncomePerSeason`, más los premios del evento y los bonus de los patrocinadores.
  - Gastos: salarios de pilotos y personal, mantenimiento de instalaciones (`60000·nivel²`, una fórmula fija en el código), alquiler de moto si el equipo no es oficial, viaje (`TravelCostPerRound`) y reparación de caídas.
- `EconomyRules` por categoría, en datos: ingreso por participación, viaje, reparación, `EngineCost` (**declarado pero sin usar**), alquiler de moto, premios por posición y salario típico de piloto. **No hay** coste de entrada, licencias, coste de personal base, reserva ni coste de puesta en marcha.
- `SeasonForecast` estima los ingresos y gastos de la temporada, pero **no calcula la tesorería a lo largo del tiempo**.
- Al final de temporada (`SeasonEndProcessor.SponsorsAndReputation`), **los equipos de la IA reciben dinero hasta un suelo** (`TeamAI.OperatingFloor`). El jugador no. Es una asimetría de reglas.
- El presupuesto de los equipos reales viene de `teams/*.json` («2026 entry list, approximate»): son estimaciones, no datos verificados.
- `WorldState.PlayerCash` es una copia de `PlayerTeam.Budget` que se reescribe en dos sitios. Es una **duplicidad**.
- No hay deuda, préstamos, intereses ni insolvencia. Un equipo del jugador puede quedarse en negativo sin ninguna consecuencia definida.

### 1.3 Patrocinadores

- `SponsorTemplate`: nivel (tier), pago por temporada, reputación mínima, pirámides, nacionalidades preferidas, equipos de origen, objetivos y duración.
- `SponsorService`:
  - `EnsureSponsors` asigna patrocinadores al crear el mundo.
  - `Available` lista los que el jugador puede firmar; la firma es **instantánea y sin negociación** (`TeamScreens.cs:293`).
  - `EvaluateRace` y `EndOfSeason` calculan bonus, satisfacción y renovación según la satisfacción.
- Lo que no existe: ofertas pendientes, pagos por adelantado o condicionados, exclusividad por sector, penalizaciones, preferencia de categoría (aprobada en la Fase 8 y aún sin implementar) y competencia entre marcas.
- El pago del patrocinio se reconoce en proporción desde el momento de la firma, sin ninguna condición.

### 1.4 Contratos de pilotos y mercado

- `TransferMarket.Run` (fin de temporada) funciona así:
  1. Aplica los `FutureContract`.
  2. Libera a los pilotos por límite de edad.
  3. Decide las renovaciones (`TeamWantsRenewal` y `RiderWantsRenewal`).
  4. Llena las plazas de mayor a menor prestigio, en cascada, entre las dos pirámides.
  5. Hace «buyouts»: un piloto con contrato puede irse si la categoría nueva tiene ≥10 puntos más de prestigio. **El equipo de origen no recibe compensación.** La IA no puede quitarle pilotos al jugador, pero el jugador tampoco puede pagar un traspaso. Es una asimetría.
- `ContractService.Offer` (el jugador ofrece) compara salario pedido, utilidad de la plaza y prestigio. Los acuerdos firmados se guardan en `FutureContract`.
  - Al renovar, se **sobrescribe el salario del contrato en curso**. Eso cambia el gasto de la temporada actual; probablemente es un error.
  - La oferta solo admite salario y años. No hay cláusulas, opciones ni contraofertas.
- **`WinBonus` y `PodiumBonus` se guardan, pero nunca se pagan.** Es un error real: `ApplyEconomy` no los lee.
- No hay rescisión de pilotos, cláusula de rescisión, opción de renovación ni objetivos contractuales.
- Tampoco hay historial de contratos. Solo existe `SeasonRecord` por temporada, con equipo y salario, pero sin motivo de salida ni traspasos.
- El personal solo tiene indemnización (medio salario, en `StaffMarket.Fire`); no tiene contrato.

### 1.5 Inicio de partida e interfaz

- `BootScreen.cs` muestra **todos los equipos de todas las categorías**, y el jugador elige cualquiera, incluidos los oficiales de MotoGP. Después se llama a `GameSession.NewGame(teamId, seed)`. Esto contradice directamente el §3 del documento.
- Pantallas existentes, en `UI/Screens/TeamScreens.cs` e `InfoScreens.cs`:
  - Equipo: pilotos, personal, contratación y, desde 8.2, el objetivo de la dirección.
  - Moto: proyectos e instalaciones.
  - Finanzas: presupuesto y patrocinios.
  - Mercado: ofertas a pilotos.
  - Clasificaciones, Calendario e Historia.
- El tema visual de la interfaz todavía usa `#0E1116`; el cambio a `#0B1320` está pendiente en la sesión 8.8.

### 1.6 Persistencia

- `SaveSystem` (en `GameController.cs`) serializa todo el `WorldState` a JSON. Usa `CurrentVersion = 1` y **no tiene ningún mecanismo de migración**: solo rechaza versiones futuras.
- `SaveManager` (Unity) escribe de forma atómica en **un único espacio, `autosave`**. Se guarda tras cada evento y al pausar la app.
- Idempotencia actual: cada evento se aplica entero en memoria (`CompleteEvent`) y avanza `NextRound` antes de guardar. Por eso recargar no repite pagos. Pero **no hay ninguna clave de idempotencia**: cualquier pago que se haga fuera de ese flujo atómico (desde una pantalla, un plazo o una cláusula) podría duplicarse.
- La sesión 8.2 ya añadió un patrón de compatibilidad: `TeamAI.EnsureAll` crea las personalidades que faltan en partidas antiguas.

---

## 2. Cobertura del documento

✅ existe · 🟡 parcial · ❌ falta

| Sección | Estado | Nota |
|---|---|---|
| §3–4 Perfil del manager, camino inicial | ❌ | Hoy se elige el equipo directamente |
| §5 Viabilidad por categoría | ❌ | Hay `SeasonForecast` (🟡), pero no hay curva de tesorería, coste de entrada ni veredicto |
| §6 Comparador de categorías | ❌ | |
| §7 Patrocinadores completos | 🟡 | Hay entidad, objetivos, bonus y satisfacción. Faltan ofertas, condiciones de pago, exclusividad, penalizaciones y preferencia de categoría |
| §8 Evolución de los patrocinadores | 🟡 | Renovación por satisfacción. Falta mercado, competencia y mejoras de condiciones |
| §9 Economía consolidada | ❌ | Ni organización ni libro de movimientos; dinero duplicado en `PlayerCash` |
| §10 Expansión multicategoría | ❌ | |
| §11 Cambio de categoría | ❌ | |
| §13 Duración, renovación, opciones | 🟡 | Duración y renovación simple. Faltan opciones y renovaciones automáticas |
| §14 Salario y primas | 🟡 | Hay salario. Las primas se guardan, pero **no se pagan** |
| §15 Objetivos contractuales | ❌ | Los objetivos de equipo (8.2) sirven como base para evaluarlos |
| §16 Rescisión, traspasos, cláusulas | ❌ / 🟡 | Hay «buyouts» de la IA sin compensación |
| §17 Negociación según preferencias | 🟡 | Hay utilidad de la plaza, ambición, lealtad y preferencia GP/SBK. Faltan el equilibrio fijo/primas, las contraofertas y la confianza en el manager |
| §18 Mercado dinámico | ✅ / 🟡 | Hay cascada, promociones, pirámides cruzadas, retiradas, lesiones y filosofía de la IA. Faltan traspasos pagados y renovaciones anticipadas de la IA |
| §19 Historial contractual | 🟡 | Hay `CareerHistory` por temporada. Faltan los contratos y los motivos de salida |
| §20–29 Carrera del manager | ❌ | Nada |
| §27 Fabricantes y programas | 🟡 | `Manufacturer.Programs` y los equipos oficiales/satélite existen. No hay puestos ni ofertas |
| §32–35 Universo, carrera visual, 2026 | ✅ | Fases 1–7 y sesiones 8.1–8.2 |
| §36 Estilo | 🟡 | Pendiente en 8.8 |
| §39 Persistencia | 🟡 | Hay guardado completo, pero sin migraciones, con un solo espacio y sin claves de idempotencia |
| §40 Consistencia | 🟡 | Los cálculos ya están en servicios del núcleo, pero hay asimetrías entre la IA y el jugador (suelo de dinero, traspasos) |

---

## 3. Problemas que provocarían duplicidades o incoherencias

1. **El dinero está en dos sitios** (`Team.Budget` y `WorldState.PlayerCash`). Si se añade una organización sin quitar esto, habría tres.
2. **Asimetría entre la IA y el jugador:**
   - La IA recibe dinero hasta un suelo y el jugador no.
   - Los «buyouts» de la IA son gratis y el jugador no puede hacerlos.
   - La IA no toca a los pilotos del jugador.

   Esto incumple el §18 («la IA no debe ignorar las reglas que se aplican al jugador»).
3. **Primas guardadas que nunca se pagan.** Cualquier sistema nuevo de primas tiene que sustituir a `WinBonus`/`PodiumBonus`, no sumarse a ellos.
4. **Cálculos de dinero dispersos:**
   - En `FinanceService.ApplyRound` (patrocinio, salarios, alquiler, viaje y caídas).
   - En `StaffMarket.Fire` (indemnización).
   - En `UpgradeFacility`, `DevelopmentService.Start` (coste de proyecto) y `SponsorsAndReputation`.

   Cada uno modifica `team.Budget` directamente. Sin un punto de entrada único es imposible garantizar que no haya cobros dobles ni tener un libro de movimientos.
5. **Tres sistemas de objetivos parecidos:** los objetivos de patrocinador (`SponsorObjective`), el objetivo del equipo (`TeamPersonality.Objective`) y los que pide el documento (cláusulas de piloto, objetivos del manager). Si cada uno evalúa sus condiciones por su cuenta, darán resultados distintos para la misma temporada.
6. **Renovar sobrescribe el contrato en curso** (`ContractService.Offer`). Con cláusulas y opciones eso es incorrecto: hace falta un contrato nuevo que empiece la temporada siguiente.
7. **No hay migración de partidas guardadas.** Los cambios de esta fase alteran la estructura (dinero, contratos), así que hace falta un mecanismo de versión antes de tocar nada.
8. **El inicio de partida permite elegir cualquier equipo.** Hay que sustituirlo, no añadir otra opción al lado.
9. **Las parrillas son cerradas.** `GridSize` está en los datos (MotoGP: 22 pilotos, 11 equipos). Fundar un equipo en una categoría con parrilla fija requiere una regla de entrada, porque no se puede añadir un equipo 12 sin más.
10. **Los datos económicos reales no son públicos** (cuotas de entrada, presupuestos). Todo lo que se añada en `EconomyRules` debe marcarse como estimado, igual que hoy hace `dataConfidence`.
11. **Costes fijos en el código:** el mantenimiento de instalaciones (`60000·nivel²`) y el coste de mejora (`400000·(nivel+1)²`) no dependen de la categoría. Contradice que la economía venga de los datos.

---

## 4. Propuesta de modelo de dominio

El criterio es reutilizar lo que ya existe y añadir solo lo imprescindible. Los nombres siguen el estilo del proyecto.

### 4.1 Entidades nuevas o ampliadas

| Entidad | Propuesta | Responsabilidad |
|---|---|---|
| `Organisation` (nueva) | `Id, Name, OwnerKind (Private/Manufacturer/Player), OwnerManagerId, Cash, Loans, TeamIds, Relations` | Propietaria de los equipos y **única dueña del dinero**: tesorería consolidada, deuda, compra y venta de equipos |
| `Team` (ampliada) | Añade `OrganisationId`, `ManagerId` y `OperatingBudget` (asignación de la temporada, **no es dinero**) | Compite, tiene contratos, objetivos y resultados. Su gasto se mide contra la asignación |
| `Manufacturer` | Sin cambios de fondo. Sus equipos oficiales pertenecen a una `Organisation` con `OwnerKind=Manufacturer` | Fabrica y desarrolla. **No es dueño** del dinero del equipo |
| `ManagerProfile` (nueva) | Nombre, nacionalidad, antecedentes, habilidades (Negociación, Gestión deportiva, Finanzas, Relaciones, Comercial), `Reputation`, `PersonalWealth`, `Job` (contrato laboral), `Offers`, `History`, `Relations` (organización → valor) | El jugador **y** los managers de la IA (versión ligera: mismos campos y generación simple) |
| `ManagerContract` (nueva) | Organización y equipo, rol (TeamManager / ProgramDirector), duración, salario, primas, autonomía, presupuesto asignado, objetivos deportivos y financieros, indemnización, preaviso y confianza de la dirección | Empleo del manager. Su salario se paga a `PersonalWealth`, separado de la caja de la organización |
| `Contract` de piloto (ampliado) | Añade `Id`, `SigningBonus`, `Bonuses[]`, `Clauses[]` y `Status`. Se **elimina** `WinBonus`/`PodiumBonus`, migrándolos a `Bonuses` | Acuerdo con efectos verificables |
| `ContractRecord` (nuevo, dentro de `Rider`) | Equipo, fechas, salario, motivo de salida (Expired, Renewed, Released, ReleaseClause, Transfer, Retired) y traspaso pagado | Historial contractual |
| `TransferOffer` (nueva) | Piloto, equipo origen y destino, cantidad, estado y fecha límite | Negociación entre equipos (jugador ↔ IA y IA ↔ IA) |
| `SponsorOffer` / `SponsorDeal` (ampliada) | La oferta es pendiente y no cuenta como dinero. El acuerdo añade calendario de pagos (adelanto, por ronda, condicionado), condiciones, exclusividad por sector y penalizaciones | Lo pedido en el §7 |
| `JobOffer` (nueva) | Contenido del §25 y fecha límite | Ofertas de empleo para el manager |
| `LedgerEntry` (nuevo) | `Date, Season, OrganisationId, TeamId, Kind, Amount, SourceId, Key` | Libro de movimientos. `Key` es única e impide aplicar dos veces lo mismo |

### 4.2 Piezas compartidas para evitar duplicidades

- **`Condition` y su evaluador:** `{Metric (Wins, Podiums, Points, ChampionshipPosition, BeatTeammate, Poles, FastestLaps, StayInCategory…), Comparison, Threshold, Period (Race / Season / Contract)}`. Un único evaluador lee `SeasonRecord` y `ChampionshipState`. Lo usarán los objetivos de patrocinador (sustituye `SponsorObjectiveType`), las cláusulas y primas de piloto, los objetivos del manager y los objetivos de equipo de 8.2.
- **`FinanceService.Post(world, entry)`** es el único punto que mueve dinero. Comprueba `Key`, actualiza `Organisation.Cash` y anota el movimiento. Todo lo que hoy hace `team.Budget -= …` pasa por aquí: la IA y el jugador por el mismo camino.
- **`CashflowForecast`**: recorre el calendario real (fechas de las rondas) con los compromisos derivados de los contratos vigentes. **No se guarda**: se calcula, así no hay datos duplicados. Separa lo confirmado de lo probable.

### 4.3 Dónde se hace cada operación

| Organización | Equipo |
|---|---|
| Tesorería, préstamos, intereses | Asignación de presupuesto y gasto contra ella |
| Comprar, vender, abrir o cerrar un equipo; cambio de categoría | Contratos de pilotos y personal, primas |
| Recibir el dinero de todos los equipos (los cobros van a la caja de la organización con `TeamId` para el informe) | Patrocinios del equipo (con exclusividad comprobada a nivel de organización) |
| Contratar al manager de cada equipo | Objetivos deportivos y resultados |
| Inyección del propietario (sustituye el suelo de dinero actual de la IA; queda anotada como movimiento) | Desarrollo e instalaciones |

---

## 5. Reglas del dominio propuestas (para validar antes de programar)

1. **Momento de cada pago.**
   - Los movimientos de cada evento se anotan en `CompleteEvent` con la clave `ronda|categoría|tipo|origen`. Incluyen premios, primas por carrera de los pilotos, cuotas por ronda de patrocinio, la parte de salarios y el viaje.
   - Los de final de temporada se anotan en `SeasonEndProcessor` **antes del mercado**, con la clave `temporada|tipo|origen`. Incluyen los bonus de campeonato, los objetivos de temporada de los patrocinadores y la evaluación de cláusulas.
   - Las acciones desde una pantalla (firmas, rescisiones, traspasos) usan la clave `acción|id`.
2. **Cobros condicionados.** Una oferta de patrocinio no cuenta como dinero. Un pago condicionado (por ejemplo, «si entras en Moto2») se convierte en movimiento solo cuando la condición se cumple en la fecha en que se comprueba.
3. **Orden de evaluación al final de temporada.**
   1. Resultados finales.
   2. Primas de temporada.
   3. Cláusulas de rendimiento: renovaciones automáticas y extensiones.
   4. Opciones con plazo: equipo antes que piloto.
   5. Cláusulas de salida.
   6. Mercado.

   Si se cumplen varias condiciones en el mismo paso, se aplica la de mayor prioridad declarada en el contrato y las demás quedan anotadas como no aplicables. Nunca se paga dos veces el mismo resultado: el bonus por posición final y el bonus por puntos se suman solo si el contrato los declara acumulables.
4. **Fichar a un piloto con contrato.** Hay tres vías: esperar a que venza, pagar su cláusula de rescisión (si la tiene y se cumple su condición) o hacer una `TransferOffer` aceptada por su equipo. Las mismas tres vías valen para la IA, y los «buyouts» gratuitos actuales desaparecen.
5. **Rescisión unilateral.** Cuesta lo que fije el contrato. Por defecto, el salario pendiente de la temporada en curso más el X % de las siguientes. Se anota como movimiento del equipo que rescinde.
6. **Renovación mientras corre el contrato.** Se crea un contrato **nuevo** que empieza en `EndSeason+1`. El vigente no se modifica.
7. **Viabilidad de una categoría:**
   - Se simula la caja por fechas del calendario con: coste de puesta en marcha, gastos de operación, compromisos firmados e ingresos **confirmados**.
   - **Viable:** la caja mínima no baja de la reserva de seguridad (un parámetro en datos, por ejemplo el 15 % del coste operativo).
   - **Arriesgada:** solo se mantiene por encima de la reserva si se cuentan los ingresos probables (valor esperado de bonus y premios según el nivel esperado).
   - **Inviable:** no aguanta ni con los ingresos probables. Se indica qué falta.
8. **Manager empleado.** Gestiona el `OperatingBudget` del equipo según su nivel de autonomía:
   - Limitada: las firmas por encima de un umbral piden aprobación de la dirección.
   - Estándar.
   - Completa.

   Su salario va a `PersonalWealth`. Ese patrimonio es lo único que puede invertir al fundar una organización, junto con préstamos e inversores.
9. **Evaluación del manager.** La confianza de la dirección (0–100) cambia cada ronda y cada temporada según los objetivos, ponderados por los recursos y el nivel esperado (se reutiliza `TeamAI.ExpectedStrength`). Hay despido si la confianza baja de un umbral **y** se fallan los objetivos del periodo; nunca por una sola carrera.
10. **Insolvencia.** Si la caja es negativa al final de una ronda, entra un estado de alerta con opciones (préstamo, recorte, venta). Si sigue negativa al final de temporada, hay venta forzosa o cierre del equipo. El manager sigue con su carrera y la partida continúa.
11. **Cambio de puesto del manager.** Sigue los ocho pasos del §29: se resuelve el contrato y se aplica la indemnización. La organización propia conserva su dueño y su gestión pasa a un manager de la IA contratado o delegado. La organización que abandona contrata a un manager de la IA del pool.

---

## 6. Cómo se relacionan los tres sistemas

```
ManagerProfile ──(ManagerContract)──► Team ◄──(posee)── Organisation ──► Cash / Ledger / Loans
     │  reputación, patrimonio              │                     ▲
     │                                      ├─ Contract (pilotos) ─┤ salarios, primas, traspasos
     │                                      ├─ SponsorDeal ────────┤ cuotas, bonus, penalizaciones
     └─ funda / es dueño ───────────────────┘                     │
                                Condition + evaluador común ──────┘ (objetivos de patrocinio, cláusulas, dirección)
```

- La reputación del manager sube o baja con los objetivos cumplidos, la mejora de los pilotos (que ya registra `RiderDevelopment`), la gestión económica (resultado del libro de movimientos) y la recuperación de equipos. Usa media móvil, así que una mala temporada no hunde una carrera.
- La reputación del manager modifica la probabilidad y las condiciones de patrocinios, préstamos y ofertas de pilotos, pero **no sustituye** al cálculo de viabilidad (§4).

---

## 7. Plan por fases

Este plan reordena las sesiones pendientes de la Fase 8: absorbe la 8.3 (contratos), la parte de patrocinio de la 8.5 y el guardado de la 8.7. Se mantienen la 8.4 (personal e instalaciones), la 8.6 (historia) y la 8.8 (interfaz), recolocadas.

| Fase | Contenido | Archivos principales | Depende de | Criterios de aceptación |
|---|---|---|---|---|
| **E1. Dominio y dinero único** | `Organisation` (una por equipo real; las de los equipos oficiales pertenecen al fabricante); `LedgerEntry` y `FinanceService.Post` con clave; migrar todos los `Budget -=`; quitar `PlayerCash`; inyección del propietario en lugar del suelo de dinero de la IA; **pagar las primas de los pilotos**; `SaveSystem` v2 con migración v1→v2 | `World/Organisations.cs`, `WorldState.cs`, `Economy/TeamServices.cs`, `GameController.cs`, `SeasonEndProcessor.cs`, `DevelopmentService.cs` | — | La caja de cada organización es igual a la suma de su libro de movimientos; repetir un evento o recargar no duplica nada; una partida v1 carga; 10 temporadas con distribuciones similares a las actuales |
| **E2. Economía y viabilidad** | `EconomyRules` ampliada en datos (entrada o licencia, coste de moto por piloto, personal mínimo, instalaciones por categoría, reserva; `EngineCost` en uso); `CashflowForecast`; `ViabilityService`; `Condition` y su evaluador común; patrocinadores con ofertas, calendario de pagos, condiciones, exclusividad y preferencia de categoría | `Data/CategoryDefinitions.cs`, `rules/*.json`, `sponsors.json`, `Economy/*` | E1 | Pruebas: «entrada asequible pero temporada inviable»; «patrocinio condicionado sin ingreso antes de cumplirse»; veredicto Viable / Arriesgada / Inviable reproducible |
| **E3. Inicio de partida y fundación** | Perfil y antecedentes; camino «empleado» (vacantes que te contratarían) o «fundador» (capital, préstamo, ofertas de patrocinio, categorías viables y regla de entrada en la parrilla); creación de la organización y el equipo; pantallas nuevas en lugar de la lista actual de `BootScreen` | `Season/WorldFactory.cs`, `Unity/UI/BootScreen.cs` y pantallas nuevas | E2 | Pruebas de los escenarios «empieza en un equipo existente» y «funda con capital limitado»; no aparece ninguna categoría inviable como seleccionable |
| **E4. Contratos avanzados de pilotos** | Primas, cláusulas, opciones y renovaciones automáticas; `TransferOffer`; rescisión; historial; negociación con contraoferta (fijo frente a primas); `TransferMarket` y `ContractService` usan las mismas reglas (sin «buyouts» gratis) | `World/Rider.cs`, `Career/ContractService.cs`, `TransferMarket.cs`, `SeasonEndProcessor.cs`, pantalla Mercado | E1, E2 (`Condition`) | Escenarios: renovación automática, primas de victoria y podio, traspaso negociado, rescisión con indemnización, recargar sin volver a activar cláusulas |
| **8.4 Personal e instalaciones** | Lo ya aprobado (12 roles, 10 instalaciones), con contratos de personal sobre la misma base y costes por categoría desde los datos | `Enums.cs`, `TeamServices.cs` | E1, E4 | Ya definidos en la 8.4 |
| **E5. Carrera del manager** | `ManagerContract`, confianza de la dirección, `JobOffer`, cambio de puesto, despido, historial, relaciones; pool ligero de managers de la IA y cobertura de vacantes | `World/` (nuevo), `Career/` (nuevo servicio), pantallas de perfil y ofertas | E1, E3 | Escenarios: oferta con contrato vigente; marcharse sin que desaparezca la organización; un dueño contrata a otro manager |
| **E6. Expansión y reestructuración** | Abrir un segundo equipo, cambiar de categoría al final de temporada, vender o cerrar, delegar, salidas para la insolvencia | `Organisation`, servicios nuevos, pantallas | E2, E5 | Escenarios: expansión que agota la caja (se bloquea o se avisa), cambio de categoría con contratos y activos |
| **E7. IA y universo** | Las organizaciones de la IA usan el mismo mercado de patrocinio, traspasos y contratación de managers; ventas y retiradas de equipos y nuevos participantes | `TeamAI.cs`, `TransferMarket.cs`, `SponsorService` | E4–E6 | Una temporada completa en la que la IA respeta las mismas reglas; 20 temporadas sin quiebras masivas ni inflación |
| **8.6 + 8.7 + 8.8** | Historia del mundo; varios espacios de guardado; ajustes y depuración; repaso de la interfaz (`#0B1320`, ES/EN completo) | — | — | Ya definidos |

Prioridad: E1 y E2 son la base de todo, porque sin dinero único y sin el evaluador de condiciones todo lo demás se duplicaría. E3 cambia la primera experiencia de juego. E4 corrige errores reales (las primas sin pagar) y la asimetría con la IA.

**Riesgos:**
- La migración del guardado (E1) se mitiga con pruebas de carga de una partida v1 real.
- Cambios en el equilibrio económico: tras cada fase se ejecutan las tiradas de 10 o 20 temporadas del SimRunner y se comparan.
- Que el libro de movimientos crezca sin límite: se guardan los movimientos de la temporada actual y la anterior, y las anteriores se resumen por concepto.
- Rendimiento en Android: la previsión de caja solo se calcula al abrir una pantalla o en la evaluación de la IA a final de temporada.

---

## 8. Decisiones necesarias antes de programar

1. **Dónde vive el dinero.** *Recomendado:* la `Organisation` tiene la caja y el equipo solo una asignación de presupuesto (sin dinero propio). La alternativa es mantener la caja por equipo y añadir solo un «grupo». Es más simple, pero complica la tesorería consolidada.
2. **Entrada en categorías con parrilla cerrada** (MotoGP, Moto2 y Moto3 tienen plazas asignadas). *Recomendado:* una regla en datos por categoría, `EntryModel`:
   - `SlotPurchase`: comprar la plaza de un equipo existente que la vende o se retira, con precio estimado.
   - `Open`: inscripción con cuota hasta `MaxEntries`; aplicable a WorldSBK y WorldSSP.
   - La Rookies Cup no se puede fundar, porque la organiza Red Bull.
3. **Managers de la IA.** *Recomendado:* un pool ligero de perfiles generados, necesario para las vacantes, la delegación y la competencia en el mercado laboral. La alternativa es una «calidad de gestión» abstracta por equipo, más barata pero con menos historia.
4. **Préstamos.** *Recomendado:* sí, simples (importe, interés fijo y plazo), con un límite según la reputación y los activos. No incluir inversores complejos ni acciones.
5. **Granularidad de la tesorería.** *Recomendado:* por evento más inicio y fin de temporada, como ahora. Sin simulación mensual.
6. **Quiebra de la organización del jugador.** *Recomendado:* venta o cierre forzoso; la partida continúa con el manager sin empleo o buscando trabajo. No habría «game over».
7. **Datos económicos estimados.** Las cuotas de entrada y precios de plaza no son públicos. *Recomendado:* usar valores estimados, marcados como `estimated` en los JSON y documentados.
8. **Compatibilidad de partidas.** *Recomendado:* migrar v1→v2. La partida antigua pasa a «manager empleado del equipo que manejaba», con un perfil por defecto. La alternativa, ya que el juego no está publicado, es no admitir partidas v1.
9. **Autonomía del manager.** *Recomendado:* tres niveles (limitada, estándar, completa) con umbrales en los datos.
10. **Plazos de negociación.** *Recomendado:* las ofertas (patrocinio, traspaso, empleo) caducan tras N eventos del calendario. No usar días reales.

---

## 9. Respuestas del usuario (primera ronda)

| # | Decisión | Respuesta |
|---|---|---|
| 1 | Organización como dueña del dinero | Pendiente: se pidió una aclaración (ver respuesta en el chat) |
| 2 | Entrada en categorías | **Rookies Cup:** se puede ser manager dentro del equipo único del organizador; se gestionan piloto(s), entrenamientos y piezas, pero no se funda. **Equipo propio:** cualquier otra categoría, según el presupuesto (en parrillas cerradas, la plaza se compra y su coste va en el presupuesto de entrada) |
| 3 | Managers de la IA | Sí |
| 4 | Préstamos simples | Sí |
| 5 | Tesorería por evento y por temporada | Sí |
| 6 | Quiebra: venta o cierre, la partida continúa | Sí, con **impacto fuerte en la reputación** del manager |
| 7 | Datos estimados y marcados | Sí |
| 8 | Migración de partidas | **No hace falta:** no existen partidas guardadas. Se mantiene el número de versión y las claves de idempotencia |
| 9 | Niveles de autonomía | Pendiente: se pidió una aclaración |
| 10 | Caducidad de ofertas por eventos | Sí |

**Nueva regla: no hay ventana de traspasos.** En cualquier momento de la temporada:

- Se puede firmar un contrato para la temporada siguiente (precontrato).
- Se puede cambiar de equipo de inmediato, rompiendo el contrato con las vías del §5.4 (cláusula, acuerdo entre equipos o rescisión).

Consecuencias para el plan:

- El mercado deja de ejecutarse solo al final de temporada. La IA lo evalúa **tras cada evento**: renovaciones anticipadas, precontratos, sustituciones a mitad de temporada (lesión, despido, fichaje) y traspasos inmediatos con compensación. La cascada de fin de temporada se queda solo para cubrir las plazas que sigan vacías.
- Los cambios a mitad de temporada mueven los puntos del piloto con él (puntos de piloto) y los dejan en el equipo (puntos de equipo). Las inscripciones del siguiente evento se reconstruyen.
- Hace falta un límite por reglamento en los datos (por ejemplo, número de sustituciones por temporada), si la categoría lo tiene.

## 10. Respuestas del usuario (segunda ronda)

- **Organización:** aprobada. La organización tiene el dinero; los equipos solo tienen un presupuesto asignado.
- **Autonomía:** se incluyen los tres niveles (limitada, estándar, completa).
- **Rookies Cup:** queda por concretar si se gestiona a uno o varios pilotos y cómo se cobra. No bloquea E1–E2.
- **Nuevo sistema 9:** merchandising. Abajo, la auditoría y el plan.

---

## 11. Merchandising (Sistema 9): auditoría y plan

### 11.1 Hechos observados

- **No existe** nada de inventario, productos, tienda, afición ni ingresos comerciales. Las coincidencias de «production» en el código son de las motos de producción (Superbike), no tienen relación.
- Lo que se puede reutilizar:
  - `Team.Reputation` y `Rider.Reputation`; esta última se actualiza con los resultados en `RiderDevelopment`.
  - `Manufacturer.Reputation`.
  - `ChampionshipCategory.Prestige`.
  - Los resultados de cada evento, en `GameController.ApplyEconomy`.
  - Los títulos, en `WorldState.Champions`.
  - `SponsorDeal`, para productos con marca de un patrocinador.
  - El libro de movimientos que se crea en E1.
- No hay tamaño de afición ni popularidad del piloto como valores separados de la reputación.

### 11.2 Modelo propuesto

**Datos** (`Resources/Data/merchandise/products.json`): plantillas de producto. Añadir productos nuevos es solo cuestión de datos. Cada plantilla tiene:

- `Id` y `Type`: camiseta, sudadera, gorra, chaqueta, réplica de mono, réplica de casco, miniatura, modelo a escala, llavero, mochila, taza, póster, producto firmado, edición de campeonato o conmemorativa.
- `Identity`: Team, Manufacturer o Rider.
- `Segment`: Casual, Fan o Collector. Cada segmento tiene su propia sensibilidad al precio.
- `BaseUnitCost`, `ReferencePrice`, `PriceRange` (precio mínimo y máximo permitidos), `Appeal`, `LimitedUnits` (opcional).
- `Trigger` para ediciones especiales: victoria, título, fichaje estrella, presentación de moto o aniversario.
- `MinOrder` y el descuento por volumen.

**Estado:**

- `Team.Fans`: tamaño de la afición. Evoluciona despacio con los resultados, la categoría y la reputación, y tiene un techo por categoría.
- `Team.Hype`: interés temporal. Sube con victorias, títulos o un fichaje destacado y se disipa evento a evento.
- `Rider.Popularity`: se calcula a partir de la reputación, los resultados recientes y la trayectoria. Así no se duplica la reputación.
- `Team.Shop`: lista de `MerchItem`, cada uno con:
  - `TemplateId` y `RiderId` (si es un producto de piloto).
  - `Price`, `Status` (activo o retirado).
  - `Stock` y `StockCost` (coste medio ponderado).
  - Unidades vendidas en la temporada y en total, ingresos, coste de lo vendido y ventas perdidas.
  - Las ventas por evento de la temporada, para la gráfica de evolución.

**Demanda**, calculada por producto en cada evento:

```
Demanda = Audiencia(categoría) · Saturación(afición) · Atractivo · FactorPiloto · (1 + Hype) · Elasticidad(precio / precio de referencia)
```

- Saturación = afición / (afición + K). Crece con la fama, pero con rendimiento decreciente.
- Elasticidad = (precio / referencia)^(−e). El exponente e depende del segmento: los coleccionistas son menos sensibles al precio. Además hay un límite superior, para que un precio de saldo no dé ventas ilimitadas.
- Ventas = mínimo(existencias, demanda × ruido determinista). Lo que falta se registra como venta perdida.

**Contabilidad**, usando el libro de E1, que distingue los movimientos de caja de los que solo cuentan para pérdidas y ganancias:

| Operación | Caja | Pérdidas y ganancias |
|---|---|---|
| Pedido de existencias | − coste total | (ninguno: aumenta el valor del inventario) |
| Venta | + ingresos | + ingresos, − coste de lo vendido (calculado con el coste medio) |
| Gastos comerciales (operar la tienda, distribución por unidad) | − | − |
| Liquidación con descuento | + ingresos | Pérdida si el precio es menor que el coste |
| Fin de temporada | — | El inventario sigue siendo un activo con su coste; no desaparece ni se convierte en ingreso |

- Todo el dinero va a la caja de la **organización**, anotado con el `TeamId` del equipo. Los informes se ven por equipo y consolidados.
- Los productos de fabricante en equipos oficiales pagan un *royalty* al fabricante, que es un coste, no un ingreso del equipo.
- Los productos de piloto respetan los derechos de imagen de su contrato: un porcentaje de los ingresos para el piloto. Si el contrato no lo permite, el producto no está disponible. Los contratos del snapshot 2026 llevan una cláusula estándar.
- La previsión de ingresos por merchandising nunca cuenta como ingreso confirmado en la viabilidad (E2). Solo cuenta como probable.

**IA:** los equipos de la IA usan el mismo modelo, con una política automática: precio de referencia y reposición según la demanda prevista. Así no hay asimetría con el jugador y su dinero sigue siendo coherente.

### 11.3 Fases

| Fase | Contenido | Depende de | Aceptación |
|---|---|---|---|
| **M1. Tienda básica** (después de E2) | Catálogo en datos, precios con límites, pedidos de existencias con previsión (coste, cobertura, caja restante, riesgo de exceso), demanda, ventas, ventas perdidas, inventario valorado, informes por equipo y consolidados, política de la IA, `Fans`/`Hype`/`Popularity` | E1 (libro con caja y pérdidas y ganancias), E2 (previsión) | Escenarios del §57: equipo desconocido, equipo popular, precio excesivo, precio por debajo del coste, victoria que dispara el interés, falta y exceso de existencias, pedido que deja poca liquidez, cambio de precio que cambia la previsión; recargar no duplica nada |
| **M2. Comercial avanzado** (después de E4/E5) | Productos de piloto con derechos de imagen, ediciones especiales ligadas a hechos reales de la partida, campañas con coste y previsión, productos con patrocinador, producción compartida entre equipos de una organización (descuento por volumen sin duplicar costes) | E4 (cláusulas), M1 | Piloto popular con productos propios; organización con varios equipos; campaña no rentable posible |

### 11.4 Impacto en E1

El libro de movimientos de E1 incluye desde el principio dos indicadores por movimiento: si afecta a la caja y si afecta a pérdidas y ganancias. Así la inversión en existencias y el coste de lo vendido entran en el libro sin rehacerlo después.

### 11.5 Orden general actualizado

**E1 → E2 → M1 → E3 → E4 → 8.4 → E5 → M2 → E6 → E7 → 8.6–8.8**

## 12. Rookies Cup (respuesta)

En la Red Bull Rookies Cup el jugador solo puede ser **manager empleado** del equipo único del organizador. Cobra un
sueldo del organizador, que va a su patrimonio personal y puede invertir después en fundar su propio equipo. No se
puede fundar un equipo en esa copa.
