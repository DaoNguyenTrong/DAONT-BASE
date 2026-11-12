// Bash PreToolUse guard for Claude and Codex (.claude/settings.json, .codex/hooks.json): stops accidental
// git/gh never-rule breaks (AGENTS.md § Commits). Not a defence against deliberate obfuscation.
// Payload JSON on stdin; prints a deny decision or nothing, always exits 0. Gaps: .claude/decisions.md.
import { isAbsolute, resolve } from 'node:path'

const SUBSTITUTION = '$()'
const MAX_DEPTH = 8

const REASON = {
  force:
    'Force push is never allowed (AGENTS.md § Commits). After a rebase, push your own branch with ' +
    '`git push --force-with-lease`.',
  longLivedPush:
    'Never push to main or dev directly: dev changes only through a reviewed PR (pr-reviewer, /git-accept) ' +
    'and main only through release-kit:git-release. Push your branch and open a PR.',
  push:
    'git push may target only feature/*, fix/*, chore/*, hotfix/* or release/* branches (HEAD is the current ' +
    'branch) and v* tags, with -u, --dry-run, -q, -v, -o, --delete, --tags, --force-with-lease or ' +
    '--force-if-includes; never --all, --branches, --prune, a wildcard refspec, or push config through -c, ' +
    'GIT_CONFIG_* or `git config`. Name the branch: `git push -u origin <branch>`.',
  longLived:
    'main and dev are long-lived branches and are never deleted. Delete only merged branches: ' +
    '`git push origin --delete <branch>`.',
  hooks:
    'Never bypass the Lefthook hooks (AGENTS.md § Commits): no --no-verify/-n, core.hooksPath ' +
    'override or LEFTHOOK variables. Fix what the hook rejected and commit again.',
  commitAll:
    'Commit only explicitly staged paths (AGENTS.md § Commits): no `git commit -a`/`--all` or a ' +
    'whole-tree pathspec. Run `git add <path>...`, then `git commit`.',
  addAll:
    'Stage explicit paths only (AGENTS.md § Commits): no `git add -A`/`--all`/`.`/`:/`, `-u` without a ' +
    'path, an exclude-only pathspec or the repo root. Use `git add <path>...`.',
  branchDelete:
    'Delete local branches with `git branch -d`, which refuses unmerged work; never `-D` or ' +
    '`--delete --force` (AGENTS.md § Commits).',
  ghMerge:
    'Merge with `gh pr merge <n> --merge` only, never --admin, --auto or --delete-branch (pr-reviewer.md, ' +
    'git-accept.md, docs/process/lifecycle.md § 6b). Then delete the remote branch only once it is merged: ' +
    '`git fetch --prune origin && git merge-base --is-ancestor origin/<b> origin/dev && git push origin ' +
    '--delete <b>` (origin/main instead of origin/dev for a release/* or hotfix/* branch merged into main), ' +
    'then `git branch -d <b>`.',
  repoDelete: 'Deleting the GitHub repository is never done by an agent; ask the user.',
  stdinShell:
    'The Bash guard cannot inspect commands a shell reads from stdin (heredoc, here-string, pipe, -s, or ' +
    'a process substitution as script). Run the commands directly, or write a script file in the ' +
    'scratchpad and run it.',
  unreadable:
    'The Bash guard could not parse this git/gh command (unbalanced quote or substitution, or unquoted ' +
    'brace expansion). Rewrite it in plain shell syntax.',
}

// The guard asks git for these instead of trusting the command text; tests pass a fixed one.
export type GuardContext = {
  cwd: string
  currentBranch(dir: string): string | null
  repoRoot(dir: string): string | null
}

type Input = 'pipe' | 'heredoc' | 'herestring' | null
type Command = { words: string[]; braces: boolean[]; input: Input }
type Redirect = 'target' | 'herestring' | 'heredoc' | 'heredoc-strip' | null

const BRACE_EXPANSION = /\{[^{}]*(,|\.\.)[^{}]*\}/

// Splits a shell string into simple commands of quote-removed words, flagging unquoted brace expansion and
// stdin fed by a pipe, heredoc or here-string. Command substitutions become commands of their own; heredoc
// bodies and comments are data. Throws on an unterminated construct.
class ShellParser {
  private pos = 0
  private readonly heredocs: { delimiter: string; stripTabs: boolean }[] = []
  private readonly commands: Command[] = []

