# Agentes con Azure Foundry: MCP Tools, Responses API y Human-in-the-Loop

**Duration**: ~60 minutes  
**Level**: Intermediate (prior experience with .NET and Azure required)  
**Base repository**: `microsoft-foundry/foundry-agent-webapp` + `az-api-oper-mcp-server-dotnet`

---

## Agenda

| Time      | Block        | Content                                                   |
| --------- | ------------ | --------------------------------------------------------- |
| 00–10 min | Intro        | Context, objectives, tech stack                           |
| 10–20 min | Architecture | Prompt Agent, Responses API, MCP Server, H-i-t-L pattern  |
| 20–30 min | Demo 1       | Read MCP tools: query balances and projects               |
| 30–45 min | Demo 2       | H-i-t-L: budget transfer with conversational confirmation |
| 45–55 min | Demo 3       | Full end-to-end flow with React frontend                  |
| 55–60 min | Wrap-up      | Q&A, possible extensions, references                      |

---

## Workshop Objectives

1. Understand how a **Prompt Agent** in Azure Foundry consumes remote tools via MCP.
2. Understand the **Responses API** flow with `agent_reference` and `previous_response_id`.
3. Implement conversational **Human-in-the-Loop** through system instructions — no additional code required.
4. Add a new destructive MCP tool (`transfer_budget`) to the existing server.

---

## Arquitectura

```
Usuario (React UI)
      │  HTTP
      ▼
foundry-agent-webapp (ASP.NET Core 10)
  POST /api/chat
  → openai.responses.create({ agent_reference, previous_response_id })
      │  HTTPS / Responses API
      ▼
Azure Foundry (Prompt Agent: "budget-agent")
  → System instructions + MCP tools configurados en el portal
      │  MCP Streamable HTTP
      ▼
McpServer.Presentation (:5230)
  → Tools: get_project_balance, get_projects, transfer_budget, ...
      │  HTTP
      ▼
McpServer.BackendApi (EF Core InMemory)
  → Datos: PRJ001, PRJ002, PRJ003, balances, tareas, usuarios
```

**Key principles**:

- The **Prompt Agent lives in Foundry** (portal `ai.azure.com`), not in code.
- The backend (`foundry-agent-webapp`) is a **stateless relay**: it only forwards messages and maintains `previous_response_id`.
- The **H-i-t-L is conversational**: the agent asks "Do you confirm?" and waits for the next turn. Does not use `mcp_approval_request`.
- The **MCP Server does not require Entra ID** in this workshop: no OBO token, no JWT — plain Streamable HTTP or a shared API key.

---

## Key Concepts

### Prompt Agent vs Hosted Agent

| Aspect             | Prompt Agent (this workshop)                       | Hosted Agent                                |
| ------------------ | -------------------------------------------------- | ------------------------------------------- |
| Definition         | Portal `ai.azure.com`                              | Code (`CreateAgentVersionAsync`)            |
| Instructions       | Text box in the portal                             | `const string AgentInstructions = ...`      |
| MCP Tools          | Configured in "Add Tools" in the portal            | `MCPToolDefinition(serverLabel, serverUrl)` |
| Invocation         | `agent_reference: { name, type }` in Responses API | `AIAgent.RunAsync(session, message)`        |
| Conversation state | `previous_response_id` for multi-turn              | `AgentSession` managed by the SDK           |

### Responses API vs Chat Completions API

- **Responses API** (`POST /openai/v1/responses`): supports `tools: [{type: "mcp", ...}]` and `agent_reference`. **Only option for MCP tools**.
- **Chat Completions API** (`POST /openai/v1/chat/completions`): does NOT support MCP tools.
- MCP tools are available on `gpt-4o`, `gpt-4.1`, and reasoning models (`o1`, `o3`).

### Conversational Human-in-the-Loop

The H-i-t-L pattern requires no special code. It is implemented in the agent's **system instructions**:

1. The user requests a destructive action.
2. The agent calls **read** tools to gather context.
3. The agent presents a **detailed plan** with quantified impact.
4. The agent explicitly asks: _"Do you confirm this operation?"_
5. The user responds affirmatively or negatively in the **next turn**.
6. Only if there is explicit confirmation does the agent call the **destructive** tool.

