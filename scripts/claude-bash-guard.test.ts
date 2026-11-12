import { afterAll, describe, expect, test } from 'bun:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { type GuardContext, check as checkIn } from './claude-bash-guard'

// The guard asks git for the current branch and the repo root; the tables run against this fixed one.
const contextOn = (branch: string | null): GuardContext => ({
  cwd: '/repo',
  currentBranch: () => branch,
  repoRoot: () => '/repo',
})
const check = (command: string) => checkIn(command, contextOn('feature/x'))

const HEREDOC_COMMIT = `git commit -m "$(cat <<'EOF'
chore: guard git push --force and git add .

git add -A, git commit --no-verify and gh pr merge --admin are now denied.
EOF
)"`

const DENY: Record<string, string[]> = {
  'force push': [
    'git push --force',
    'git push -f',
    'git push origin feature/x --force',
    'git push origin feature/x -f',
    'git push -fu origin feature/x',
    'git push -uf origin feature/x',
    'git push origin +feature/x',
    'git push origin +HEAD:feature/x',
    'git push origin "+"feature/x',
    'git push --forc origin feature/x',
    'git push --mirror origin',
    'git push --force-with-lease --force origin feature/x',
    'git -C /tmp/repo push --force',
    'git -c color.ui=never push origin x -f',
    '/usr/bin/git push -f',
    'command git push -f',
    'env FOO=1 git push -f',
    'timeout 30 git push -f',
    'bash -c "git push -f origin x"',
    "sh -lc 'git push origin +x'",
    'eval git push -f',
  ],
  'push to main or delete a long-lived branch': [
    'git push origin main',
    'git push origin HEAD:main',
    'git push origin feature/x:refs/heads/main',
    'git push origin refs/heads/main',
    'git push -u origin main',
    'git push origin :main',
    'git push origin :dev',
    'git push origin --delete dev',
    'git push origin -d main',
    'git push --delete origin feature/x dev',
    'git push origin dev',
    'git push origin HEAD:dev',
    'git push origin feature/x:refs/heads/dev',
    'git push origin HEAD:heads/main',
    'git push origin :heads/dev',
    'git push origin --delete heads/dev',
    'git push origin --delete refs/heads/main',
  ],
  'push outside the allowed destinations': [
    'git push origin --all',
    'git push origin --branches',
    'git push --al origin',
    "git push origin 'refs/heads/*:refs/heads/*'",
    "git push --prune origin 'refs/heads/*:refs/heads/*'",
    'git push --prune origin feature/x',
    "git push origin 'feature/*'",
    'git push origin :',
    'git push origin feature/x:',
    'git push origin some-branch',
    'git push origin HEAD:wip',
    'git push origin HEAD:refs/remotes/origin/main',
    'git push origin :v1.2.0',
    'git push origin --delete v1.2.0',
    'git push origin --delete refs/tags/v1.2.0',
    'git push --repo=origin feature/x',
    'git push --receive-pack=/bin/true origin feature/x',
    'git push --no-verify origin feature/x',
  ],
  'push through config': [
    'git -c remote.origin.mirror=true push origin',
    'git -c remote.origin.push=+feature/x:main push',
    'git -c Remote.Origin.Mirror=true push origin feature/x',
    'git -c push.default=matching push',
    'git --config-env=remote.origin.mirror=MIRROR push origin feature/x',
    'GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=remote.origin.mirror GIT_CONFIG_VALUE_0=true git push origin',
    'GIT_CONFIG_GLOBAL=/tmp/evil git push origin feature/x',
    'export GIT_CONFIG_KEY_0=remote.origin.push && git push origin feature/x',
    'git config remote.origin.mirror true',
    "git config remote.origin.push '+refs/heads/*:refs/heads/*'",
    'git config --local remote.origin.push +HEAD:main',
    'git config set remote.origin.mirror true',
    'git config push.default matching',
  ],
  'brace expansion in git or gh arguments': [
    'git push --{force,quiet} origin feature/x',
    'git push origin {+,}feature/x',
    'git push origin feature/x{,:main}',
    'git commit --{no-verify,quiet} -m "x"',
    'git add --{all,verbose}',
    'git push origin feature/{a..c}',
    'gh pr merge 13 --{merge,admin}',
  ],
  'shell reading commands from stdin': [
    "bash <<'EOF'\ngit push -f origin x\nEOF",
    'bash <<EOF\ngit status\nEOF',
    'sh <<-EOF\n\tgit status\n\tEOF',
    'bash <<< "git push -f origin x"',
    'echo "git push -f origin x" | bash',
    'echo "git status" | sh -s',
    'printf "git status" |& zsh',
    'cat <<EOF | bash\ngit status\nEOF',
    'echo ls | sudo bash',
    'sh -s',
    'bash -s -- arg',
    'bash -',
    'bash /dev/stdin',
    'bash <(echo git push -f origin x)',
    'source <(echo git push -f origin x)',
    '. <(echo git push -f origin x)',
    'echo "git status" | source /dev/stdin',
  ],
  'commit hook bypass': [
    'git commit --no-verify -m "x"',
    'git commit -m "x" --no-verify',
    'git commit -n -m "x"',
    'git commit -nm "x"',
    'git commit --no-verif -m "x"',
    'git -c core.hooksPath=/dev/null commit -m "x"',
    'git -c core.hookspath=/dev/null commit -m "x"',
    'git -ccore.hooksPath=/dev/null commit -m "x"',
    'git --config-env=core.hooksPath=HOOKS commit -m "x"',
    'LEFTHOOK=0 git commit -m "x"',
    'LEFTHOOK_EXCLUDE=prettier git commit -m "x"',
    'LEFTHOOK_SKIP_comments=1 git commit -m "x"',
    'LEFTHOOK_CONFIG=/tmp/empty.yml git commit -m "x"',
    'env LEFTHOOK=0 git commit -m "x"',
    'export LEFTHOOK=0 && git commit -m "x"',
    'export LEFTHOOK=0; git commit -m "x"',
    'GIT_CONFIG_PARAMETERS="\'core.hooksPath\'=\'/dev/null\'" git commit -m "x"',
    'git config core.hooksPath /dev/null',
    'git config --local core.hooksPath .nohooks',
    'git merge --no-verify feature/x',
  ],
  'commit everything': [
    'git commit -a -m "x"',
    'git commit -am "x"',
    'git commit --all -m "x"',
    'git commit -m "x" -a',
    'git commit --amend -a --no-edit',
    'git commit -m "x" .',
    'git commit -m "x" -- :/',
    "git commit -m x -- ':!x'",
    "git commit -m x -- ':(exclude)x' ':^y'",
  ],
  'add everything': [
    'git add -A',
    'git add -A backend/',
    'git add --all',
    'git add --all --dry-run',
    'git add --al',
    'git add --no-ignore-removal',
    'git add .',
    'git add ./',
    'git add -- .',
    'git add :/',
    "git add ':(top)'",
    'git add -fA',
    'git stage -A',
    'git add -u',
    'git add --update',
    'git add --upd',
    'git add -u --',
    'git add -uv',
    "git add ':!nothing'",
    "git add ':^nothing'",
    "git add ':(exclude)x'",
    "git add ':(exclude,glob)x' ':!y'",
    "git add ':/'",
    "git add '*'",
    "git add '**'",
    'git add /repo',
    'git add /repo/',
    'git add /repo/.',
    'git add /repo/backend/..',
    'git -C /repo/backend add ..',
    'git -C backend add /repo',
  ],
  'force-delete a branch': [
    'git branch -D feature/x',
    'git branch -df feature/x',
    'git branch -fd feature/x',
    'git branch -d -f feature/x',
    'git branch --delete --force feature/x',
    'git branch -d --force feature/x',
    'git branch --force --delete feature/x',
  ],
  'gh merge bypass': [
    'gh pr merge 13 --merge --admin',
    'gh pr merge --admin 13',
    'gh pr merge 13 --merge --auto',
    'gh pr merge 13 --merge --delete-branch',
    'gh pr merge 13 --merge -d',
    'gh pr merge 13 -md',
    'gh pr merge 13 --admin=true',
    'gh pr merge 13 --squash',
    'gh pr merge 13 --rebase',
    'gh pr merge 13 -s',
    'gh pr merge 13 -r',
    'gh pr merge 13 -ms',
    'gh pr merge 13 --squash=true',
    'gh repo delete',
    'gh repo delete DaoNguyenTrong/FEEDBACK-HUB --yes',
  ],
  'compound commands (denied part wins)': [
    'git status && git push -f',
    'git fetch origin; git push origin +x',
    'git log --oneline | head -1 || git branch -D x',
    'git add a.ts\ngit commit -nm "x"',
    'git status & git push -f',
    'echo "$(git push -f)"',
    'echo `git push -f`',
    '(cd /tmp && git push -f)',
    '{ git add -A; }',
    'if true; then git push -f; fi',
  ],
}