  constructor(private readonly src: string) {}

  parse(): Command[] {
    this.list(false)
    return this.commands
  }

  private list(inSubstitution: boolean): void {
    const src = this.src
    let words: string[] = []
    let braces: boolean[] = []
    let word: string | null = null
    let unquoted = ''
    let input: Input = null
    let redirect: Redirect = null
    let depth = 0
    const endWord = () => {
      if (word === null) return
      if (redirect === 'heredoc' || redirect === 'heredoc-strip')
        this.heredocs.push({ delimiter: word, stripTabs: redirect === 'heredoc-strip' })
      else if (redirect === null) {
        words.push(word)
        braces.push(BRACE_EXPANSION.test(unquoted))
      }
      redirect = null
      word = null
      unquoted = ''
    }
    const endCommand = () => {
      endWord()
      redirect = null
      if (words.length) {
        this.commands.push({ words, braces, input })
        input = null
      }
      words = []
      braces = []
    }

    while (this.pos < src.length) {
      const c = src[this.pos]
      const next = src[this.pos + 1]
      if (c === ')' && depth === 0 && inSubstitution) {
        this.pos++
        endCommand()
        return
      }
      if (c === ' ' || c === '\t') {
        endWord()
        this.pos++
      } else if (c === '\\' && next === '\n') {
        this.pos += 2
      } else if (c === '\n') {
        endCommand()
        this.pos++
        this.skipHeredocs()
      } else if (c === '#' && word === null) {
        while (this.pos < src.length && src[this.pos] !== '\n') this.pos++
      } else if ((c === '<' || c === '>') && next === '(') {
        this.pos += 2
        this.list(true)
        word = (word ?? '') + SUBSTITUTION
        unquoted += '\0'
      } else if (c === '<' || c === '>' || (c === '&' && next === '>')) {
        if (word !== null && /^\d+$/.test(word)) word = null
        endWord()
        redirect = this.redirection()
        if (redirect === 'herestring') input = 'herestring'
        else if (redirect === 'heredoc' || redirect === 'heredoc-strip') input = 'heredoc'
      } else if (c === ';' || c === '&' || c === '|') {
        endCommand()
        const pipe = c === '|' && next !== '|'
        this.pos += next === c || (c === '|' && next === '&') ? 2 : 1
        input = pipe ? 'pipe' : null
      } else if (c === '(' || c === ')') {
        endCommand()
        if (c === '(') depth++
        else if (depth > 0) depth--
        this.pos++
      } else {
        const part = this.wordPart()
        word = (word ?? '') + part
        unquoted += '\'"$`\\'.includes(c) ? '\0' : part
      }
    }
    if (inSubstitution) throw new Error('unterminated $(')
    endCommand()
  }

  private wordPart(): string {
    const src = this.src
    const c = src[this.pos]
    if (c === "'") {
      const end = src.indexOf("'", this.pos + 1)
      if (end < 0) throw new Error("unterminated '")
      const text = src.slice(this.pos + 1, end)
      this.pos = end + 1
      return text
    }
    if (c === '"') return this.doubleQuoted()
    if (c === '$') return this.dollar()
    if (c === '`') return this.backtick()
    if (c === '\\') {
      this.pos += 2
      return src[this.pos - 1] ?? ''
    }
    this.pos++
    return c
  }

  private doubleQuoted(): string {
    const src = this.src
    let text = ''
    this.pos++
    while (this.pos < src.length) {
      const c = src[this.pos]
      const next = src[this.pos + 1]
      if (c === '"') {
        this.pos++
        return text
      }
      if (c === '\\' && next !== undefined && '$`"\\\n'.includes(next)) {
        text += next === '\n' ? '' : next
        this.pos += 2
      } else if (c === '$' && (next === '(' || next === '{')) {
        text += this.dollar()
      } else if (c === '`') {
        text += this.backtick()
      } else {
        text += c
        this.pos++
      }
    }
    throw new Error('unterminated "')
  }

