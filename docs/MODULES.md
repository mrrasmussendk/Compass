# Built-in Modules

Vitruvian ships with two categories of built-in modules: **Standard Modules** bundled inside the host, and **Google MCP Modules** that live in the `modules/` directory and connect to Google services via [MCP (Model Context Protocol)](https://modelcontextprotocol.io/).

All modules implement `IVitruvianModule` and are discovered automatically at startup. Use `/list-modules` in the CLI to see which modules are loaded, or `--configure-modules` to enable/disable them interactively.

---

## Table of Contents

- [Standard Modules](#standard-modules)
  - [Conversation](#conversation)
  - [File Operations](#file-operations)
  - [Web Search](#web-search)
  - [Shell Command](#shell-command)
  - [Summarization](#summarization)
- [Google MCP Modules](#google-mcp-modules)
  - [Gmail](#gmail)
  - [Google Drive](#google-drive)
  - [Google Calendar](#google-calendar)
- [Environment Variables](#environment-variables)

---

## Standard Modules

These modules are compiled into `Vitruvian.StandardModules` and are always available.

### Conversation

| Property | Value |
|----------|-------|
| **Domain** | `conversation` |
| **Description** | Answer general questions, provide explanations, fallback for unhandled queries |
| **Permissions** | `Read` |
| **API Keys** | None |

The default fallback module. When no other module matches a request, the conversation module handles it by routing the query to the configured LLM. It cannot be disabled.

### File Operations

| Property | Value |
|----------|-------|
| **Domain** | `file-operations` |
| **Description** | Read content from files or write/create text files |
| **Permissions** | `Read`, `Write` |
| **API Keys** | None |

Reads and writes files within the configured working directory (`VITRUVIAN_WORKING_DIRECTORY`, default `~/Vitruvian-workspace`). Write operations are gated through HITL approval.

### Web Search

| Property | Value |
|----------|-------|
| **Domain** | `web-search` |
| **Description** | Search the web for current information, weather, news, real-time data |
| **Permissions** | `Read` |
| **API Keys** | None |

Searches the web for up-to-date information using the configured model's tool-calling capabilities.

### Shell Command

| Property | Value |
|----------|-------|
| **Domain** | `shell-command` |
| **Description** | Execute shell commands |
| **Permissions** | `Execute` |
| **API Keys** | None |

Runs shell commands on the host system. Gated through HITL approval and subject to sandbox resource limits when enabled. See [Security](SECURITY.md) for sandboxing details.

### Summarization

| Property | Value |
|----------|-------|
| **Domain** | `summarization` |
| **Description** | Summarize text content using AI |
| **Permissions** | `Read` |
| **API Keys** | None |

Summarizes text content by routing it to the configured LLM with a summarization prompt.

---

## Google MCP Modules

These modules live in the `modules/` directory and connect to Google services through OpenAI MCP connectors. Each module requires a Google API token to authenticate with the respective service.

> **Provider requirement:** MCP connector-based modules are forwarded natively to providers that support them. Currently OpenAI and Anthropic support MCP tools. See [MCP Tools](EXTENDING.md#mcp-tools) for details.

### Gmail

| Property | Value |
|----------|-------|
| **Domain** | `gmail-mcp` |
| **Description** | Read, search, and draft Gmail messages using MCP |
| **Permissions** | `Read` |
| **API Keys** | `GOOGLE_API_TOKEN` |
| **MCP Connector** | `connector_gmail` |
| **Source** | `modules/Vitruvian.GmailModule/` |

Connects to Gmail via an MCP connector to read, search, and draft email messages. The module can create draft replies but **never sends messages directly**.

#### Capabilities

- Search emails by query, sender, subject, or label
- Read email content and metadata
- Create draft replies and new draft messages

#### Example requests

```
> Search my Gmail for emails from alice@example.com
> Draft a reply to the latest email about the project update
> Show me unread emails from today
```

#### Setup

Set the `GOOGLE_API_TOKEN` environment variable in `.env.Vitruvian` or export it before running:

```bash
echo 'GOOGLE_API_TOKEN=your-google-api-token' >> .env.Vitruvian
```

### Google Drive

| Property | Value |
|----------|-------|
| **Domain** | `google-drive-mcp` |
| **Description** | Read, search, and manage Google Drive files using MCP |
| **Permissions** | `Read`, `Write` |
| **API Keys** | `GOOGLE_DRIVE_TOKEN` |
| **MCP Connector** | `connector_googledrive` |
| **Source** | `modules/Vitruvian.GoogleDriveModule/` |

Connects to Google Drive via an MCP connector to manage files. Supports both read and write operations — write operations are subject to HITL approval.

#### Capabilities

- List and search files in Google Drive
- Upload new files
- Download file content
- Update existing files
- Delete files

#### Example requests

```
> List my recent Google Drive files
> Search Google Drive for documents about quarterly report
> Upload notes.txt to my Google Drive
```

#### Setup

Set the `GOOGLE_DRIVE_TOKEN` environment variable in `.env.Vitruvian` or export it before running:

```bash
echo 'GOOGLE_DRIVE_TOKEN=your-google-drive-token' >> .env.Vitruvian
```

### Google Calendar

| Property | Value |
|----------|-------|
| **Domain** | `google-calendar-mcp` |
| **Description** | Read, create, and manage Google Calendar events using MCP |
| **Permissions** | `Read`, `Execute` |
| **API Keys** | `GOOGLE_API_TOKEN` |
| **MCP Connector** | `connector_googlecalendar` |
| **Source** | `modules/Vitruvian.GoogleCalendarModule/` |

Connects to Google Calendar via an MCP connector to manage calendar events. Event creation, updates, and deletions are gated through HITL approval.

#### Capabilities

- List upcoming events
- Create new calendar events
- Update existing events
- Delete events

#### Example requests

```
> What meetings do I have today?
> Create a meeting with Bob tomorrow at 2pm for 30 minutes
> Show my calendar for next week
```

#### Setup

The Google Calendar module shares the `GOOGLE_API_TOKEN` with the Gmail module. Set it in `.env.Vitruvian` or export it before running:

```bash
echo 'GOOGLE_API_TOKEN=your-google-api-token' >> .env.Vitruvian
```

---

## Environment Variables

The Google MCP modules require the following environment variables:

| Variable | Used by | Description |
|----------|---------|-------------|
| `GOOGLE_API_TOKEN` | Gmail, Google Calendar | Google API token for Gmail and Calendar access |
| `GOOGLE_DRIVE_TOKEN` | Google Drive | Google API token for Drive access |

These can be set via:

1. **Guided setup** — `./scripts/install.sh` or `.\scripts\install.ps1` (the installer scans module DLLs for `[RequiresApiKey]` attributes and prompts for missing values)
2. **`.env.Vitruvian` file** — add `GOOGLE_API_TOKEN=...` and/or `GOOGLE_DRIVE_TOKEN=...`
3. **Environment export** — `export GOOGLE_API_TOKEN=...` before running the CLI

See [Installation](INSTALL.md) for the full environment variable reference.