const ALLOW = [
  'git status',
  'git push --force-with-lease origin feature/x',
  'git push --force-with-lease=feature/x:0123abc origin feature/x',
  'git push --force-if-includes --force-with-lease origin feature/x',
  'git push -u origin chore/x',
  'git push -u origin release/v1.2.0',
  'git push origin v1.2.0',
  'git push origin refs/tags/v1.2.0',
  'git push origin --tags',
  'git push --tags',
  'git push origin --delete hotfix/old-name',
  'git push origin :fix/old-name',
  'git push origin main:feature/x',
  'git push origin HEAD:refs/heads/feature/y',
  'git push origin HEAD:heads/fix/y',
  'git push -o ci.skip origin feature/x',
  'git push --dry-run -q origin feature/x',
  'git push -nv origin feature/x',
  'git push --set-upstream origin feature/x',
  'git push --force-with origin feature/x',
  'git push',
  'git push origin',
  'git push origin HEAD',
  'git push -u origin HEAD',
  'git push -u origin @',
  'git -C /repo push',
  // release-kit 2.0.2 (git-release SKILL.md, commands/git-mr.md, git-sync.md, git-commit.md)
  'git push origin release/v1.2.0',
  'git push -u origin hotfix/fix-login',
  'git push -u origin hotfix/v1.2.1',
  'git push origin --delete hotfix/fix-login',
  'git push -u origin chore/release-v1.2.0-changelog',
  'git push -u origin chore/reconcile-main-v1.2.0',
  'git push --force-with-lease=chore/x:0123abc origin chore/x',
  'git config branch.release/v1.2.0.releaseKitBase dev',
  'git config branch.hotfix/fix-login.releaseKitBase main',
  'git config --get branch.feature/x.releaseKitBase',
  'git checkout -b chore/reconcile-main-v1.2.0 origin/dev',
  'git ls-remote --exit-code --heads origin chore/reconcile-main-v1.2.0',
  "git ls-remote --tags origin 'refs/tags/v*'",
  "git tag --list 'v[0-9]*' --sort=-v:refname | grep -E '^v[0-9]+\\.[0-9]+\\.[0-9]+$' | head -1",
  'git rev-list origin/main..HEAD | grep -F -x -f <(git rev-list origin/main..origin/dev)',
  "git show -s --format=%B abc | grep -oE -m1 '(pull request #|\\(#)[0-9]+' | grep -oE '[0-9]+$'",
  "gh api repos/{owner}/{repo}/commits/abc/pulls --jq '.[].number'",
  "gh pr list --head release/v1.2.0 --base main --state open --json number --jq '.[0].number'",
  'gh pr create --base main --head release/v1.2.0 --title "Release v1.2.0" --body "x"',
  'gh pr create --base dev --head feature/x --title "x" --body-file /tmp/body.md',
  'gh pr merge 13 --merge --subject "Release v1.2.0"',
  'git stash push -m "git-sync auto-stash"',
  'git stash pop',
  'git rebase origin/dev',
  'git fetch --prune origin && git merge-base --is-ancestor origin/feature/x origin/dev && git push origin --delete feature/x',
  'git merge-base --is-ancestor origin/release/v1.2.0 origin/main && git push origin --delete release/v1.2.0',
  'git rev-parse HEAD@{1}',
  'git diff stash@{0}..stash@{1}',
  "git log --format='{%h,%s}'",
  'git config --unset remote.origin.mirror',
  'git config --get remote.origin.url',
  'git commit -m "--force and -n in text"',
  'git commit -m "x" -m "git push -f, git add ., --no-verify"',
  'git commit -mn',
  'git commit -m "-a"',
  'git commit --no-edit',
  'git commit --amend --no-edit',
  'git commit -m "x" -- backend/a.cs frontend/b.ts',
  HEREDOC_COMMIT,
  'git add path/a path/b',
  'git add backend/',
  'git add -u backend/',
  'git add --update -- backend/a.cs',
  "git add backend/ ':!backend/generated'",
  'git add /repo/backend/a.cs',
  'git add -- "file with spaces.ts"',
  'git branch -d feature/x',
  'git branch --delete feature/x',
  'git branch -f dev origin/dev',
  'git branch -m hotfix/v1.2.1',
  'git branch --format "%(refname) -D" --list',
  'git merge --no-ff origin/main -m "Merge main into dev after v1.2.0"',
  'git reset --hard origin/dev',
  'git clean -fdx',
  'git tag -a v1.2.0 -m "Release v1.2.0" origin/main',
  'git config --get core.hooksPath',
  'git config --unset core.hooksPath',
  'git -c core.editor=true rebase --continue',
  'gh pr merge 13 --merge',
  'gh pr merge 13 --merge --subject "Merge release/v1.2.0 --admin --auto"',
  'gh pr merge 13 --merge -t "-d"',
  'gh pr merge 13 --merge --auto=false',
  'gh pr create --base dev --title "x" --body "never gh pr merge --admin"',
  'gh pr view 13 --json state',
  'gh repo view',
  'echo "git push -f"',
  "printf '%s' 'git add -A'",
  'grep -n "git push --force" AGENTS.md',
  'LEFTHOOK_VERBOSE=1 git commit -m "x"',
  'FOO=1 git push -u origin feature/x',
  'git status && git push --force-with-lease origin feature/x',
  'ls -la # git push -f',
  'bun test scripts/',
  'bash scripts/test-gate.sh',
  'bash -c "git status"',
  'bash -n scripts/dev-up.sh',
  'git status || bash scripts/dev-up.sh',
  'echo x | grep x',
  'cat <<EOF > /tmp/body.md\ngit push -f\nEOF',
  'source scripts/env.sh',
  'echo {a,b}',
]

