# ClubGear roadmap

Audience: Maintainers deciding what to build next across the core and plugin repositories
Scope: Prioritised programme view derived from the canonical backlog; no item is defined here
Last-Validated: 2026-10-02

This is the single graphical roadmap for ClubGear. Item status, scope and ownership remain
canonical in [`core-platform.md`](core-platform.md), [`core-capabilities.md`](core-capabilities.md)
and [`plugins.md`](plugins.md). Dependency detail remains in [`domains.md`](domains.md). This page
selects a practical order from those dependencies.

## Reading the roadmap

- A solid arrow is a required dependency. A dotted arrow marks a useful integration, not a release
  prerequisite.
- Nodes sharing a phase may run in parallel only when they own different repositories or do not
  overlap files. Every implementation run still owns exactly one repository.
- `P0/P1/P2/P3` are backlog priorities, not estimates. No dates are implied.
- Delivered items appear only where they explain why current work is ready.

## Programme roadmap

```mermaid
flowchart LR
  classDef done fill:#d8f3dc,stroke:#2d6a4f,color:#1b4332
  classDef now fill:#ffe8a1,stroke:#b7791f,color:#4a2c00,stroke-width:2px
  classDef core fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef plugin fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef decision fill:#fee2e2,stroke:#dc2626,color:#450a0a
  classDef later fill:#f3f4f6,stroke:#6b7280,color:#111827

  subgraph P0["Phase 0 — restore a releasable plugin baseline"]
    C18["CORE-18 delivered"]:::done
    CAR02["CAR-02 release<br/>plugin-carinfo"]:::now
    FIN02["FIN-02<br/>plugin-finance"]:::now
    SB02["SB-02<br/>plugin-servicebook"]:::now
    INV02["INV-02<br/>plugin-inventar"]:::now
    IAM02["IAM-02<br/>plugin-iamoidc"]:::now
    C18 --> CAR02
    C18 --> FIN02
    C18 --> SB02
    C18 --> INV02
    C18 --> IAM02
  end

  subgraph P1["Phase 1 — finish the platform spine"]
    C13["CORE-13 provider seam"]:::core
    C14["CORE-14 selection"]:::core
    C15["CORE-15 settings + secrets"]:::core
    C19["CORE-19 + CORE-24<br/>manifest guards"]:::core
    C27["CORE-27 authoring + template"]:::core
    C13 --> C14
    C13 --> C19
    C19 --> C27
  end

  subgraph P2["Phase 2 — foundational core capabilities"]
    C01["CORE-01 organisation"]:::core
    C05["CORE-05 templates/events"]:::core
    C02["CORE-02 file storage"]:::core
    C25["CORE-25 first-run installer"]:::core
    C03["CORE-03 plugin surfaces"]:::core
    C14 --> C02
  end

  subgraph DG["Decision gates"]
    D4{"D4 IdP precedence<br/>per concern"}:::decision
    D6{"D6 shared settings +<br/>calendar write policy"}:::decision
    SG{"Security report refresh<br/>and triage"}:::decision
  end

  subgraph P3["Phase 3 — first user-value streams"]
    INV01["INV-01 inventory<br/>plugin-inventar"]:::plugin
    FIN01["FIN-01 import, reports,<br/>budget, roles"]:::plugin
    NEWS01["NEWS-01 newsletter"]:::plugin
    CAL["CORE-07 → CALDAV-01"]:::plugin
    CHAN["CHAN-01 Matrix"]:::plugin
    STOR["STOR-01 S3"]:::plugin
    BDAY["BDAY-01 greetings"]:::plugin
    RADIUS["CORE-26 radius search"]:::core
    C03 --> INV01
    C01 --> INV01
    C05 --> NEWS01
    C01 --> CAL
    C13 --> CHAN
    C15 --> CHAN
    C02 --> STOR
    C14 --> STOR
    C01 --> RADIUS
    C05 --> BDAY
  end



  subgraph P4["Phase 4 — workflows and migration-complete domains"]
    C06["CORE-06 workflow engine"]:::core
    RENT["RENT-01 rental"]:::plugin
    EDIT["EDIT-01 editorial"]:::plugin
    POST["POST-01 postal"]:::plugin
    ORG["ORGVIEW-01"]:::plugin
    NC["NC-DOC-01"]:::plugin
    C03 --> C06
    C06 --> RENT
    INV01 --> RENT
    C01 --> EDIT
    C05 --> EDIT
    C05 --> POST
    C01 --> ORG
    C13 --> NC
    C15 --> NC
  end

  subgraph P5["Phase 5 — optional breadth and hardening"]
    HARD["CORE-20/21<br/>widgets, health, lifecycle"]:::later
    TAIL["remaining P2/P3 plugins<br/>and core polish"]:::later
    HARD --> TAIL
  end

  CAR02 -. independent repositories .-> C13
  FIN02 -. independent repositories .-> C13
  SB02 -. independent repositories .-> C13
  INV02 -. independent repositories .-> C13
  IAM02 -. independent repositories .-> C13
  D4 --> C01
  D6 --> C15
  SG --> C25
  FIN01 -. finance events/contracts .-> RENT
```

