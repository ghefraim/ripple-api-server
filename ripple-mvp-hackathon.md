# Ripple — Hackathon MVP Build Reference

**A-CDM intelligence for airports that can't afford A-CDM.**

Real-time disruption cascade analysis and action planning for mid-size airports.

---

## 1. Scope

### What we're building in 48 hours

A web application where an airport duty manager can:

1. View today's flight schedule on a visual timeline
2. Enter a disruption (delay, cancellation, gate change)
3. Instantly see every downstream impact — gate conflicts, crew gaps, cascading delays
4. Receive a prioritized, LLM-generated action plan with reasoning
5. Push actionable alerts to staff via a mobile app

An admin can:

6. Create, edit, and manage operational rules using natural language (LLM-parsed) or a visual editor

### Explicit non-goals (hackathon)

- Automatic disruption detection (manual entry only)
- Real AODB integration (free flight API for demo, abstraction layer for future)
- Crew rostering optimization (we flag gaps, we don't solve scheduling)
- IoT sensor integration
- Historical analytics or reporting
- Multi-airport demo (architecture supports it via multi-tenancy, but we demo one airport)
- User management / role CRUD (hardcoded roles for demo)

---

## 2. Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend (web) | Vite + React + TypeScript |
| Frontend (mobile) | React Native / Expo |
| Backend | .NET (existing multi-tenant boilerplate) |
| Database | PostgreSQL (or whatever boilerplate uses) |
| LLM | Gemini Flash 2.5 |
| Flight data | Free flight API (AviationStack / AeroDataBox / similar) |
| Real-time | SignalR (comes with .NET) |
| Auth | Boilerplate auth (roles: duty_manager, staff, admin) |

---

## 3. Architecture

### System overview

```
┌─────────────────────────────────────────────────────┐
│                    FRONTEND                          │
│  ┌──────────────────┐    ┌───────────────────────┐  │
│  │   Web Dashboard   │    │  Mobile App (Expo)    │  │
│  │  - Timeline view  │    │  - Notification feed  │  │
│  │  - Cascade panel  │    │  - Acknowledge action │  │
│  │  - Action plan    │    │  - Flight schedule    │  │
│  │  - Rule config    │    └───────────────────────┘  │
│  └──────────────────┘                                │
└────────────────┬────────────────────────┬────────────┘
                 │ REST + SignalR          │ Push notifications
┌────────────────▼────────────────────────▼────────────┐
│                   BACKEND (.NET)                      │
│                                                       │
│  ┌─────────────────┐  ┌──────────────────────────┐   │
│  │  API Controllers │  │  SignalR Hub             │   │
│  └────────┬────────┘  └──────────┬───────────────┘   │
│           │                      │                    │
│  ┌────────▼──────────────────────▼───────────────┐   │
│  │           Disruption Service                   │   │
│  │  1. Receive disruption event                   │   │
│  │  2. Run cascade engine                         │   │
│  │  3. Call LLM for action plan                   │   │
│  │  4. Broadcast via SignalR                      │   │
│  │  5. Push to mobile                             │   │
│  └────────┬──────────────────────┬───────────────┘   │
│           │                      │                    │
│  ┌────────▼────────┐   ┌────────▼────────────────┐   │
│  │  Cascade Engine  │   │  LLM Service            │   │
│  │  (Rule executor) │   │  (ILlmProvider)         │   │
│  └────────┬────────┘   │  - Action plan gen       │   │
│           │             │  - Rule parsing          │   │
│  ┌────────▼────────┐   └─────────────────────────┘   │
│  │  Rule Store      │                                 │
│  │  (DB-backed)     │                                 │
│  └─────────────────┘                                  │
│                                                       │
│  ┌─────────────────────────────────────────────────┐ │
│  │  Abstraction Interfaces                          │ │
│  │  - IFlightDataProvider (free API now, AODB later)│ │
│  │  - ILlmProvider (Gemini now, swappable)          │ │
│  │  - INotificationProvider (Expo push now)         │ │
│  └─────────────────────────────────────────────────┘ │
└───────────────────────────┬──────────────────────────┘
                            │
                    ┌───────▼───────┐
                    │  PostgreSQL    │
                    │  (per-tenant)  │
                    └───────────────┘
```

### Abstraction interfaces

These exist from day one. Each has one concrete implementation for the hackathon and is designed for swap-in replacements post-hackathon.

**IFlightDataProvider**
- `GetFlightSchedule(airportCode, date)` → list of flights
- `GetFlightStatus(flightNumber)` → current status
- Hackathon impl: free API. Production impl: AODB connector.

**ILlmProvider**
- `GenerateActionPlan(cascadeData)` → prioritized action plan text
- `ParseRule(naturalLanguageInput)` → structured rule JSON
- Hackathon impl: Gemini Flash 2.5. Swappable to GPT, Claude, etc.

**INotificationProvider**
- `SendAlert(staffId, alert)` → push notification
- Hackathon impl: Expo Push Notifications.

---

## 4. Data Model

### Core tables

**Airports** (tenant)
```
id, code (IATA), name, timezone, config_json
```

**Flights**
```
id, airport_id, flight_number, airline,
origin, destination, type (arrival/departure),
scheduled_time, estimated_time, actual_time,
gate_id, status (on_time/delayed/cancelled/diverted),
turnaround_pair_id (links arrival to departure)
```

**Gates**
```
id, airport_id, code (e.g. "A3"), type (domestic/international/both),
size_category (narrow/wide), is_active
```

**GroundCrews**
```
id, airport_id, name, shift_start, shift_end,
assigned_flight_id, status (available/assigned/on_break)
```

**Rules**
```
id, airport_id, name, description,
rule_json (structured schema), is_active,
created_by, created_at
```

**Disruptions**
```
id, airport_id, flight_id, type (delay/cancellation/gate_change),
details_json, reported_by, reported_at
```

**CascadeImpacts**
```
id, disruption_id, affected_flight_id,
impact_type (gate_conflict/crew_gap/downstream_delay/turnaround_breach),
severity (critical/warning/info), details_json
```

**ActionPlans**
```
id, disruption_id, llm_output_text,
actions_json (structured list), generated_at
```

**Notifications**
```
id, action_plan_id, staff_id, message,
status (sent/acknowledged/handled), sent_at
```

### Seed data (Cluj airport)

Pre-load a realistic day at Cluj (CLJ):
- 15-20 flights (mix of arrivals/departures, domestic/international)
- 5-6 gates with type restrictions
- 4-5 ground crews with shift patterns
- 3-4 tight turnarounds that are vulnerable to cascade
- 5-6 pre-seeded rules covering common scenarios

---

## 5. Rule Schema

### Structure

```json
{
  "id": "rule_001",
  "name": "Critical turnaround breach",
  "description": "Flag when turnaround drops below minimum threshold",
  "trigger": {
    "event": "turnaround_time_changed",
    "conditions": [
      {
        "field": "turnaround_minutes",
        "operator": "less_than",
        "value": 35
      }
    ]
  },
  "filters": [
    {
      "field": "flight_type",
      "operator": "equals",
      "value": "international"
    }
  ],
  "actions": [
    {
      "type": "flag_severity",
      "value": "critical"
    },
    {
      "type": "recommend",
      "value": "gate_reassignment"
    }
  ]
}
```

### Supported fields

`turnaround_minutes`, `delay_minutes`, `flight_type`, `gate_type`, `crew_status`, `flight_status`, `time_until_departure`

### Supported operators

`equals`, `not_equals`, `less_than`, `greater_than`, `in`, `not_in`

### Supported actions

`flag_severity` (critical/warning/info), `recommend` (gate_reassignment/crew_reallocation/timeline_adjustment/passenger_notification), `auto_notify` (staff role)

### LLM rule parsing

System prompt constrains Gemini to output ONLY valid JSON matching this schema. Few-shot examples baked into the prompt:

```
User: "If any flight is delayed more than 60 minutes, flag it as critical and notify the duty manager"
→ { trigger: { event: "delay_changed", conditions: [{ field: "delay_minutes", operator: "greater_than", value: 60 }] }, actions: [{ type: "flag_severity", value: "critical" }, { type: "auto_notify", value: "duty_manager" }] }
```

---

## 6. Cascade Engine Logic

When a disruption is entered:

**Step 1: Direct impact**
- Update the disrupted flight's estimated time / status / gate

**Step 2: Gate conflict scan**
- For affected gate, check if any other flight now overlaps (considering turnaround buffer)
- If overlap found → create CascadeImpact (gate_conflict)

**Step 3: Turnaround breach scan**
- If disrupted flight has a turnaround pair, calculate new turnaround time
- If below minimum threshold → create CascadeImpact (turnaround_breach)

**Step 4: Downstream delay propagation**
- If turnaround is breached, the paired departure is delayed
- Recurse: that departure's delay may trigger further cascades at destination (out of scope for MVP — flag but don't recurse beyond one level)