Turn continuity is managed by `previous_response_id`, which the backend forwards on every call to the Responses API.

### mcp_approval_request (Different from H-i-t-L)

When `require_approval: "always"` is configured on an MCP tool, the Responses API returns an `mcp_approval_request` object **before** executing the tool. The backend must respond with an `mcp_approval_response` to continue. This mechanism operates at the infrastructure level (every call to any tool). **Not used in this workshop** — our H-i-t-L is semantic and selective (only for `transfer_budget`).

---

## Responses API vs Agent Activity Protocol

There are two agent interaction models in the Microsoft ecosystem worth distinguishing.

### Quick comparison

| Aspect            | **Responses API** (this workshop)           | **Agent Activity Protocol**                              |
| ----------------- | ------------------------------------------- | -------------------------------------------------------- |
| Standard          | OpenAI-compatible                           | Bot Framework Activity Schema                            |
| State             | Stateless (`previous_response_id` per turn) | Stateful (threads, activities, transport channel)        |
| Transport         | HTTPS request/response                      | WebSocket / Direct Line / Bot Connector                  |
| MCP tools         | Yes, natively (`type: "mcp"`)               | Not directly — integrates via agent SDK tool definitions |
| `agent_reference` | Yes — invokes a Foundry Prompt Agent        | N/A                                                      |
| Multi-channel     | No (direct API)                             | Yes (Teams, Copilot Studio, Direct Line, etc.)           |
| Best for          | Web/mobile apps calling the agent directly  | Multi-channel bots, Copilot Studio, Teams scenarios      |
| Setup complexity  | Low — one HTTP call                         | Higher — bot registration, channels, channel auth        |

### When to use each

**Responses API**: the app controls the conversation lifecycle. The backend makes one HTTP call per turn and returns the response to the client. Simple, direct, ideal for this workshop.

**Agent Activity Protocol**: the channel (Teams, Direct Line) delivers activities (`Activity.Type = "message"`) to the bot. The bot responds with activities in return. Required when the agent lives in Teams or Copilot Studio and must handle lifecycle events (`conversationUpdate`, `typing`, etc.).

### Relationship with MCP

The MCP protocol (Model Context Protocol) is **orthogonal** to both: it defines how the agent discovers and calls remote tools, regardless of how the user reaches the agent. In this workshop, the agent uses MCP to call tools on the MCP Server and the Responses API to receive user messages. In a Bot Framework scenario, the same MCP Server could be used from a Hosted Agent consumed via Activity Protocol.

---

## Test Data (MockApi)

| Project                | Status    | Allocated | Spent          | Available   |
| ---------------------- | --------- | --------- | -------------- | ----------- |
| PRJ001 — Project Alpha | Active    | $150,000  | $92,500 (62%)  | **$39,500** |
| PRJ002 — Project Beta  | Planning  | $75,000   | $12,000 (16%)  | **$58,000** |
| PRJ003 — Project Gamma | Completed | $200,000  | $198,500 (99%) | $1,500      |

**Workshop scenario**: PRJ001 needs an additional $15,000 to cover commitments. PRJ002 has $58,000 available. Budget is transferred from PRJ002 → PRJ001.

---

## Available MCP Tools

| Tool                  | Type             | Description                                 |
| --------------------- | ---------------- | ------------------------------------------- |
| `get_projects`        | ReadOnly         | Lists all projects                          |
| `get_project_details` | ReadOnly         | Project detail by ID                        |
| `get_project_balance` | ReadOnly         | Financial balance of a project              |
| `get_tasks`           | ReadOnly         | Lists tasks (optional filters)              |
| `create_task`         | Write            | Creates a new task                          |
| `update_task_status`  | Write            | Updates task status                         |
| `delete_task`         | Destructive      | Deletes a task                              |
| `get_backend_users`   | ReadOnly (admin) | Lists system users                          |
| **`transfer_budget`** | **Destructive**  | **Transfers budget between projects** ← new |