describe('claude-bash-guard check', () => {
  for (const [rule, commands] of Object.entries(DENY)) {
    test.each(commands)(`denies ${rule}: %s`, (command) => {
      expect(check(command)).not.toBeNull()
    })
  }

  test.each(ALLOW)('allows %s', (command) => {
    expect(check(command)).toBeNull()
  })

  test('the reason names the allowed alternative', () => {
    expect(check('git push origin +x')).toContain('--force-with-lease')
    expect(check('git push origin dev')).toContain('PR')
    expect(check('git push origin --all')).toContain('feature/*')
    expect(check('git add -A')).toContain('explicit path')
    expect(check('git branch -D x')).toContain('git branch -d')
    expect(check('echo ls | bash')).toContain('script file')
  })

  test('the gh merge reason gives the branch cleanup for both bases', () => {
    const reason = check('gh pr merge 13 --merge --delete-branch')
    expect(reason).toContain('--merge')
    expect(reason).toContain('git merge-base --is-ancestor origin/<b> origin/dev')
    expect(reason).toContain('origin/main')
    expect(reason).toContain('git branch -d')
  })
})

describe('claude-bash-guard push without a refspec', () => {
  const NO_REFSPEC = ['git push', 'git push origin', 'git push origin HEAD', 'git push -u origin']

  test.each(NO_REFSPEC)('allows %s on an allowed branch', (command) => {
    expect(checkIn(command, contextOn('fix/login'))).toBeNull()
  })

  test.each(NO_REFSPEC)('denies %s on main, dev, other branches or an unknown HEAD', (command) => {
    for (const branch of ['main', 'dev', 'backup/pr13', null])
      expect(checkIn(command, contextOn(branch))).not.toBeNull()
  })

  test('does not ask git for the branch when the refspec names it', () => {
    const context: GuardContext = {
      ...contextOn(null),
      currentBranch: () => {
        throw new Error('not expected')
      },
    }
    expect(checkIn('git push origin feature/x', context)).toBeNull()
    expect(checkIn('git push --tags', context)).toBeNull()
  })
})