**Step 5: Crew gap scan**
- Check if any crew assigned to affected flights now has a scheduling conflict
- If crew can't make the new time → create CascadeImpact (crew_gap)

**Step 6: Execute user-defined rules**
- Load all active rules from the Rule Store
- For each rule, evaluate trigger conditions against the current disruption context
- If triggered, execute the rule's actions (flag severity, add recommendations)

**Step 7: Generate action plan**
- Collect all CascadeImpacts
- Send to LLM with context (flight schedule, gate availability, crew availability)
- LLM returns prioritized, natural language action plan
- Store and broadcast via SignalR

---

## 7. Screens

### 7.1 Login
- Standard auth from boilerplate
- Roles: admin, duty_manager, staff

### 7.2 Operations Dashboard (duty_manager)
- **Timeline view**: horizontal swimlanes by gate, flights as bars, color-coded by status
- **Disruption panel** (sidebar): list of active disruptions, expandable cascade tree
- **Action plan panel**: LLM-generated recommendations, numbered by priority, with reasoning. Each action has "Acknowledge" / "Execute" buttons
- **"Report Disruption" button**: opens modal (flight selector, disruption type, details)
- **Real-time updates via SignalR**

### 7.3 Flight Schedule (duty_manager, staff)
- Table view: all flights for today
- Columns: flight #, airline, origin/dest, scheduled, estimated, gate, status, crew
- Filterable, sortable
- Status badges color-coded