---

## Step 1: New Tool — `transfer_budget`

### 1.1 MockApi Endpoint

Add to `src/McpServer.BackendApi/Controllers/BalancesController.cs`:

```csharp
[HttpPost("transfer")]
public async Task<IActionResult> Transfer([FromBody] TransferRequest request, CancellationToken ct)
{
    var result = await _balancesService.TransferAsync(
        request.SourceProjectId, request.TargetProjectId, request.Amount, ct);

    if (!result.Success)
    {
        return BadRequest(new { error = result.ErrorMessage });
    }

    return Ok(new
    {
        message = $"Transfer completed: ${request.Amount:N0} from {request.SourceProjectId} to {request.TargetProjectId}",
        sourceBalance = result.UpdatedSourceBalance,
        targetBalance = result.UpdatedTargetBalance
    });
}
```

Request model in `src/McpServer.BackendApi/Models/`:

```csharp
public sealed record TransferRequest(
    string SourceProjectId,
    string TargetProjectId,
    decimal Amount);
```

### 1.2 MCP Server Tool

Add to `src/MCP-Server/McpServer.Presentation/Tools/BalancesTools.cs` (or a separate class):

```csharp
[McpServerTool(
    Name = "transfer_budget",
    Title = "Transfer Budget Between Projects",
    ReadOnly = false,
    Destructive = true,
    Idempotent = false,
    OpenWorld = false)]
[Description("Transfers budget from one project to another. ALWAYS ask for explicit user confirmation before calling this tool.")]
public async Task<string> TransferBudget(
    [Description("Source project ID (budget is deducted from here)"), Required] string sourceProjectId,
    [Description("Target project ID (budget is added here)"), Required] string targetProjectId,
    [Description("Amount to transfer in USD (must be positive)"), Required, Range(1, 1_000_000)] decimal amount,
    CancellationToken cancellationToken)
{
    var result = await transferBudgetUseCase.ExecuteAsync(
        sourceProjectId, targetProjectId, amount, cancellationToken);
    return result.ToJson();
}
```

> Workshop note: the agent's system instructions (see next section) are the first line of defense for H-i-t-L. The `Destructive = true` parameter exposes metadata to the MCP client but does not block the call on its own.

---

## Step 2: Configure the Prompt Agent in Foundry

### 2.1 Create the agent in the portal

