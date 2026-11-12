import { afterEach, describe, expect, test } from 'bun:test'
import {
  chmodSync,
  copyFileSync,
  existsSync,
  lstatSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  statSync,
  symlinkSync,
  utimesSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

// Runs the real test-gate.sh, copied into a throwaway repo layout, with bun, dotnet and the backend
// tests stubbed on PATH. Stubs act at a known step (no timing races); none touch the real repo.
const EXAMPLE = 'backend/src/StarterKit.API/appsettings.Example.json'
const CONFIG = 'backend/src/StarterKit.API/appsettings.json'
const EXAMPLE_JSON = '{"CorsSettings":{"AllowedOrigins":["https://example.invalid"]}}\n'
const OWN_JSON = '{"Own":"developer config"}\n'

const RECORD_EXPORT = `if [[ -f ${CONFIG} ]] && cmp -s ${EXAMPLE} ${CONFIG}; then echo copy > export-saw
elif [[ -e ${CONFIG} ]]; then echo other > export-saw; else echo none > export-saw; fi`
const WRITE_OWN = `printf '%s' '${OWN_JSON}' > ${CONFIG}.new && mv -f ${CONFIG}.new ${CONFIG}`

const DIRS = [
  'scripts',
  'backend/scripts',
  'backend/src/StarterKit.API',
  'shared/openapi',
  'stubs',
]

type Layout = { example?: boolean; backendTests?: string; dotnet?: string; config?: string }

const roots: string[] = []
afterEach(() => {
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true })
})

function writeScript(path: string, body: string) {
  writeFileSync(path, `#!/usr/bin/env bash\n${body}\n`)
  chmodSync(path, 0o755)
}

function layout({ example = true, backendTests = '', dotnet = '', config }: Layout = {}) {
  const root = mkdtempSync(join(tmpdir(), 'test-gate-'))
  roots.push(root)
  for (const dir of DIRS) mkdirSync(join(root, dir), { recursive: true })
  copyFileSync(join(import.meta.dir, 'test-gate.sh'), join(root, 'scripts/test-gate.sh'))
  writeFileSync(join(root, 'shared/openapi/openapi.json'), '{"openapi":"3.1.0"}\n')
  if (example) writeFileSync(join(root, EXAMPLE), EXAMPLE_JSON)
  if (config !== undefined) writeFileSync(join(root, CONFIG), config)
  writeScript(join(root, 'stubs/bun'), 'exit 0')
  writeScript(join(root, 'stubs/dotnet'), `${RECORD_EXPORT}\n${dotnet}`)
  writeScript(join(root, 'backend/scripts/test.sh'), backendTests)
  return root
}

function runGate(root: string) {
  // Bash can't trap a signal it inherited as ignored (e.g. from a backgrounded parent): reset INT/HUP first.
  const reset = '$SIG{INT} = $SIG{HUP} = "DEFAULT"; exec @ARGV'
  const proc = Bun.spawnSync(['perl', '-e', reset, 'bash', join(root, 'scripts/test-gate.sh')], {
    env: { ...process.env, PATH: `${join(root, 'stubs')}:${process.env.PATH}` },
  })
  const read = (path: string) =>
    existsSync(join(root, path)) ? readFileSync(join(root, path), 'utf8') : null
  return {
    code: proc.exitCode,
    stdout: proc.stdout.toString(),
    stderr: proc.stderr.toString(),
    exportSaw: read('export-saw')?.trim() ?? null,
    config: read(CONFIG),
    backendTestsRan: existsSync(join(root, 'backend-tests-ran')),
  }
}

describe('test-gate.sh temporary appsettings.json', () => {
  test('removes an untouched copy after the export', () => {
    const result = runGate(layout())
    expect(result.code).toBe(0)
    expect(result.exportSaw).toBe('copy')
    expect(result.stdout).toContain('temporary copy')
    expect(result.config).toBeNull()
  })

  test('keeps a copy edited during the export and warns', () => {
    const result = runGate(layout({ dotnet: WRITE_OWN }))
    expect(result.code).toBe(0)
    expect(result.exportSaw).toBe('copy')
    expect(result.config).toBe(OWN_JSON)
    expect(result.stderr).toContain(`kept ${CONFIG}`)
  })

  test('neither overwrites nor removes a file created before the export', () => {
    const result = runGate(layout({ backendTests: WRITE_OWN }))
    expect(result.code).toBe(0)
    expect(result.exportSaw).toBe('other')
    expect(result.stdout).not.toContain('temporary copy')
    expect(result.config).toBe(OWN_JSON)
  })

  test.each([
    ['its own content', OWN_JSON],
    ['the example content', EXAMPLE_JSON],
  ])('leaves a pre-existing file with %s untouched', (_, content) => {
    const root = layout({ config: content })
    const path = join(root, CONFIG)
    utimesSync(path, new Date('2020-01-01T00:00:00Z'), new Date('2020-01-01T00:00:00Z'))
    const before = statSync(path)
    const result = runGate(root)
    const after = statSync(path)
    expect(result.code).toBe(0)
    expect(result.config).toBe(content)
    expect([after.ino, after.mtimeMs, after.size]).toEqual([
      before.ino,
      before.mtimeMs,
      before.size,
    ])
  })

  test('removes the copy when the export fails', () => {
    const result = runGate(layout({ dotnet: 'exit 1' }))
    expect(result.code).toBe(1)
    expect(result.exportSaw).toBe('copy')
    expect(result.config).toBeNull()
  })

  test('creates no file and fails before the backend tests when appsettings.Example.json is missing', () => {
    const result = runGate(layout({ example: false, backendTests: 'touch backend-tests-ran' }))
    expect(result.code).not.toBe(0)
    expect(result.stderr).toContain(EXAMPLE)
    expect(result.config).toBeNull()
    expect(result.backendTestsRan).toBe(false)
  })

  test('runs with its own appsettings.json when appsettings.Example.json is missing', () => {
    const root = layout({ example: false, config: OWN_JSON })
    const path = join(root, CONFIG)
    utimesSync(path, new Date('2020-01-01T00:00:00Z'), new Date('2020-01-01T00:00:00Z'))
    const before = statSync(path)
    const result = runGate(root)
    const after = statSync(path)
    expect(result.code).toBe(0)
    expect(result.exportSaw).toBe('other')
    expect(result.stderr).not.toContain(EXAMPLE)
    expect(result.config).toBe(OWN_JSON)
    expect([after.ino, after.mtimeMs, after.size]).toEqual([
      before.ino,
      before.mtimeMs,
      before.size,
    ])
  })

  test.each([
    ['present', true],
    ['missing', false],
  ])(
    'fails before the backend tests on a dangling appsettings.json symlink, example %s',
    (_, example) => {
      const root = layout({ example, backendTests: 'touch backend-tests-ran' })
      symlinkSync('missing-target.json', join(root, CONFIG))
      const result = runGate(root)
      expect(result.code).not.toBe(0)
      expect(result.stderr).toContain(`${CONFIG} is a symlink to a missing file`)
      expect(result.backendTestsRan).toBe(false)
      expect(lstatSync(join(root, CONFIG)).isSymbolicLink()).toBe(true)
    },
  )

  test.each([
    ['INT', 130],
    ['HUP', 129],
    ['TERM', 143],
  ])('removes the copy when SIG%s arrives during the export', (signal, code) => {
    const result = runGate(layout({ dotnet: `kill -${signal} "$PPID"` }))
    expect(result.code).toBe(code)
    expect(result.exportSaw).toBe('copy')
    expect(result.config).toBeNull()
  })
})