### 7.4 Rule Configuration (admin)
- **Natural language input**: text field + "Create Rule" button
- **LLM generates** → shows summary card (human-readable) + raw JSON toggle
- **Visual editor**: form-based — dropdowns for field, operator, value. "Add condition" / "Add action" buttons. Pre-filled from LLM output, editable.
- **Rule list**: all active rules with name, summary, status toggle (active/paused), edit/delete

### 7.5 Mobile App (staff)
- **Notification feed**: cards with alert message, severity badge, timestamp
- **Tap to expand**: full detail + "Acknowledged" / "Handled" buttons
- **Quick flight schedule view**: same data as web, simplified layout
- Status syncs back to dashboard via API

---

## 8. Demo Script

**Setting**: Cluj airport, realistic flight schedule loaded. Dashboard on projector. Phone on table.

1. **"Normal day"** — Show the dashboard with a full day of flights, all green. Point out the timeline, gate assignments, crew allocations. "This is Cluj on a Tuesday."

2. **"Something goes wrong"** — Enter a disruption: Wizz Air W6-2901 from London Luton, delayed 55 minutes due to late inbound. Click submit.

3. **"Watch it ripple"** — Dashboard updates live. Timeline shifts. Cascade panel shows: gate A3 conflict with departing W6-2902, turnaround breach (down to 20 minutes), crew gap for ground team Alpha who were supposed to handle both flights. Three impacts, flagged by severity.

4. **"Here's what to do"** — Action plan generates in seconds. "1. Reassign W6-2902 to gate A5 (available, compatible, no conflicts). 2. Extend ground crew Alpha shift by 30 min or reassign crew Beta (currently idle). 3. Update W6-2902 departure estimate to +15 min and notify pax." Each with clear reasoning.

5. **"Your team knows instantly"** — Phone buzzes on table. Show the notification: "Gate A3 conflict — W6-2902 needs reassignment. Suggested: A5." Tap acknowledge. Dashboard updates to show staff received the alert.

6. **"Airport rules, your way"** — Switch to admin view. Type: "If any Wizz Air flight has less than 30 minutes turnaround, flag as critical and notify duty manager immediately." LLM parses it, show the summary card, toggle to see raw JSON, tweak a value in the visual editor. Activate the rule.

7. **"It already works"** — Trigger another smaller disruption. Show the new custom rule firing alongside the built-in cascade logic.

**Closing line**: "One delay. Three conflicts found. Three actions recommended. Staff notified. All before the duty manager finishes their radio call. That's Ripple."

---

## 9. Sprint Plan

Tasks are organized by function (backend, frontend-web, frontend-mobile, integration). Assign based on team strengths on-site. All tasks build on top of the existing Vite + .NET multi-tenant boilerplate.

### Phase 1: Foundation (Hours 0-8)