1. Go to [https://ai.azure.com](https://ai.azure.com) → your project.
2. **Agents** → **New agent**.
3. Name: `budget-agent`.
4. Model: `gpt-4.1` (or `gpt-4o`).
5. Paste the system instructions (see section 2.2).
6. **Add Tools** → **Model Context Protocol** → enter the MCP Server URL:
   - URL: `https://<your-server>/mcp` (or `http://localhost:5230/mcp` for local)
   - Label: `budget_api`
   - Auth: None (no Entra ID for the workshop)
7. Save the agent. Note the exact name (`budget-agent`).

### 2.2 System Instructions for the Prompt Agent

```
You are a project financial assistant. You have access to tools to query and modify
project budgets through the budget API.

## Available tools
- get_projects: list all active projects
- get_project_balance: retrieve the financial balance of a project
- transfer_budget: transfer budget between projects (DESTRUCTIVE)

## Mandatory rules for transfer_budget

NEVER call transfer_budget without explicit user confirmation in the current turn.

Before executing any transfer, you MUST:
1. Call get_project_balance for both the source and target projects.
2. Present a summary including:
   - Amount to transfer
   - Source project: current balance → resulting balance
   - Target project: current balance → resulting balance
   - Warning if the source project will have less than 20% of its allocated budget remaining
3. Ask: "Do you confirm the transfer of $X from [source] to [target]?"
4. Wait for the user's response.
5. Only if the user replies with clear confirmation ("yes", "confirm", "proceed", "ok"),
   call transfer_budget.
6. If the user says "no", "cancel", or expresses any doubt, cancel the operation and inform the user.

## Response format
- Always respond in Spanish.
- Present amounts in $XX,000 format (USD).
- For read operations, respond directly and concisely.
- For destructive operations, follow the confirmation flow above without exceptions.
```

---

## Step 3: Backend Relay (foundry-agent-webapp)

The `foundry-agent-webapp` backend already implements the relay pattern. The `/api/chat` handler does essentially:

```csharp
// Call the Responses API with agent_reference
var response = await openAIClient.Responses.CreateAsync(new ResponseCreationOptions
{
    Input = [new UserChatMessage(userMessage)],
    Model = "gpt-4.1",
    PreviousResponseId = previousResponseId, // multi-turn
    // Tools are not passed here: the agent has them configured in Foundry
});

// Extract previous_response_id for the next turn
var newResponseId = response.Id;

// Handle mcp_approval_request if require_approval: "always"
// (in this workshop: require_approval not configured or "never" — H-i-t-L is conversational)
```

> The `previous_response_id` is sent from the frontend on every request and returned in the response so the client can store it.

**Backend environment variables**:

```env
AZURE_OPENAI_ENDPOINT=https://<account>.openai.azure.com
AZURE_OPENAI_API_KEY=<api-key>  # or use Managed Identity
FOUNDRY_AGENT_NAME=budget-agent
```

---

## Step 4: Demo Script

### Demo 1 — Balance Query (5 min)

```
User: "How much budget is available for Project Beta?"

[Agent calls: get_project_balance("PRJ002")]

Agent: "Project Beta (PRJ002) has:
- Allocated budget: $75,000
- Spent: $12,000 (16%)
- Committed: $5,000
- Available: $58,000"
```

### Demo 2 — H-i-t-L: Transfer Initiation (10 min)

```
User: "I need to transfer $15,000 from Project Beta to Project Alpha"

[Agent calls: get_project_balance("PRJ002")]
[Agent calls: get_project_balance("PRJ001")]

Agent: "Before proceeding, I checked the current balances:

**Operation summary:**
- Amount to transfer: $15,000

| Project         | Current balance   | Resulting balance |
|-----------------|-------------------|-------------------|
| PRJ002 (source) | $58,000 available | $43,000 available |
| PRJ001 (target) | $39,500 available | $54,500 available |

PRJ002 would retain 57% of its available capacity — no risk.

Do you confirm the transfer of $15,000 from Project Beta to Project Alpha?"
```

### Demo 3 — Confirmation and Execution (5 min)

```
User: "Yes, confirm"

[Agent calls: transfer_budget("PRJ002", "PRJ001", 15000)]

Agent: "Transfer completed successfully.

- $15,000 transferred from PRJ002 → PRJ001
- PRJ002 (Project Beta): now $43,000 available
- PRJ001 (Project Alpha): now $54,500 available"
```

### Demo 4 — Rejection (2 min)

```
User: "Transfer $60,000 from Project Beta to Project Alpha"

[Agent calls: get_project_balance("PRJ002")]
[Agent calls: get_project_balance("PRJ001")]

Agent: "I found a problem with this operation:

| Project         | Current balance   | Resulting balance |
|-----------------|-------------------|-------------------|
| PRJ002 (source) | $43,000 available | -$17,000 ❌        |
| PRJ001 (target) | $54,500 available | $114,500          |

⚠️ The transfer exceeds PRJ002's available balance ($43,000).
This operation cannot be executed.

Would you like to transfer a smaller amount, for example $40,000?"
```

---

## Workshop Prerequisites

### For the presenter

- [ ] Active Azure Foundry project with `gpt-4.1` model deployed
- [ ] MCP Server (`McpServer.Presentation`) published or running locally (`:5230`)
- [ ] `foundry-agent-webapp` running with environment variables configured
- [ ] Prompt Agent `budget-agent` created in the portal with instructions and MCP tool connected
- [ ] `transfer_budget` tool implemented in MockApi and MCP Server
- [ ] Fresh MockApi data (restart the app to reset the InMemory DB)

### For attendees (if hands-on)

- [ ] Access to the Azure tenant with permissions in the Foundry project
- [ ] .NET 10 SDK installed
- [ ] Node.js 20+ installed
- [ ] Repos cloned: `foundry-agent-webapp` and `az-api-oper-mcp-server-dotnet`
- [ ] `.env` file with credentials provided by the presenter

### Minimum environment variables

**foundry-agent-webapp** (backend):

```env
AZURE_OPENAI_ENDPOINT=https://<account>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
FOUNDRY_AGENT_NAME=budget-agent
```

**McpServer** (MCP Server — workshop mode, no Entra ID):

```env
# appsettings.Development.json or env vars
# No EntraId configuration for the workshop
# Expose on http://localhost:5230
```

---

## Validations against Microsoft Learn (April 2026)

The following features were validated against official documentation before the workshop:

| Feature                                          | Status        | Reference                                                                                                                   |
| ------------------------------------------------ | ------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `agent_reference` in Responses API               | ✅ No changes | [REST API Reference](https://learn.microsoft.com/rest/api/aifoundry/aiproject#components)                                   |
| `mcp_approval_request` / `mcp_approval_response` | ✅ No changes | [Responses API — MCP Approvals](https://learn.microsoft.com/azure/foundry/openai/how-to/responses#using-remote-mcp-servers) |
| `require_approval: "always" \| "never"`          | ✅ No changes | [Toolbox docs](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/toolbox)                                       |
| MCP tools only in Responses API                  | ✅ Confirmed  | [Responses API docs](https://learn.microsoft.com/azure/foundry/openai/how-to/responses#using-remote-mcp-servers)            |
| 100-second timeout (MCP non-streaming)           | ✅ No changes | Documented in Agent Service                                                                                                 |
| TLS 1.2+ required for MCP client                 | ✅ Applicable | [Responses API Authentication](https://learn.microsoft.com/azure/foundry/openai/how-to/responses)                           |
| Unauthenticated access to MCP Server             | ✅ Supported  | When the server does not require auth                                                                                       |

> **Note**: Microsoft Learn documentation now distinguishes between **Foundry Agent Service (classic)** (threads/runs, classic SDK) and the new experience based on `AIProjectClient.AgentAdministrationClient`. The Responses API + `agent_reference` flow for Prompt Agents is part of the **new** experience and remains the recommended approach.

---

## Possible Extensions (Post-Workshop)

1. **Add Entra ID to the MCP Server**: enable JWT Bearer + OBO as configured in the full repo (`McpServer.Presentation` with `AuthenticationExtensions.cs`). See `docs/ENTRA-ID-TESTING-GUIDE.md`.
2. **`mcp_approval_request` in the backend**: implement the approval loop in the relay for tools with `require_approval: "always"` as a second security layer.
3. **Streaming**: use `stream: true` in the Responses API to stream responses progressively in the frontend.
4. **Telemetry**: connect `McpActivitySource` + OpenTelemetry to the Aspire Dashboard to visualize tool call traces.
5. **Multiple MCP Servers**: add a second server (e.g., email notifications) and demonstrate multi-server orchestration in a single agent.

---

## References

| Resource                             | URL                                                                                        |
| ------------------------------------ | ------------------------------------------------------------------------------------------ |
| Responses API — MCP Servers          | https://learn.microsoft.com/azure/foundry/openai/how-to/responses#using-remote-mcp-servers |
| MCP Tool in Foundry Agent Service    | https://learn.microsoft.com/azure/foundry/agents/how-to/tools/model-context-protocol       |
| Toolbox with require_approval        | https://learn.microsoft.com/azure/foundry/agents/how-to/tools/toolbox                      |
| MCP Tool with Agent Framework (.NET) | https://learn.microsoft.com/agent-framework/agents/tools/hosted-mcp-tools                  |
| REST API Reference (agent_reference) | https://learn.microsoft.com/rest/api/aifoundry/aiproject#components                        |
| MCP via APIM (governance)            | https://learn.microsoft.com/azure/foundry/agents/how-to/tools/governance                   |
| MCP Spec (modelcontextprotocol.io)   | https://modelcontextprotocol.io/introduction                                               |
| foundry-agent-webapp (GitHub)        | https://github.com/microsoft-foundry/foundry-agent-webapp                                  |
