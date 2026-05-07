# Taller: Agentes con Azure Foundry
## MCP Server · Human-in-the-Loop · Responses API

> **Guía de diapositivas** — ~60 min · Nivel intermedio  
> Términos técnicos en inglés (Foundry, Responses API, MCP, Human-in-the-Loop) se mantienen como tales.

---

## DIAPOSITIVA 1 — Portada

**Título:** Agentes con Azure Foundry  
**Subtítulo:** MCP Server, Human-in-the-Loop y Responses API  
**Pie:** [nombre del presentador] · [fecha] · [evento]

> **Diagrama recomendado:** Logo de Azure AI Foundry centrado. Fondo degradado azul oscuro. Ningún texto adicional.

---

## DIAPOSITIVA 2 — Agenda

| # | Bloque | Contenido |
|---|--------|-----------|
| 1 | Fundamentos | ¿Qué es Azure Foundry? |
| 2 | Tipos de agente | Prompt Agent vs Hosted Agent |
| 3 | Protocolos | Responses API vs Activity Protocol |
| 4 | Herramientas | MCP Server: conceptos y arquitectura |
| 5 | Control humano | Human-in-the-Loop: dos enfoques |
| 6 | Demo en vivo | Transfer budget con confirmación conversacional |

---

## DIAPOSITIVA 3 — ¿Qué es Azure AI Foundry?

**Plataforma totalmente administrada** para construir, desplegar y escalar agentes de IA.

**Tres componentes clave:**

- **Model**: Razonamiento y lenguaje (GPT-4o, GPT-4.1, Llama, DeepSeek…)
- **Instructions**: Definen objetivos, restricciones y comportamiento del agente
- **Tools**: Acceso a datos y acciones (búsqueda, APIs, MCP servers, funciones…)

**El servicio se encarga de:** hosting, escalado, identidad, observabilidad y seguridad.

> **Diagrama recomendado:** Triángulo con los tres componentes en los vértices. En el centro: "Agente". Debajo: "Azure AI Foundry" como plataforma.

---

## DIAPOSITIVA 4 — Tipos de agente en Foundry

| | **Prompt Agent** | **Workflow Agent** | **Hosted Agent** |
|---|---|---|---|
| **Requiere código** | No | No (YAML opcional) | Sí |
| **Hosting** | Totalmente administrado | Totalmente administrado | Contenedor administrado |
| **Orquestación** | Agente único | Multi-agente, branching | Lógica personalizada |
| **Mejor para** | Prototipos, tareas simples | Automatización multi-paso | Control total, frameworks propios |
| **Se define en** | Portal / API / SDK | Portal visual o YAML | Código + contenedor |

**Foco de este taller:** Prompt Agent + Hosted Agent.

---

## DIAPOSITIVA 5 — Prompt Agent (en detalle)

**Definición:** configuración declarativa — instrucciones + modelo + tools.  
No requiere código. El portal lo orquesta automáticamente.

**Flujo:**
1. Se crea en `ai.azure.com` → Agents → New agent
2. Se asigna: modelo, instrucciones de sistema, tools (MCP, OpenAPI…)
3. Se invoca via Responses API con `agent_reference`
4. El estado de conversación se mantiene con `previous_response_id`

**Ideal para:** prototipos rápidos, tools internas, agentes sin lógica de orquestación personalizada.

> **Diagrama recomendado:** Caja "Portal ai.azure.com" → define → caja "Prompt Agent" (modelo + instrucciones + tools). Flecha desde app backend → Responses API → Prompt Agent.

---

## DIAPOSITIVA 6 — Hosted Agent (en detalle)

**Definición:** agente como contenedor. El desarrollador escribe la orquestación.  
Foundry gestiona el runtime, el escalado y la identidad.

**Flujo:**
1. Se construye con Agent Framework, LangGraph, Semantic Kernel o código propio
2. Se empaqueta como imagen de contenedor y se publica en Azure Container Registry
3. Foundry despliega, asigna identidad Microsoft Entra ID y expone un endpoint dedicado
4. Soporta protocolos: **Responses**, **Invocations**, **Activity** y **A2A**

