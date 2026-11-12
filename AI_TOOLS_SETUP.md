# AI Tooling Setup

A guide for new contributors who want to work on this repo with Claude Code (or Codex). It lists the AI tools, MCP servers and plugins the repo's agent workflow relies on, and how to install and verify each one.

The workflow itself (agents, lifecycle, rules) is described in [`AGENTS.md`](AGENTS.md) and `docs/process/lifecycle.md`. This file only covers getting the tools onto your machine.

> You can build, test and run the project without any of this (see [`README.md`](README.md)). The tools below are needed only for the AI-assisted workflow.

## 1. Prerequisites

| Tool | Why | Check |
| --- | --- | --- |
| Git + [GitHub CLI](https://cli.github.com) (`gh`) | commits, PRs, `release-kit` skills | `gh auth status` |
| [Bun](https://bun.sh) | frontend package manager, Lefthook, the Bash guard script | `bun --version` |
| .NET 10 SDK | backend | `dotnet --version` |
| Node.js (LTS) | runs `npx`-based MCP servers (Playwright) | `node --version` |
| [uv](https://docs.astral.sh/uv/) | installs Serena and runs `uvx`-based MCP servers | `uv --version` |
| [Claude Code](https://docs.claude.com/en/docs/claude-code) | the main agent CLI | `claude --version` |

### Install anything that is missing

Skip the ones `Check` already passes for. Open a new terminal after installing so `PATH` updates.

```bash
# Bun
curl -fsSL https://bun.sh/install | bash

# uv (Python tool manager, needed for Serena)
curl -LsSf https://astral.sh/uv/install.sh | sh

# .NET 10 SDK (Linux/macOS; on Windows use winget install Microsoft.DotNet.SDK.10)
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0

# Node.js LTS, via nvm
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/master/install.sh | bash
nvm install --lts

# Claude Code
curl -fsSL https://claude.ai/install.sh | bash
```

GitHub CLI: follow <https://github.com/cli/cli#installation> for your OS, then run `gh auth login`.
Windows users can run the same tools in WSL2 (recommended) or install each from its own website.

Then, once, at the repo root:

```bash
bun install        # installs Lefthook git hooks (commitlint, secretlint, branch guard)
```

## 2. Required: code-intelligence MCP servers

`AGENTS.md` § Tool Selection tells agents to orient with CodeGraph first, then drill in with Serena. Both are expected to be available.

### CodeGraph

Pre-built knowledge graph of the codebase; the `codegraph_explore` tool answers most "how does X work / what breaks if I change Y" questions in one call.

```bash
# 1. Install the CLI if `codegraph --version` fails (macOS / Linux)
curl -fsSL https://raw.githubusercontent.com/colbymchenry/codegraph/main/install.sh | sh
#    Windows (PowerShell):  irm https://raw.githubusercontent.com/colbymchenry/codegraph/main/install.ps1 | iex
#    Or, with Node:         npm i -g @colbymchenry/codegraph
#    Open a new terminal afterwards, then confirm:
codegraph --version

# 2. Register it as an MCP server for Claude Code
claude mcp add --scope user codegraph -- codegraph serve --mcp
#    (alternative: `codegraph install` writes the agent config for you)

# 3. Build the index for this repo (run from the repo root)
codegraph init            # first time; creates .codegraph/ (gitignored)
codegraph status          # verify
```

Auto-sync is on by default, so you normally do not need to re-index. Restart Claude Code after registering the server.

The `.codegraph/` directory is local to your machine; never commit it.

### Serena

Language-server-backed symbol navigation and editing for C#, Vue and TypeScript. Project config is committed in `.serena/project.yml`; machine-specific overrides go in `.serena/project.local.yml` (ignored).

```bash
# 1. Install if `serena --version` fails (requires uv, see section 1)
uv tool install -p 3.13 serena-agent
serena init               # one-time setup
serena --version

# 2. Register it as an MCP server for Claude Code
claude mcp add --scope user serena -- serena start-mcp-server --context=claude-code --project-from-cwd
```

Install Serena with `uv` as above — not through an MCP or plugin marketplace. Other clients and options: <https://oraios.github.io/serena/02-usage/030_clients.html>.

Known limitations (frontend caller search / rename, gitignored files) are documented in `.claude/rules/serena.md` — read it before relying on Serena in `frontend/`.

## 3. Required: Claude Code plugins

The repo's skills come from two plugins.

| Plugin | Marketplace | Provides |
| --- | --- | --- |
| `superpowers` | `claude-plugins-official` | `brainstorming`, `writing-plans`, `subagent-driven-development`, TDD, debugging and review skills the workflow builds on |
| `release-kit` | `daont-claude-plugins` | `git-commit`, `git-mr`, `git-sync`, `git-release` skills; bound to this repo by `.claude/release-kit.json` |

Inside Claude Code:

```text
/plugin marketplace add DaoNguyenTrong/daont-claude-plugins
/plugin install superpowers@claude-plugins-official
/plugin install release-kit@daont-claude-plugins
```

`release-kit` and its marketplace are already declared in `.claude/settings.json`, so Claude Code will prompt you to trust and install them the first time you open the repo. Run `/plugin` to confirm both show as enabled.

## 4. Recommended: documentation and browser MCP servers

Not required, but the agents use them when available.

| MCP server | Used for | Install |
| --- | --- | --- |
| `context7` | up-to-date library docs (Vue, naive-ui, EF Core, ...) | `/plugin install context7@claude-plugins-official` |
| `playwright` | driving a real browser for UI checks and e2e debugging | `/plugin install playwright@claude-plugins-official` |

For e2e tests themselves you also need the Playwright browsers:

```bash
bun run --cwd frontend test:e2e:install
```

## 5. Optional: personal and team-specific tools

These are not referenced by the repo's rules. Add them only if you use them.

- **Claude Design** — the `ui-designer` agent publishes screens through the Artifact tool's Design canvas. It is built into Claude Code when your account has it enabled; nothing to install.
- **Plane** (`uvx plane-mcp-server stdio`), **ClickUp**, **Google Drive** — project-management and document connectors. Use the claude.ai connector settings (or `claude mcp add`) with your own credentials.
- **Codex** — the repo also supports Codex via `.codex/` and `.agents/skills/`. Install with `bun add -g @openai/codex`; after the first run, trust the repo's hook with `/hooks` (see `AGENTS.md` § Codex). CodeGraph and Serena must be added to your own Codex MCP config, the repo does not vendor them.

## 6. Verify

```bash
claude mcp list        # codegraph and serena should show "Connected"
codegraph status       # index exists for this repo
```

Inside a Claude Code session in the repo root:

1. `/plugin` — `superpowers` and `release-kit` enabled.
2. `/mcp` — `codegraph` and `serena` connected.
3. Ask: *"Use codegraph_explore to show how login is handled."* — you should get symbol source back, not a grep loop.

## 7. Things to know

- **Never commit secrets or local state.** `.codegraph/`, `.serena/cache`, `.serena/memories`, `.serena/project.local.yml`, `.claude/settings.local.json` and all `appsettings*.json` stay on your machine.
- **A Bash guard hook** (`scripts/claude-bash-guard.ts`, via `.claude/settings.json`) blocks `git add -A`, `git commit -a`, `--no-verify` and force-pushes. It needs `bun` on your `PATH`.
- **Worktrees** lack gitignored files (config, generated API client); read `.claude/reference/worktrees.md` before using one.
- **Troubleshooting:** if an MCP server shows "Failed to connect", run its command by hand (for example `codegraph serve --mcp`) to see the error, and make sure the tool's directory (`~/.local/bin`, `~/.bun/bin`) is on your `PATH`.