## Execution order

### Now — restore compatibility and close release residue

Run the five plugin repositories independently. CarInfo needs only the deliberate `1.0.10` release
and updated core gitlink; Finance, ServiceBook, Inventar and IamOidc still need contract `1.13.0`
adoption and their own releases. These runs may proceed in parallel because they share no writer.
Do not combine them into a core commit.

### Next — platform spine

1. **CORE-13 + CORE-14** in one core run: provider consumption, collision handling and explicit
   provider selection.
2. **CORE-15** may run in parallel after its D6 ownership/read/uninstall decisions are recorded.
3. **CORE-19 + CORE-24** after CORE-13: manifest category/kind and consistency checks.
4. **CORE-27** after CORE-19: restructure the authoring guidance and publish a rule-compliant plugin
   project template only after the manifest vocabulary is settled.

This phase is the highest leverage: it unlocks channel, storage, document and API-key plugins and
prevents each integration from inventing configuration and precedence rules.

### Then — capability foundations in parallel

- **CORE-01** after the D4 authority decision; it has the widest fan-out.
- **CORE-05** immediately; it unlocks newsletter, postal and birthday automation.
- **CORE-02** after CORE-14.
- **CORE-03** can proceed incrementally, tied to named consumers.
- **CORE-25** can proceed independently, but only after a fresh security triage establishes the
  bootstrap threat model. It combines the old twelve-slice install plan into one canonical item.

### Product streams

Choose at least one stream after the foundations rather than starting every plugin at once:

| Stream | Minimum chain | Repository sequence |
|---|---|---|
| Inventory and rental | CORE-01 + CORE-02 + CORE-03 → INV-01 → CORE-06 → RENT-01 | core → `plugin-inventar` → core → `plugin-rental` |
| Finance completion | FIN-02 → FIN-01 | `plugin-finance` only; split imports, reporting/budget and roles into bounded runs |
| Communication | CORE-05 + CORE-13/15 → NEWS-01 / CHAN-01 / POST-01 / BDAY-01 | core, then one plugin repository per run |
| Organisation and calendar | D4 → CORE-01 → CORE-07 → CALDAV-01; CORE-26 and ORGVIEW-01 branch after CORE-01 | core → `plugin-caldav`; side branches may run in parallel |
| Files and documents | CORE-14 → CORE-02 → STOR-01; CORE-13/15 → NC-DOC-01 | core, then independent provider repositories |


## Decisions and gates

Before work reaches the affected node, record these decisions in the architecture decision record:

1. **Identity authority (D4):** precedence per member field, permission and organisation unit;
   rejected local edits; local-only fallback.
2. **Shared settings (D6):** ownership, read scope, encryption, uninstall semantics and whether
   managed calendars are member-readable or writable.
3. **Installer security:** bootstrap authentication, post-install route behaviour, registration
   before installation, concurrency/transaction boundary and audit record.
4. **Finance compatibility:** whether `kassenwart` remains a permanent persisted permission alias and
   when legacy command aliases may be removed.
5. **Security evidence:** the 2026-08 scan is historical. Re-run supported scanners against HEAD,
   classify only reproducible findings, and never schedule raw scanner output as fact.

## Intentionally deferred

P2/P3 breadth—dashboard widgets, plugin health/data lifecycle, API-key extraction, Telegram,
WhatsApp, SMS, Listmonk, Traccar, CMS, document compendium, decision trees, CarInfo field residue,
ServiceBook parity and IAM mapping—stays visible in the canonical item files but does not interrupt
the platform spine or the first selected product stream.