  private dollar(): string {
    const src = this.src
    const next = src[this.pos + 1]
    if (next === "'") {
      let text = ''
      this.pos += 2
      while (this.pos < src.length && src[this.pos] !== "'") {
        if (src[this.pos] === '\\') this.pos++
        text += src[this.pos] ?? ''
        this.pos++
      }
      if (this.pos >= src.length) throw new Error("unterminated $'")
      this.pos++
      return text
    }
    if (next === '(' && src[this.pos + 2] === '(') return this.skipBalanced('(', ')')
    if (next === '(') {
      this.pos += 2
      this.list(true)
      return SUBSTITUTION
    }
    if (next === '{') return this.skipBalanced('{', '}')
    this.pos++
    return '$'
  }

  // Returns `$((...))` or `${...}` verbatim: arithmetic and parameter expansion run no command.
  private skipBalanced(open: string, close: string): string {
    const start = this.pos
    let depth = 0
    for (this.pos++; this.pos < this.src.length; this.pos++) {
      const c = this.src[this.pos]
      if (c === open) depth++
      else if (c === close && --depth === 0) {
        this.pos++
        return this.src.slice(start, this.pos)
      }
    }
    throw new Error(`unterminated $${open}`)
  }

  private backtick(): string {
    const src = this.src
    let body = ''
    let end = this.pos + 1
    while (end < src.length && src[end] !== '`') {
      if (src[end] === '\\' && '\\`$'.includes(src[end + 1] ?? '')) end++
      body += src[end++]
    }
    if (end >= src.length) throw new Error('unterminated `')
    this.pos = end + 1
    this.commands.push(...new ShellParser(body).parse())
    return SUBSTITUTION
  }

  private redirection(): Redirect {
    const src = this.src
    if (src.startsWith('<<<', this.pos)) {
      this.pos += 3
      return 'herestring'
    }
    if (src.startsWith('<<-', this.pos)) {
      this.pos += 3
      return 'heredoc-strip'
    }
    if (src.startsWith('<<', this.pos)) {
      this.pos += 2
      return 'heredoc'
    }
    if (src[this.pos] === '&') this.pos++
    this.pos++
    if ('>&|'.includes(src[this.pos] ?? '')) this.pos++
    return 'target'
  }

  private skipHeredocs(): void {
    const src = this.src
    for (const { delimiter, stripTabs } of this.heredocs.splice(0)) {
      while (this.pos < src.length) {
        const eol = src.indexOf('\n', this.pos)
        const end = eol < 0 ? src.length : eol
        const line = src.slice(this.pos, end)
        this.pos = end + 1
        if ((stripTabs ? line.replace(/^\t+/, '') : line) === delimiter) break
      }
    }
  }
}

type Option = { name: string; long: boolean; value?: string }
type Spec = {
  shortValue?: string
  shortAttachedValue?: string
  longValue?: string[]
  abbreviations?: boolean
}

function parseArgs(args: string[], spec: Spec) {
  const options: Option[] = []
  const positionals: string[] = []
  const takesValue = (name: string) =>
    (spec.longValue ?? []).some(
      (long) => long === name || (spec.abbreviations && long.startsWith(name)),
    )
  let endOfOptions = false
  for (let i = 0; i < args.length; i++) {
    const arg = args[i]
    if (endOfOptions || arg === '-' || !arg.startsWith('-')) {
      positionals.push(arg)
    } else if (arg === '--') {
      endOfOptions = true
    } else if (arg.startsWith('--')) {
      const eq = arg.indexOf('=')
      const name = eq < 0 ? arg.slice(2) : arg.slice(2, eq)
      const value = eq >= 0 ? arg.slice(eq + 1) : takesValue(name) ? args[++i] : undefined
      options.push({ name, long: true, value })
    } else {
      for (let j = 1; j < arg.length; j++) {
        const name = arg[j]
        const rest = arg.slice(j + 1)
        if (spec.shortValue?.includes(name)) {
          options.push({ name, long: false, value: rest || args[++i] })
          break
        }
        if (spec.shortAttachedValue?.includes(name)) {
          options.push({ name, long: false, value: rest })
          break
        }
        options.push({ name, long: false })
      }
    }
  }
  return { options, positionals }
}

// git accepts any unambiguous prefix of a long option; an ambiguous one fails, so denying it is safe.
const isLong = (option: Option, name: string, abbreviations = true) =>
  option.long && (option.name === name || (abbreviations && name.startsWith(option.name)))