**Ideal para:** lógica de orquestación compleja, multi-agente, frameworks propios, workloads con estado.

**Aislamiento:** cada sesión corre en un sandbox aislado con filesystem persistente (`$HOME`).

> **Diagrama recomendado:** Imagen Docker → Azure Container Registry → Foundry Runtime → Endpoint dedicado. Flechas mostrando los 4 protocolos saliendo del endpoint.

---

## DIAPOSITIVA 7 — Prompt Agent vs Hosted Agent (resumen visual)

> **Diagrama recomendado (PowerPoint):**  
> Dos columnas con fondo de colores distintos (azul claro / azul oscuro).  
> **Izquierda — Prompt Agent:** ícono de portal, "Sin código", "Orquestación automática", "Minutos para crear"  
> **Derecha — Hosted Agent:** ícono de contenedor Docker, "Código propio", "Orquestación custom", "Control total"  
> Línea divisoria con texto: "¿Cuánto control necesitas?"

**Regla práctica:**
- Empezá con Prompt Agent.
- Pasá a Hosted Agent cuando necesites frameworks propios, payloads custom o lógica de orquestación que el portal no puede expresar.

---

## DIAPOSITIVA 8 — Responses API

**La Responses API es la única opción para MCP tools.**  
(Chat Completions API no soporta MCP tools.)

**Características clave:**

- Stateless: cada llamada es independiente
- Multi-turn via `previous_response_id`
- Soporta `agent_reference` para invocar un Prompt Agent definido en Foundry
- Disponible para: `gpt-4o`, `gpt-4.1`, `o1`, `o3` y modelos de razonamiento
- Solo se pagan los tokens usados en definiciones de tools y llamadas — sin costo adicional por MCP

**Llamada mínima con Prompt Agent:**

```json
POST /openai/v1/responses
{
  "agent": { "type": "agent_reference", "name": "budget-agent" },
  "input": "¿Cuánto presupuesto tiene el Proyecto Beta?",
  "previous_response_id": "resp_abc123"
}
```

---

## DIAPOSITIVA 9 — Responses API vs Activity Protocol

| | **Responses API** (este taller) | **Activity Protocol** (Bot Framework) |
|---|---|---|
| **Estándar** | Compatible con OpenAI | Bot Framework Activity Schema |
| **Estado** | Stateless (`previous_response_id`) | Stateful (threads, actividades, canal) |
| **Transporte** | HTTPS request/response | WebSocket / Direct Line / Bot Connector |
| **MCP tools** | Sí, nativo (`type: "mcp"`) | No directo — via SDK de agente |
| **Multi-canal** | No (API directa) | Sí (Teams, Copilot Studio, Direct Line…) |
| **Complejidad** | Baja — una llamada HTTP por turno | Alta — registro de bot, canales, channel auth |

**¿Cuándo usar cada uno?**

- **Responses API:** la app controla el ciclo de vida. Un HTTP call por turno. Ideal para web/mobile.
- **Activity Protocol:** el agente vive en Teams o Copilot Studio y necesita manejar eventos de ciclo de vida (`conversationUpdate`, `typing`…).

> **Diagrama recomendado:** Dos caminos desde "Usuario". Izquierda: Usuario → App → Responses API → Foundry Agent. Derecha: Usuario → Teams/Copilot Studio → Bot Connector → Bot Framework Agent. MCP Server al fondo, conectado a ambos.

---

## DIAPOSITIVA 10 — MCP: ¿Qué es?

**Model Context Protocol (MCP)** es un estándar abierto que define cómo las aplicaciones exponen herramientas y datos contextuales a los LLMs.

**Beneficios:**
- Integración consistente y escalable de tools externas
- Agnóstico al lenguaje y al cliente (cualquier LLM compatible con MCP puede consumirlo)
- Descubrimiento automático de tools via `tools/list`

**Transportes soportados:**

| Transporte | Protocolo | Mejor para |
|---|---|---|
| **Streamable HTTP** | HTTPS POST/GET | Servicios cloud, producción |
| **SSE** (deprecado) | Server-Sent Events | Clientes legacy |
| **stdio** | stdin/stdout | Herramientas locales, desarrollo |