**Backend**
- [ ] Define data model, create migrations, seed CLJ airport data (gates, crews, 15-20 flights with turnaround pairs)
- [ ] Create IFlightDataProvider interface + free API implementation
- [ ] Create ILlmProvider interface + Gemini Flash implementation
- [ ] Build Rule Store — CRUD endpoints for rules, rule schema validation
- [ ] Build basic flight schedule API endpoints (list, get, update status)

**Frontend Web**
- [ ] Set up dashboard layout shell (timeline area, sidebar, action panel)
- [ ] Build flight schedule table view with status badges
- [ ] Report Disruption modal (flight picker, type selector, details input)

**Frontend Mobile**
- [ ] Expo project init, navigation setup
- [ ] Notification feed screen (static UI first)
- [ ] Push notification setup (Expo Push)

### Phase 2: Core Engine (Hours 8-20)

**Backend**
- [ ] Build cascade engine — Steps 1-5 (gate conflicts, turnaround breaches, downstream delays, crew gaps)
- [ ] Build rule executor — load active rules, evaluate conditions, execute actions
- [ ] Wire disruption entry → cascade engine → store impacts
- [ ] LLM action plan generation — prompt engineering, structured input, natural language output
- [ ] SignalR hub — broadcast cascade results + action plans to connected dashboards
- [ ] Push notification service — send alerts to mobile on cascade events

**Frontend Web**
- [ ] Timeline visualization — gate swimlanes, flight bars, color-coded status, real-time updates via SignalR
- [ ] Cascade panel — disruption list, expandable impact tree, severity badges
- [ ] Action plan panel — numbered recommendations, reasoning text, acknowledge/execute buttons

### Phase 3: Rule Builder + Polish (Hours 20-35)

**Backend**
- [ ] LLM rule parsing — natural language → rule JSON, with few-shot prompt
- [ ] Rule validation — ensure LLM output matches schema before saving
- [ ] Wire rule CRUD to cascade engine (new rules are live immediately)

**Frontend Web**
- [ ] Rule configuration page — natural language input + "Create Rule" button
- [ ] Rule summary card (LLM-generated human-readable summary + raw JSON toggle)
- [ ] Visual rule editor — form with dropdowns for field/operator/value, add condition/action
- [ ] Rule list with active/paused toggle, edit, delete

**Frontend Mobile**
- [ ] Wire to real API — notification feed from backend
- [ ] Tap-to-expand detail view
- [ ] Acknowledge/Handled buttons syncing back to backend
- [ ] Quick flight schedule view

### Phase 4: Demo Prep (Hours 35-48)

- [ ] Seed data tuning — make sure the demo disruption triggers a compelling cascade
- [ ] End-to-end testing of the demo script (run through it 5+ times)
- [ ] LLM prompt tuning — make action plans sound professional and specific
- [ ] Rule parsing reliability — test 10+ natural language inputs, keep the ones that work for demo
- [ ] UI polish — loading states, animations on cascade updates, severity color consistency
- [ ] Mobile push notification reliability check
- [ ] Swagger/OpenAPI docs auto-generated for AODB integration story
- [ ] Prepare backup plan: if rule builder LLM fails on stage, have pre-created rules ready

---

## 10. Interoperability Strategy

Even without real integrations, we demonstrate integration-readiness:

1. **IFlightDataProvider abstraction** — show interface, explain AODB swap
2. **REST API with Swagger docs** — auto-generated, shows other systems can consume Ripple's output
3. **Webhook-ready disruption input** — the same endpoint that the UI calls can be called by external systems
4. **A-CDM milestone naming** — use TOBT, TSAT, ELDT in data model where applicable
5. **Multi-tenant architecture** — each airport is a tenant, isolated data, shared platform

Pitch line: "We're not asking airports to rip and replace. Ripple sits alongside existing systems — plug in your AODB, and it starts working."

---

## 11. Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Cascade engine has bugs | Pre-seed data is hand-crafted to produce known cascades. Test with exact demo scenario early. |
| LLM rule parsing unreliable | Pre-seed 5-6 working rules. Demo rule creation with tested inputs only. |
| LLM action plan is generic | Invest in prompt engineering. Include specific flight numbers, gate codes, crew names in the prompt context. |
| Mobile push notifications fail | Have mobile app open and refresh manually as backup. The web dashboard is the primary demo surface. |
| Free flight API is down/limited | Seed all data locally. API is for realism, not a runtime dependency. |
| Run out of time | Cut order: mobile app → rule visual editor → rule LLM parsing → keep cascade engine + dashboard + pre-seeded rules. |