describe('claude-bash-guard hook', () => {
  const run = (stdin: string) => {
    const proc = Bun.spawnSync(['bun', join(import.meta.dir, 'claude-bash-guard.ts')], {
      stdin: Buffer.from(stdin),
    })
    return { code: proc.exitCode, stdout: proc.stdout.toString() }
  }
  const payload = (command: string, cwd?: string) =>
    JSON.stringify({
      hook_event_name: 'PreToolUse',
      tool_name: 'Bash',
      tool_input: { command },
      cwd,
    })
  const repo = mkdtempSync(join(tmpdir(), 'bash-guard-'))
  const git = (...args: string[]) => Bun.spawnSync(['git', '-C', repo, ...args])
  git('init', '-q', '-b', 'main')
  git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '--allow-empty', '-m', 'init')
  afterAll(() => rmSync(repo, { recursive: true, force: true }))

  test('resolves the current branch of the payload cwd for a push without a refspec', () => {
    expect(
      JSON.parse(run(payload('git push', repo)).stdout).hookSpecificOutput.permissionDecision,
    ).toBe('deny')
    git('switch', '-q', '-c', 'feature/x')
    expect(run(payload('git push', repo))).toEqual({ code: 0, stdout: '' })
    expect(
      JSON.parse(run(payload(`git add ${repo}`, repo)).stdout).hookSpecificOutput
        .permissionDecision,
    ).toBe('deny')
  })

  test('prints a PreToolUse deny decision for a denied command', () => {
    const result = run(payload('git push -f'))
    expect(result.code).toBe(0)
    const output = JSON.parse(result.stdout).hookSpecificOutput
    expect(output.hookEventName).toBe('PreToolUse')
    expect(output.permissionDecision).toBe('deny')
    expect(output.permissionDecisionReason).toContain('--force-with-lease')
  })

  test('prints nothing for an allowed command', () => {
    expect(run(payload('git push --force-with-lease origin feature/x'))).toEqual({
      code: 0,
      stdout: '',
    })
  })

  test('denies a git command it cannot read instead of letting it through', () => {
    const result = run(payload('git commit -m "unterminated'))
    expect(JSON.parse(result.stdout).hookSpecificOutput.permissionDecision).toBe('deny')
  })

  test('stays silent on unreadable input that is not about git or gh', () => {
    expect(run('not json')).toEqual({ code: 0, stdout: '' })
  })
})