**En Azure:** se puede hostear en App Service, Azure Functions o Azure Container Apps.

---

## DIAPOSITIVA 11 — MCP Server: arquitectura de este taller

> **Diagrama recomendado (PowerPoint):**
>
> ```
> Usuario (React UI)
>       │  HTTP
>       ▼
> foundry-agent-webapp (ASP.NET Core 10)
>   POST /api/chat  →  previous_response_id
>       │  Responses API
>       ▼
> Azure Foundry (Prompt Agent: "budget-agent")
>   System instructions + MCP tools configurados en el portal
>       │  MCP Streamable HTTP
>       ▼
> McpServer.Presentation (:5230)
>   get_project_balance, get_projects, transfer_budget…
>       │  HTTP
>       ▼
> McpServer.BackendApi (EF Core InMemory)
>   PRJ001, PRJ002, PRJ003 — balances, tareas, usuarios
> ```
>
> Usar cajas con íconos de Azure para cada capa. Resaltar la flecha MCP en color distinto.

**Principios de este diseño:**
- El Prompt Agent vive en Foundry (portal), no en el código
- El backend es un **relay stateless**: solo reenvía mensajes y mantiene `previous_response_id`
- El MCP Server no requiere Entra ID en este taller (modo simplificado)

---

## DIAPOSITIVA 12 — MCP Tools: definición en .NET

```csharp
[McpServerTool(
    Name = "transfer_budget",
    ReadOnly = false,
    Destructive = true,
    Idempotent = false)]
[Description("Transfiere presupuesto entre proyectos. SIEMPRE pedir confirmación antes de llamar.")]
public async Task<string> TransferBudget(
    [Description("ID del proyecto origen")] string sourceProjectId,
    [Description("ID del proyecto destino")] string targetProjectId,
    [Description("Monto en USD (positivo)")] decimal amount,
    CancellationToken cancellationToken)
{
    var result = await _useCase.ExecuteAsync(sourceProjectId, targetProjectId, amount, cancellationToken);
    return result.ToJson();
}
```

**Atributos clave:**

| Atributo | Qué comunica al cliente MCP |
|---|---|
| `ReadOnly = false` | La tool modifica estado |
| `Destructive = true` | La operación no es reversible |
| `Idempotent = false` | Llamadas repetidas tienen efectos distintos |

---

## DIAPOSITIVA 13 — Human-in-the-Loop: dos enfoques

> **Diagrama recomendado:** Dos recuadros lado a lado con flechas distintas mostrando el flujo.

### Enfoque 1: Conversacional (este taller)

**El agente pregunta antes de ejecutar.** No requiere código adicional. Se implementa en las instrucciones de sistema.

```
Turno 1: Usuario pide transferencia
         → Agente consulta balances
         → Agente presenta plan detallado
         → Agente pregunta: "¿Confirmás?"
Turno 2: Usuario dice "sí"
         → Agente llama transfer_budget
```

**Ventaja:** Simple. Solo instrucciones de sistema.  
**Limitación:** El agente podría ser persuadido a saltear la confirmación.

---

### Enfoque 2: `mcp_approval_request` (infraestructura)

**El servidor Responses API detiene la ejecución** antes de llamar al MCP tool y retorna un objeto de aprobación.

```json
// El agente retorna esto ANTES de ejecutar el tool:
{
  "type": "mcp_approval_request",
  "name": "transfer_budget",
  "arguments": "{ \"sourceProjectId\": \"PRJ002\", \"amount\": 15000 }",
  "server_label": "budget_api"
}
```

El backend debe responder con `mcp_approval_response` para continuar.

**Ventaja:** Garantía a nivel de infraestructura — el tool nunca se llama sin aprobación.  
**Cuándo:** Tools configuradas con `require_approval: "always"`.

---

## DIAPOSITIVA 14 — Human-in-the-Loop: comparación