const isShort = (option: Option, name: string) => !option.long && option.name === name
const isTrue = (option: Option) =>
  option.value === undefined || !/^(false|f|0)$/i.test(option.value)

const configKey = (config: string) => config.split('=')[0]
const isHooksPath = (config: string) => configKey(config).toLowerCase() === 'core.hookspath'
const isPushConfig = (config: string) =>
  /^(remote\..+\.(mirror|push)|push\..+)$/i.test(configKey(config))

function checkAssignment(name: string, value: string): string | null {
  if (name === 'LEFTHOOK') return /^(1|true)$/i.test(value) ? null : REASON.hooks
  if (/^LEFTHOOK_(EXCLUDE|SKIP|CONFIG|BIN)/.test(name)) return REASON.hooks
  const keys =
    name === 'GIT_CONFIG_PARAMETERS'
      ? value.split(/\s+/).map((pair) => pair.replace(/'/g, ''))
      : /^GIT_CONFIG_KEY_\d+$/.test(name)
        ? [value]
        : []
  if (keys.some(isHooksPath)) return REASON.hooks
  return keys.some(isPushConfig) ? REASON.push : null
}

const ALLOWED_BRANCH = /^(feature|fix|chore|hotfix|release)\/[^/\s]/

// git push options this guard knows, so an abbreviation resolves the way git resolves it.
const PUSH_OPTIONS = [
  'all',
  'branches',
  'mirror',
  'delete',
  'tags',
  'follow-tags',
  'dry-run',
  'porcelain',
  'force',
  'force-with-lease',
  'force-if-includes',
  'repo',
  'set-upstream',
  'signed',
  'atomic',
  'push-option',
  'receive-pack',
  'exec',
  'prune',
  'no-verify',
  'verify',
  'recurse-submodules',
  'thin',
  'no-thin',
  'quiet',
  'verbose',
  'progress',
  'no-progress',
  'ipv4',
  'ipv6',
]
const PUSH_ALLOWED = [
  'delete',
  'tags',
  'dry-run',
  'force-with-lease',
  'force-if-includes',
  'set-upstream',
  'push-option',
  'quiet',
  'verbose',
]
const PUSH_ALLOWED_SHORT = 'dnquvo'

function pushOption(name: string): string | null {
  if (PUSH_OPTIONS.includes(name)) return name
  const matches = PUSH_OPTIONS.filter((option) => option.startsWith(name))
  return matches.length === 1 ? matches[0] : null
}

function checkDestination(
  ref: string,
  deletes: boolean,
  currentBranch: () => string | null,
): string | null {
  const name = ref === 'HEAD' || ref === '@' ? currentBranch() : ref
  if (name === null) return REASON.push
  const branch = /^(refs\/)?heads\//.test(name) ? name.replace(/^(refs\/)?heads\//, '') : null
  if (branch === null && (/^(refs\/)?tags\//.test(name) || /^v\d/.test(name)))
    return deletes ? REASON.push : null
  const target = branch ?? name
  if (target === 'main' || target === 'dev')
    return deletes ? REASON.longLived : REASON.longLivedPush
  return ALLOWED_BRANCH.test(target) ? null : REASON.push
}

function checkPush(args: string[], currentBranch: () => string | null): string | null {
  const { options, positionals } = parseArgs(args, {
    shortValue: 'o',
    longValue: ['repo', 'receive-pack', 'exec', 'push-option'],
    abbreviations: true,
  })
  let deleting = false
  let tags = false
  for (const option of options) {
    const name = option.long ? pushOption(option.name) : option.name
    if (name === 'force' || name === 'mirror' || name === 'f') return REASON.force
    if (name === 'no-verify') return REASON.hooks
    const allowed = option.long
      ? PUSH_ALLOWED.includes(name ?? '')
      : PUSH_ALLOWED_SHORT.includes(name ?? '')
    if (!allowed) return REASON.push
    deleting ||= name === 'delete' || name === 'd'
    tags ||= name === 'tags'
  }
  const refspecs = positionals.slice(1)
  if (refspecs.length === 0) {
    if (tags && !deleting) return null
    return deleting ? REASON.push : checkDestination('HEAD', false, currentBranch)
  }
  for (const refspec of refspecs) {
    if (refspec.startsWith('+')) return REASON.force
    if (refspec.includes('*')) return REASON.push
    const colon = refspec.indexOf(':')
    if (deleting && colon >= 0) return REASON.push
    const target = colon < 0 ? refspec : refspec.slice(colon + 1)
    if (target === '') return REASON.push
    const reason = checkDestination(target, deleting || colon === 0, currentBranch)
    if (reason) return reason
  }
  return null
}

const isExclude = (pathspec: string) => {
  const long = /^:\(([^)]*)\)/.exec(pathspec)
  if (long) return long[1].split(',').some((magic) => magic.trim() === 'exclude')
  return /^:[/!^]*[!^]/.test(pathspec)
}

// Pathspecs that name the whole tree: from the repo root or the working directory, or only exclusions.
function isWholeTree(pathspecs: string[], isRoot: (path: string) => boolean): boolean {
  if (pathspecs.length > 0 && pathspecs.every(isExclude)) return true
  return pathspecs.some((pathspec) => {
    const magic = /^:(\/|\([^)]*\)|[!^]+)/.exec(pathspec)
    const rest = magic ? pathspec.slice(magic[0].length) : pathspec
    if (!magic && rest === '') return false
    const segments = rest.split('/').filter((segment) => segment !== '' && segment !== '.')
    return (
      segments.length === 0 ||
      (segments.length === 1 && /^\*+$/.test(segments[0])) ||
      segments.every((segment) => segment === '..') ||
      (!magic && isRoot(rest))
    )
  })
}

function checkCommit(args: string[], isRoot: (path: string) => boolean): string | null {
  const { options, positionals } = parseArgs(args, {
    shortValue: 'mFcCt',
    shortAttachedValue: 'Su',
    longValue: [
      'message',
      'file',
      'author',
      'date',
      'template',
      'reuse-message',
      'reedit-message',
      'fixup',
      'squash',
      'cleanup',
      'trailer',
      'pathspec-from-file',
    ],
    abbreviations: true,
  })
  if (options.some((o) => isLong(o, 'no-verify') || isShort(o, 'n'))) return REASON.hooks
  if (options.some((o) => isLong(o, 'all') || isShort(o, 'a'))) return REASON.commitAll
  return isWholeTree(positionals, isRoot) ? REASON.commitAll : null
}

function checkAdd(args: string[], isRoot: (path: string) => boolean): string | null {
  const { options, positionals } = parseArgs(args, {
    longValue: ['chmod', 'pathspec-from-file'],
    abbreviations: true,
  })
  const all = options.some(
    (o) => isLong(o, 'all') || isLong(o, 'no-ignore-removal') || isShort(o, 'A'),
  )
  const fromFile = options.some((o) => isLong(o, 'pathspec-from-file'))
  const update = options.some((o) => isLong(o, 'update') || isShort(o, 'u'))
  if (all || (update && positionals.length === 0 && !fromFile)) return REASON.addAll
  return isWholeTree(positionals, isRoot) ? REASON.addAll : null
}

function checkBranch(args: string[]): string | null {
  const { options } = parseArgs(args, {
    shortValue: 'u',
    longValue: [
      'set-upstream-to',
      'contains',
      'no-contains',
      'merged',
      'no-merged',
      'points-at',
      'sort',
      'format',
    ],
    abbreviations: true,
  })
  const deletes = options.some((o) => isLong(o, 'delete') || isShort(o, 'd'))
  const force = options.some((o) => isLong(o, 'force') || isShort(o, 'f'))
  return options.some((o) => isShort(o, 'D')) || (deletes && force) ? REASON.branchDelete : null
}

function checkConfig(args: string[]): string | null {
  const { options, positionals } = parseArgs(args, {
    shortValue: 'f',
    longValue: ['file', 'blob', 'type', 'default', 'comment', 'value'],
  })
  const key = positionals.findIndex((p) => isHooksPath(p) || isPushConfig(p))
  if (key < 0) return null
  const reads = options.some((o) => (o.long && /^(get|unset|list)/.test(o.name)) || isShort(o, 'l'))
  if (reads || ['get', 'unset', 'list'].includes(positionals[0])) return null
  if (positionals.length <= key + 1) return null
  return isHooksPath(positionals[key]) ? REASON.hooks : REASON.push
}

function checkGit(args: string[], env: string[], context: GuardContext): string | null {
  let dir = context.cwd
  const configs: string[] = []
  let i = 0
  for (; i < args.length && args[i].startsWith('-'); i++) {
    const arg = args[i]
    if (arg === '-c' || arg === '--config-env') configs.push(args[++i] ?? '')
    else if (arg.startsWith('-c')) configs.push(arg.slice(2))
    else if (arg.startsWith('--config-env=')) configs.push(arg.slice('--config-env='.length))
    else if (arg === '-C') dir = resolve(dir, args[++i] ?? '')
    else if (
      ['--git-dir', '--work-tree', '--namespace', '--super-prefix', '--attr-source'].includes(arg)
    )
      i++
  }
  if (configs.some(isHooksPath)) return REASON.hooks
  let root: string | null | undefined
  const isRoot = (path: string) => {
    if (!isAbsolute(path) && !path.split('/').includes('..')) return false
    root ??= context.repoRoot(dir)
    return root !== null && resolve(dir, path) === resolve(root)
  }
  const rest = args.slice(i + 1)
  switch (args[i]) {
    case 'push':
      if (configs.some(isPushConfig) || env.some((name) => name.startsWith('GIT_CONFIG')))
        return REASON.push
      return checkPush(rest, () => context.currentBranch(dir))
    case 'commit':
      return checkCommit(rest, isRoot)
    case 'add':
    case 'stage':
      return checkAdd(rest, isRoot)
    case 'branch':
      return checkBranch(rest)
    case 'config':
      return checkConfig(rest)
    case 'merge':
      return parseArgs(rest, { shortValue: 'mFsX', shortAttachedValue: 'S' }).options.some((o) =>
        isLong(o, 'no-verify'),
      )
        ? REASON.hooks
        : null
    default:
      return null
  }
}

function checkGh(args: string[]): string | null {
  if (args[0] === 'repo' && args[1] === 'delete') return REASON.repoDelete
  if (args[0] !== 'pr' || args[1] !== 'merge') return null
  const { options } = parseArgs(args.slice(2), {
    shortValue: 'bFtAR',
    longValue: ['body', 'body-file', 'subject', 'author-email', 'match-head-commit', 'repo'],
  })
  const bypass = options.some(
    (o) =>
      isTrue(o) &&
      (isLong(o, 'admin', false) ||
        isLong(o, 'auto', false) ||
        isLong(o, 'delete-branch', false) ||
        isLong(o, 'squash', false) ||
        isLong(o, 'rebase', false) ||
        isShort(o, 'd') ||
        isShort(o, 's') ||
        isShort(o, 'r')),
  )
  return bypass ? REASON.ghMerge : null
}

const ASSIGNMENT = /^([A-Za-z_][A-Za-z0-9_]*)\+?=(.*)$/s
const KEYWORDS = new Set(['!', '{', '}', 'if', 'then', 'else', 'elif', 'while', 'until', 'do'])
const SHELLS = new Set(['bash', 'sh', 'zsh', 'dash', 'ksh'])
const DECLARE = new Set(['export', 'declare', 'typeset', 'local', 'readonly'])

const readsStdin = (script: string) =>
  script === '-' || script.includes(SUBSTITUTION) || /^\/(dev\/(stdin|fd\/)|proc\/)/.test(script)

// Skips options of a wrapper command; `withValue` lists the options that take the next word.
function skipOptions(words: string[], i: number, withValue: string[]): number {
  while (i < words.length && words[i].startsWith('-')) {
    if (words[i] === '--') return i + 1
    i += withValue.includes(words[i]) ? 2 : 1
  }
  return i
}

function checkShell(
  args: string[],
  input: Input,
  context: GuardContext,
  depth: number,
): string | null {
  const end = skipOptions(args, 0, ['-o', '+o', '-O', '+O', '--rcfile', '--init-file'])
  const options = args.slice(0, end)
  if (options.some((o) => /^-[a-zA-Z]*c/.test(o)))
    return end < args.length ? check(args[end], context, depth + 1) : null
  if (options.some((o) => o === '-' || /^-[a-zA-Z]*s/.test(o))) return REASON.stdinShell
  if (end >= args.length) return input ? REASON.stdinShell : null
  return readsStdin(args[end]) ? REASON.stdinShell : null
}

function checkSimple(
  { words, braces, input }: Command,
  context: GuardContext,
  depth: number,
): string | null {
  const env: string[] = []
  let i = 0
  while (i < words.length) {
    const word = words[i]
    const assignment = ASSIGNMENT.exec(word)
    if (assignment) {
      const reason = checkAssignment(assignment[1], assignment[2])
      if (reason) return reason
      env.push(assignment[1])
      i++
      continue
    }
    if (KEYWORDS.has(word)) {
      i++
      continue
    }
    const name = word.slice(word.lastIndexOf('/') + 1)
    if (DECLARE.has(name)) {
      for (const arg of words.slice(i + 1)) {
        const declared = ASSIGNMENT.exec(arg)
        const reason = declared && checkAssignment(declared[1], declared[2])
        if (reason) return reason
      }
      return null
    }
    if (name === 'eval') return check(words.slice(i + 1).join(' '), context, depth + 1)
    if (SHELLS.has(name)) return checkShell(words.slice(i + 1), input, context, depth)
    if (name === 'source' || name === '.')
      return readsStdin(words[i + 1] ?? '') ? REASON.stdinShell : null
    if (name === 'env') {
      const split = words.findIndex((w, k) => k > i && (w === '-S' || w === '--split-string'))
      if (split > 0) return check(words.slice(split + 1).join(' '), context, depth + 1)
      i = skipOptions(words, i + 1, ['-u', '--unset', '-C', '--chdir'])
    } else if (name === 'timeout') {
      i = skipOptions(words, i + 1, ['-s', '--signal', '-k', '--kill-after']) + 1
    } else if (name === 'nice') {
      i = skipOptions(words, i + 1, ['-n'])
    } else if (name === 'sudo') {
      i = skipOptions(words, i + 1, ['-u', '-g', '-C', '-D', '-h', '-p', '-r', '-t', '-U'])
    } else if (name === 'xargs') {
      i = skipOptions(words, i + 1, ['-I', '-L', '-n', '-P', '-s', '-d', '-E', '-a'])
    } else if (['command', 'exec', 'nohup', 'time', 'builtin'].includes(name)) {
      i = skipOptions(words, i + 1, ['-a'])
    } else if (name === 'git' || name === 'gh') {
      if (braces.slice(i + 1).some(Boolean)) return REASON.unreadable
      const args = words.slice(i + 1)
      return name === 'git' ? checkGit(args, env, context) : checkGh(args)
    } else {
      return null
    }
  }
  return null
}

function git(dir: string, ...args: string[]): string | null {
  const proc = Bun.spawnSync(['git', '-C', dir, ...args], { stderr: 'ignore' })
  return proc.exitCode === 0 ? proc.stdout.toString().trim() : null
}

export function gitContext(cwd: string): GuardContext {
  return {
    cwd,
    currentBranch: (dir) => {
      const branch = git(dir, 'rev-parse', '--abbrev-ref', 'HEAD')
      return branch && branch !== 'HEAD' ? branch : null
    },
    repoRoot: (dir) => git(dir, 'rev-parse', '--show-toplevel'),
  }
}

// The deny reason for a shell command string, or null when it may run. Throws when it can't parse.
export function check(
  command: string,
  context = gitContext(process.cwd()),
  depth = 0,
): string | null {
  if (depth > MAX_DEPTH) throw new Error('nested too deeply')
  for (const parsed of new ShellParser(command).parse()) {
    const reason = checkSimple(parsed, context, depth)
    if (reason) return reason
  }
  return null
}

function deny(reason: string) {
  const output = {
    hookEventName: 'PreToolUse',
    permissionDecision: 'deny',
    permissionDecisionReason: reason,
  }
  console.log(JSON.stringify({ hookSpecificOutput: output }))
}

// A crashed hook lets the command run, so every failure on a git/gh command becomes a deny.
async function main() {
  let text = ''
  try {
    text = await Bun.stdin.text()
    const payload = JSON.parse(text)
    const command = payload?.tool_input?.command
    if (typeof command !== 'string') return
    text = command
    const cwd = typeof payload.cwd === 'string' && payload.cwd ? payload.cwd : process.cwd()
    const reason = check(command, gitContext(cwd))
    if (reason) deny(reason)
  } catch {
    if (/\b(git|gh)\b/.test(text)) deny(REASON.unreadable)
  }
}

if (import.meta.main) await main()