| | **Conversacional** | **`mcp_approval_request`** |
|---|---|---|
| **Nivel** | Semántico (instrucciones) | Infraestructura (protocolo) |
| **Código extra** | No | Sí (loop de aprobación en el backend) |
| **Selectividad** | Solo para tools destructivas (por instrucción) | Configurable por tool (`require_approval`) |
| **Posible saltear** | Sí (persuasión) | No (protocolo lo bloquea) |
| **UX** | Fluida — mismo chat | Puede ser abrupta si no se diseña bien |
| **Usado en este taller** | ✅ Sí | ❌ No (extensión opcional) |

**Recomendación:** Combinar ambos para máxima seguridad en producción.

---

## DIAPOSITIVA 15 — Instrucciones de sistema para H-i-t-L conversacional

```
## Reglas obligatorias para transfer_budget

NUNCA llamar a transfer_budget sin confirmación explícita del usuario en el turno actual.

Antes de ejecutar cualquier transferencia, DEBÉS:
1. Llamar a get_project_balance para el proyecto origen y destino.
2. Presentar un resumen con:
   - Monto a transferir
   - Origen: balance actual → balance resultante
   - Destino: balance actual → balance resultante
   - Advertencia si el origen quedará con menos del 20% de su presupuesto
3. Preguntar: "¿Confirmás la transferencia de $X de [origen] a [destino]?"
4. Esperar la respuesta del usuario.
5. Solo si el usuario confirma ("sí", "confirmo", "procede"),
   llamar a transfer_budget.
6. Si dice "no" o expresa dudas, cancelar y notificar.
```

**Clave:** La continuidad entre turnos la garantiza `previous_response_id`.

---

## DIAPOSITIVA 16 — Demo: flujo completo

> **Diagrama recomendado:** Diagrama de secuencia (swim lanes) con: Usuario / App Backend / Foundry Agent / MCP Server / BackendApi

**Turno 1 — Consulta de balance:**
```
Usuario: "¿Cuánto tiene disponible el Proyecto Beta?"
  → Foundry Agent llama: get_project_balance("PRJ002")
  → MCP Server → BackendApi → $58,000 disponible
  ← Agente responde en español con el resumen
```

**Turno 2 — Solicitud de transferencia:**
```
Usuario: "Necesito transferir $15,000 de Beta a Alpha"
  → Agente llama: get_project_balance("PRJ002"), get_project_balance("PRJ001")
  ← Agente presenta tabla con balances actuales y resultantes
  ← Agente pregunta: "¿Confirmás la transferencia?"
```

**Turno 3 — Confirmación y ejecución:**
```
Usuario: "Sí, confirmo"
  → Agente llama: transfer_budget("PRJ002", "PRJ001", 15000)
  → MCP Server → BackendApi actualiza balances
  ← Agente confirma: "Transferencia completada. PRJ002: $43,000 | PRJ001: $54,500"
```

---

## DIAPOSITIVA 17 — Datos de prueba (MockApi)

| Proyecto | Estado | Asignado | Gastado | **Disponible** |
|---|---|---|---|---|
| PRJ001 — Project Alpha | Activo | $150,000 | $92,500 (62%) | **$39,500** |
| PRJ002 — Project Beta | Planning | $75,000 | $12,000 (16%) | **$58,000** |
| PRJ003 — Project Gamma | Completado | $200,000 | $198,500 (99%) | $1,500 |

**Escenario del taller:** PRJ001 necesita $15,000 adicionales. PRJ002 tiene disponibilidad. Transferencia PRJ002 → PRJ001.

---

## DIAPOSITIVA 18 — Tools disponibles en el MCP Server

| Tool | Tipo | Descripción |
|---|---|---|
| `get_projects` | ReadOnly | Lista todos los proyectos |
| `get_project_details` | ReadOnly | Detalle de un proyecto por ID |
| `get_project_balance` | ReadOnly | Balance financiero de un proyecto |
| `get_tasks` | ReadOnly | Lista tareas (filtros opcionales) |
| `create_task` | Write | Crea una tarea nueva |
| `update_task_status` | Write | Actualiza el estado de una tarea |
| `delete_task` | Destructive | Elimina una tarea |
| `get_backend_users` | ReadOnly (admin) | Lista usuarios del sistema |
| **`transfer_budget`** | **Destructive** | **Transfiere presupuesto entre proyectos** |

---

## DIAPOSITIVA 19 — Extensiones posibles

1. **Agregar Entra ID al MCP Server:** JWT Bearer + OBO token exchange para que el agente llame al backend como el usuario autenticado.
2. **`mcp_approval_request` en el backend:** loop de aprobación programático como segunda capa de seguridad.
3. **Streaming:** `stream: true` en la Responses API para respuestas progresivas en el frontend.
4. **Telemetría:** conectar `McpActivitySource` + OpenTelemetry al Aspire Dashboard para visualizar trazas de llamadas a tools.
5. **Múltiples MCP Servers:** agregar un segundo servidor (ej. notificaciones por email) y demostrar orquestación multi-servidor en un solo agente.

---

## DIAPOSITIVA 20 — Referencias

| Recurso | URL |
|---|---|
| Responses API + MCP | https://learn.microsoft.com/azure/foundry/openai/how-to/responses |
| MCP Tool en Foundry Agent Service | https://learn.microsoft.com/azure/foundry/agents/how-to/tools/model-context-protocol |
| Hosted Agents | https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents |
| Human-in-the-Loop con AG-UI | https://learn.microsoft.com/agent-framework/integrations/ag-ui/human-in-the-loop |
| MCP Tool con Agent Framework (.NET) | https://learn.microsoft.com/agent-framework/agents/tools/hosted-mcp-tools |
| MCP Spec oficial | https://modelcontextprotocol.io/introduction |
| foundry-agent-webapp (GitHub) | https://github.com/microsoft-foundry/foundry-agent-webapp |

---

## DIAPOSITIVA 21 — Cierre

**Lo que vimos hoy:**

- Azure Foundry: plataforma administrada para agentes de IA
- Prompt Agent (sin código) vs Hosted Agent (contenedor propio)
- Responses API: la única opción para MCP tools, stateless, multi-turn
- Responses API vs Activity Protocol: cuándo usar cada uno
- MCP Server: estándar abierto, Streamable HTTP, .NET con `ModelContextProtocol.AspNetCore`
- Human-in-the-Loop: conversacional (instrucciones) vs `mcp_approval_request` (infraestructura)

**Próximos pasos:**
- Clonar el repo y ejecutar `cd src/McpServer.AppHost && dotnet run`
- Agregar `transfer_budget` al MCP Server siguiendo la guía del taller
- Crear el Prompt Agent en `ai.azure.com` y conectar el MCP Server

---

## NOTAS PARA EL PRESENTADOR

### Diagramas de PowerPoint recomendados (resumen)

| Diapositiva | Tipo de diagrama | Herramienta sugerida |
|---|---|---|
| 3 — ¿Qué es Foundry? | Triángulo con Model/Instructions/Tools | SmartArt → Proceso cíclico |
| 5 — Prompt Agent | Flujo lineal portal → API → Agente | SmartArt → Proceso |
| 6 — Hosted Agent | Pipeline Docker → ACR → Foundry → Endpoint | Cajas con flechas + íconos Azure |
| 7 — Comparación | Dos columnas con fondo de color | Tabla con formato condicional |
| 9 — Responses vs Activity | Dos caminos paralelos desde el usuario | Diagrama de carriles (swim lanes) |
| 11 — Arquitectura del taller | Stack vertical de capas | Cajas apiladas con flechas y colores |
| 13 — H-i-t-L enfoques | Dos recuadros lado a lado con flujo de turnos | Diagrama de secuencia simplificado |
| 16 — Demo flujo | Swim lanes: Usuario / Backend / Foundry / MCP / API | Diagrama de secuencia completo |

### Pitfalls comunes a mencionar en el taller

- **`previous_response_id`:** si no se reenvía en cada turno, el agente pierde el contexto de conversación.
- **`mcp_approval_request`:** si `require_approval` no está configurado, el tool se ejecuta sin confirmación a nivel de infraestructura.
- **MCP tool solo en Responses API:** Chat Completions API no lo soporta — error frecuente.
- **Roles Entra ID:** deben asignarse en Enterprise Applications → Users and groups, no solo en App Registrations.
- **Tool no aparece en Foundry:** verificar que el endpoint `/mcp` sea accesible públicamente (TLS 1.2+).
